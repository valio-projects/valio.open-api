# 03 — Новые TypeScript HTTP клиенты, фреймворки, MSW и TanStack Query

> **Этап:** 1 и 3  
> **Приоритет:** Высокий  
> **Зависит от:** 01_AST_REFACTORING, 02_PLUGIN_ARCHITECTURE

---

## Содержание

1. [Архитектура: как устроен HTTP клиент шаг](#1-архитектура-как-устроен-http-клиент-шаг)
2. [Транспорты: Ky, Undici, Ofetch, Got](#2-транспорты-ky-undici-ofetch-got)
3. [Angular HttpClient (RxJS и Promise)](#3-angular-httpclient-rxjs-и-promise)
4. [SolidStart](#4-solidstart)
5. [MSW (Mock Service Worker)](#5-msw-mock-service-worker)
6. [TanStack Query для всех фреймворков](#6-tanstack-query-для-всех-фреймворков)
7. [Карта AST нод и рендереров](#7-карта-ast-нод-и-рендереров)
8. [Структура файлов](#8-структура-файлов)

---

## 1. Архитектура: как устроен HTTP клиент шаг

### Общий алгоритм каждого транспортного Step'а

Каждый `ICodeGenerationStep` для HTTP транспорта следует одному алгоритму:

```
ExecuteAsync(context):
    1. Получить options = (TypeScriptGenerationOptions) context.PluginOptions
    2. Получить irDocument = context.IrDocument
    3. Для каждой операции в irDocument.Clients[*].Operations:
       a. Построить AST через AstBuilder (операция → AST ноды)
       b. Рендерить AST в строку через TypeScriptAstRenderer
       c. Определить путь к файлу (по convention или конфигу)
       d. Добавить в результат files[path] = code
    4. Вернуть files
```

### Базовый класс транспортного шага

```csharp
// ValIO.Generators.TypeScript/Steps/BaseTransportClientStep.cs

public abstract class BaseTransportClientStep : ICodeGenerationStep
{
    protected readonly TypeScriptAstRenderer Renderer;
    protected readonly IFilePathResolver PathResolver;

    public abstract string Name { get; }
    public abstract int Order { get; }
    public abstract string FileSuffix { get; }  // ".fetch.ts", ".axios.ts", ".ky.ts", etc.

    public bool ShouldExecute(GenerationContext context)
    {
        var opts = (TypeScriptGenerationOptions)context.PluginOptions!;
        return context.Configuration.GenerateClients
            && opts.Transports.Contains(TransportId)
            && context.IrDocument.Clients.Any(c => c.Operations.Count > 0);
    }

    protected abstract string TransportId { get; }

    /// <summary>
    /// Строит AST-ноду SourceFile для конкретной операции.
    /// Каждый транспорт переопределяет логику построения.
    /// </summary>
    protected abstract SourceFileNode BuildOperationSourceFile(
        IrOperation operation,
        TypeScriptGenerationOptions options,
        IServiceProvider services);

    public async Task<IReadOnlyDictionary<string, string>> ExecuteAsync(
        GenerationContext context,
        CancellationToken ct = default)
    {
        var options = (TypeScriptGenerationOptions)context.PluginOptions!;
        var files = new Dictionary<string, string>();

        foreach (var client in context.IrDocument.Clients)
        {
            foreach (var operation in client.Operations)
            {
                ct.ThrowIfCancellationRequested();

                var sourceFile = BuildOperationSourceFile(operation, options, context.Services);
                var code = Renderer.Render(sourceFile);
                var operationSlug = ToKebabCase(operation.MethodName);
                var path = PathResolver.ResolveClientPath(
                    $"{operationSlug}/{operationSlug}{FileSuffix}",
                    context.Configuration);

                files[path] = code;
            }
        }

        return files;
    }
}
```

---

## 2. Транспорты: Ky, Undici, Ofetch, Got

### 2.1 Ky

**Что такое Ky:** Tiny & elegant JavaScript HTTP client based on the Fetch API.  
**Отличия от Fetch:** Fluent API, встроенный retry, hooks, json(), text() helpers.

**Проект:** `ValIO.Generators.TypeScript.Ky`  
**Файл суффикс:** `.ky.ts`

#### Генерируемый код (пример)

```typescript
// getUser/getUser.ky.ts
import ky from 'ky';
import type { GetUserParams } from './getUser.params';
import type { User } from '../models/user/user';

export const getUser = async (params: GetUserParams): Promise<User> => {
  return ky.get(`/api/users/${params.id}`, {
    searchParams: {
      ...(params.includeDeleted !== undefined && { includeDeleted: params.includeDeleted }),
    },
    headers: {
      'Accept': 'application/json',
    },
  }).json<User>();
};
```

#### AST построение

```csharp
// ValIO.Generators.TypeScript.Ky/Steps/KyClientStep.cs

public sealed class KyClientStep : BaseTransportClientStep
{
    public override string Name => "Ky HTTP Client";
    public override int Order => 42;
    public override string FileSuffix => ".ky.ts";
    protected override string TransportId => "ky";

    protected override SourceFileNode BuildOperationSourceFile(
        IrOperation operation,
        TypeScriptGenerationOptions options,
        IServiceProvider services)
    {
        var imports = new List<ImportNode>
        {
            new ImportNode(
                ImportKind.Default,
                ["ky"],
                "ky"),           // import ky from 'ky'
        };

        // Params import (если есть параметры)
        // ... (аналогично Fetch/Axios)

        // Тело функции
        var kyMethodCall = BuildKyCall(operation);
        var returnType = BuildReturnType(operation);
        
        var funcDecl = new FunctionDeclarationNode(
            new IdentifierNode(operation.MethodName),
            BuildParameters(operation),
            returnType,
            isAsync: true,
            isExported: true,
            body: new BlockNode([
                new ReturnStatementNode(
                    new AwaitExpressionNode(kyMethodCall))
            ]));

        return new SourceFileNode(imports, [funcDecl]);
    }

    private CallExpressionNode BuildKyCall(IrOperation operation)
    {
        // ky.get(`/path/${id}`, { searchParams: {...}, headers: {...} })
        // ky.post(`/path`, { json: body, headers: {...} })
        
        var method = operation.HttpMethod.ToLowerInvariant();  // "get", "post", etc.
        var pathTemplate = BuildPathTemplate(operation);
        
        var optionsObj = BuildKyOptionsObject(operation);
        
        // Для GET: возвращаем .json<T>()
        // Для методов с телом: ky.post({json: body}).json<T>()
        var kyCall = new CallExpressionNode(
            new MemberAccessExpressionNode(
                new IdentifierExpressionNode(new IdentifierNode("ky")),
                new IdentifierNode(method)),
            [pathTemplate, optionsObj]);
        
        // Добавляем .json<T>() для десериализации
        return new CallExpressionNode(
            new MemberAccessExpressionNode(kyCall, new IdentifierNode("json")),
            [],
            typeArguments: [BuildResponseType(operation)]);
    }
}
```

### 2.2 Undici

**Что такое Undici:** Node.js HTTP/1.1 и HTTP/2 клиент, встроен в Node 18+.  
**Отличия от Fetch:** Высокая производительность, connection pooling, stream support.  
**Целевая аудитория:** Node.js серверные приложения, не браузер.

**Проект:** `ValIO.Generators.TypeScript.Undici`  
**Файл суффикс:** `.undici.ts`

#### Генерируемый код

```typescript
// getUser/getUser.undici.ts
import { request } from 'undici';
import type { GetUserParams } from './getUser.params';
import type { User } from '../models/user/user';

export const getUser = async (params: GetUserParams): Promise<User> => {
  const url = new URL(`/api/users/${params.id}`, process.env.API_BASE_URL);
  if (params.includeDeleted !== undefined) {
    url.searchParams.set('includeDeleted', String(params.includeDeleted));
  }

  const { statusCode, body } = await request(url, {
    method: 'GET',
    headers: { 'Accept': 'application/json' },
  });

  if (statusCode < 200 || statusCode >= 300) {
    throw new Error(`HTTP Error: ${statusCode}`);
  }

  return body.json() as Promise<User>;
};
```

### 2.3 Ofetch

**Что такое Ofetch:** Fetch wrapper с удобными хелперами, используется в Nuxt.  
**Отличия от Fetch:** Автоматический JSON parse, interceptors, retry, error handling.

**Проект:** `ValIO.Generators.TypeScript.Ofetch`  
**Файл суффикс:** `.ofetch.ts`

#### Генерируемый код

```typescript
// getUser/getUser.ofetch.ts
import { ofetch } from 'ofetch';
import type { GetUserParams } from './getUser.params';
import type { User } from '../models/user/user';

export const getUser = async (params: GetUserParams): Promise<User> => {
  return ofetch<User>(`/api/users/${params.id}`, {
    method: 'GET',
    query: {
      ...(params.includeDeleted !== undefined && { includeDeleted: params.includeDeleted }),
    },
  });
};
```

### 2.4 Got

**Что такое Got:** Human-friendly HTTP library для Node.js.  
**Отличия:** Поддержка stream, pagination, retry, hooks. Node.js only.

**Проект:** `ValIO.Generators.TypeScript.Got`  
**Файл суффикс:** `.got.ts`

#### Генерируемый код

```typescript
// getUser/getUser.got.ts
import got from 'got';
import type { GetUserParams } from './getUser.params';
import type { User } from '../models/user/user';

export const getUser = async (params: GetUserParams): Promise<User> => {
  return got.get(`/api/users/${params.id}`, {
    searchParams: {
      ...(params.includeDeleted !== undefined && { includeDeleted: params.includeDeleted }),
    },
  }).json<User>();
};
```

---

## 3. Angular HttpClient (RxJS и Promise)

**Проект:** `ValIO.Generators.TypeScript.Angular`

### 3.1 RxJS-based API (основная)

**Транспорт ID:** `angular-rxjs`  
**Файл суффикс:** `.angular.ts`

#### Генерируемый код (RxJS)

```typescript
// getUser/getUser.angular.ts
import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import type { GetUserParams } from './getUser.params';
import type { User } from '../models/user/user';

@Injectable({ providedIn: 'root' })
export class GetUserService {
  constructor(private readonly http: HttpClient) {}

  getUser(params: GetUserParams): Observable<User> {
    let httpParams = new HttpParams();
    if (params.includeDeleted !== undefined) {
      httpParams = httpParams.set('includeDeleted', String(params.includeDeleted));
    }

    return this.http.get<User>(`/api/users/${params.id}`, {
      params: httpParams,
    });
  }
}
```

#### AST-ноды для Angular-специфичного синтаксиса

Angular требует специфичных нод (декоратор `@Injectable`, `HttpParams`, `Observable`):

```csharp
// ValIO.Generators.TypeScript.Angular/Ast/Nodes/

// Нода Injectable-декорированного класса (extends ClassDeclarationNode)
public sealed class AngularInjectableNode : AstNode
{
    public string ServiceName { get; }
    public bool ProvidedInRoot { get; }
    public IReadOnlyList<AngularConstructorParamNode> ConstructorParams { get; }
    public IReadOnlyList<AngularMethodNode> Methods { get; }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        // Через IAngularAstVisitor (расширение visitor'а)
        if (visitor is IAngularAstVisitor<TResult> angularVisitor)
            return angularVisitor.VisitAngularInjectable(this);
        return visitor.DefaultVisit(this);  // fallback
    }
    // ...
}

// Нода HttpParams builder
public sealed class HttpParamsBuilderNode : ExpressionNode
{
    public IReadOnlyList<(string Key, ExpressionNode Value)> Params { get; }
    // ...
}

// Observable<T> тип
public sealed class ObservableTypeNode : GenericTypeNode
{
    public TypeNode InnerType { get; }
    // Рендерится как "Observable<T>"
}
```

#### IAngularAstVisitor — расширение

```csharp
// ValIO.Generators.TypeScript.Angular/Ast/Visitors/IAngularAstVisitor.cs

public interface IAngularAstVisitor<TResult> : IBaseAstVisitor<TResult>
{
    TResult VisitAngularInjectable(AngularInjectableNode node);
    TResult VisitAngularMethod(AngularMethodNode node);
    TResult VisitHttpParamsBuilder(HttpParamsBuilderNode node);
}
```

#### AngularAstRenderer — специфичный рендерер

```csharp
// ValIO.Generators.TypeScript.Angular/Ast/Renderers/AngularAstRenderer.cs

public sealed class AngularAstRenderer : TypeScriptAstRenderer, IAngularAstVisitor<string>
{
    public string VisitAngularInjectable(AngularInjectableNode node)
    {
        var sb = new StringBuilder();
        sb.AppendLine("@Injectable({ providedIn: 'root' })");
        sb.AppendLine($"export class {node.ServiceName} {{");
        // constructor с private readonly params
        sb.AppendLine($"  constructor({RenderConstructorParams(node.ConstructorParams)}) {{}}");
        sb.AppendLine();
        // методы
        foreach (var method in node.Methods)
            sb.Append(VisitAngularMethod(method));
        sb.AppendLine("}");
        return sb.ToString();
    }

    public string VisitAngularMethod(AngularMethodNode node)
    {
        // Рендерим Observable-возвращающий метод
    }
}
```

### 3.2 Promise/async-based API

**Транспорт ID:** `angular-promise`  
**Файл суффикс:** `.angular-async.ts`

#### Генерируемый код (Promise)

```typescript
// getUser/getUser.angular-async.ts
import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { lastValueFrom } from 'rxjs';
import type { GetUserParams } from './getUser.params';
import type { User } from '../models/user/user';

@Injectable({ providedIn: 'root' })
export class GetUserService {
  constructor(private readonly http: HttpClient) {}

  async getUser(params: GetUserParams): Promise<User> {
    let httpParams = new HttpParams();
    if (params.includeDeleted !== undefined) {
      httpParams = httpParams.set('includeDeleted', String(params.includeDeleted));
    }

    return lastValueFrom(
      this.http.get<User>(`/api/users/${params.id}`, { params: httpParams })
    );
  }
}
```

Разница только в: `lastValueFrom()` обёртка, `async` метод, нет `Observable<>` в типе возврата.

### 3.3 NgRx вариант (дополнительно)

**Транспорт ID:** `angular-ngrx`

Генерирует NgRx Effects для интеграции с NgRx Store:

```typescript
// getUser/getUser.effect.ts
import { Injectable } from '@angular/core';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { HttpClient } from '@angular/common/http';
import { switchMap, map, catchError } from 'rxjs/operators';
import { of } from 'rxjs';
import * as GetUserActions from './getUser.actions';

@Injectable()
export class GetUserEffects {
  getUser$ = createEffect(() =>
    this.actions$.pipe(
      ofType(GetUserActions.getUser),
      switchMap(({ params }) =>
        this.http.get<User>(`/api/users/${params.id}`).pipe(
          map(user => GetUserActions.getUserSuccess({ user })),
          catchError(error => of(GetUserActions.getUserFailure({ error })))
        )
      )
    )
  );

  constructor(
    private actions$: Actions,
    private http: HttpClient,
  ) {}
}
```

---

## 4. SolidStart

**Проект:** `ValIO.Generators.TypeScript.Solid`  
**Транспорт ID:** `solid`  
**Файл суффикс:** `.solid.ts`

SolidStart использует native fetch (или createResource для SSR).

#### Генерируемый код

```typescript
// getUser/getUser.solid.ts
import { createResource } from 'solid-js';
import type { GetUserParams } from './getUser.params';
import type { User } from '../models/user/user';

export const fetchGetUser = async (params: GetUserParams): Promise<User> => {
  const url = new URL(`/api/users/${params.id}`, import.meta.env.VITE_API_BASE_URL);
  if (params.includeDeleted !== undefined) {
    url.searchParams.set('includeDeleted', String(params.includeDeleted));
  }

  const response = await fetch(url, {
    method: 'GET',
    headers: { 'Accept': 'application/json' },
  });

  if (!response.ok) throw new Error(`HTTP ${response.status}`);
  return response.json() as Promise<User>;
};

// SolidJS resource wrapper
export const createGetUserResource = (params: () => GetUserParams) =>
  createResource(params, fetchGetUser);
```

---

## 5. MSW (Mock Service Worker)

**Проект:** `ValIO.Generators.TypeScript.MSW`  
**Транспорт ID:** `msw`  
**Файл суффикс:** `.msw.ts`

### Что генерируется

MSW хендлеры для всех эндпоинтов, готовые к использованию в тестах и Storybook.

#### Генерируемый код

```typescript
// mocks/handlers.ts (один файл для всего API)
import { http, HttpResponse } from 'msw';
import type { User } from '../models/user/user';
import type { CreateUserRequest } from '../models/createUserRequest/createUserRequest';

export const handlers = [
  // GET /api/users/{id}
  http.get('/api/users/:id', ({ params }) => {
    return HttpResponse.json<User>({
      id: params.id as string,
      name: 'Mock User',
      email: 'mock@example.com',
      // ... default mock data
    });
  }),

  // POST /api/users
  http.post('/api/users', async ({ request }) => {
    const body = await request.json() as CreateUserRequest;
    return HttpResponse.json<User>({
      id: crypto.randomUUID(),
      name: body.name,
      email: body.email,
    }, { status: 201 });
  }),

  // DELETE /api/users/{id}
  http.delete('/api/users/:id', () => {
    return new HttpResponse(null, { status: 204 });
  }),
];
```

### Алгоритм MSW Step

```csharp
// ValIO.Generators.TypeScript.MSW/Steps/MswStep.cs

public sealed class MswStep : ICodeGenerationStep
{
    public string Name => "MSW Handlers";
    public int Order => 100;  // Генерируется последним

    public bool ShouldExecute(GenerationContext context)
    {
        var opts = (TypeScriptGenerationOptions)context.PluginOptions!;
        return opts.GenerateMsw
            && context.IrDocument.Clients.Any(c => c.Operations.Count > 0);
    }

    public async Task<IReadOnlyDictionary<string, string>> ExecuteAsync(
        GenerationContext context, CancellationToken ct = default)
    {
        var files = new Dictionary<string, string>();
        var allHandlers = new List<AstNode>();

        foreach (var client in context.IrDocument.Clients)
        {
            foreach (var operation in client.Operations)
            {
                ct.ThrowIfCancellationRequested();
                allHandlers.Add(BuildMswHandler(operation));
            }
        }

        // Все хендлеры в один файл mocks/handlers.ts
        var handlersFile = BuildHandlersFile(allHandlers, context.IrDocument);
        var code = _renderer.Render(handlersFile);
        var path = PathResolver.ResolveMocksPath("handlers.ts", context.Configuration);
        files[path] = code;

        // Дополнительно: browser.ts и server.ts (setup файлы)
        files[PathResolver.ResolveMocksPath("browser.ts", context.Configuration)] 
            = GenerateBrowserSetup();
        files[PathResolver.ResolveMocksPath("server.ts", context.Configuration)] 
            = GenerateServerSetup();

        return files;
    }

    private AstNode BuildMswHandler(IrOperation operation)
    {
        // http.get('/path/:param', handler)
        // http.post('/path', handler)
        // etc.
        var method = operation.HttpMethod.ToLowerInvariant();
        var pathPattern = BuildMswPathPattern(operation.Path); // /api/users/{id} → /api/users/:id

        var handlerFn = BuildHandlerFunction(operation);

        return new MswHandlerNode(method, pathPattern, handlerFn);
    }
}
```

### MSW-специфичные AST ноды

```csharp
// MSW handler нода
public sealed class MswHandlerNode : AstNode
{
    public string HttpMethod { get; }    // "get", "post", "put", "delete"
    public string PathPattern { get; }   // "/api/users/:id"
    public AstNode HandlerFunction { get; }  // Arrow function или function expression

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is IMswAstVisitor<TResult> mswVisitor)
            return mswVisitor.VisitMswHandler(this);
        throw new NotSupportedException("MSW visitor required");
    }
}

// HttpResponse.json<T>({...}) нода
public sealed class MswJsonResponseNode : ExpressionNode
{
    public TypeNode ResponseType { get; }
    public AstNode MockData { get; }
    public int StatusCode { get; }
    // ...
}
```

---

## 6. TanStack Query для всех фреймворков

**Проект:** `ValIO.Generators.TypeScript.TanStack`

### Общая архитектура

TanStack Query v5 имеет единый API для всех фреймворков (`@tanstack/query-core`), но разные entry points:
- React: `@tanstack/react-query`
- Vue: `@tanstack/vue-query`
- Angular: `@tanstack/angular-query-experimental`
- Solid: `@tanstack/solid-query`
- Svelte: `@tanstack/svelte-query`

### Базовый класс

```csharp
// ValIO.Generators.TypeScript.TanStack/Steps/BaseTanStackStep.cs

public abstract class BaseTanStackStep : ICodeGenerationStep
{
    protected abstract string PackageName { get; }   // "@tanstack/react-query"
    protected abstract string UseQueryHook { get; }  // "useQuery"
    protected abstract string UseMutationHook { get; } // "useMutation"
    protected abstract string FileSuffix { get; }    // ".query.ts" или ".query.tsx"
    protected abstract string TransportId { get; }

    public bool ShouldExecute(GenerationContext context)
    {
        var opts = (TypeScriptGenerationOptions)context.PluginOptions!;
        return opts.QueryWrapper == TransportId
            && context.IrDocument.Clients.Any(c => c.Operations.Count > 0);
    }

    public async Task<IReadOnlyDictionary<string, string>> ExecuteAsync(
        GenerationContext context, CancellationToken ct = default)
    {
        var files = new Dictionary<string, string>();
        var opts = (TypeScriptGenerationOptions)context.PluginOptions!;
        var transport = opts.GetPrimaryTransport();  // "fetch", "axios", "ky", etc.

        foreach (var client in context.IrDocument.Clients)
        {
            foreach (var operation in client.Operations)
            {
                ct.ThrowIfCancellationRequested();

                var isMutation = IsMutation(operation);
                var sourceFile = BuildHookFile(operation, transport, isMutation);
                var code = _renderer.Render(sourceFile);
                var slug = ToKebabCase(operation.MethodName);
                var path = PathResolver.ResolveClientPath(
                    $"{slug}/{slug}{FileSuffix}", context.Configuration);

                files[path] = code;
            }
        }

        return files;
    }

    private static bool IsMutation(IrOperation op)
        => op.HttpMethod is "POST" or "PUT" or "PATCH" or "DELETE";
}
```

### 6.1 TanStack Query для Vue

**Транспорт ID:** `tanstack-vue`  
**Пакет:** `@tanstack/vue-query`

#### Генерируемый код

```typescript
// getUser/getUser.query.ts
import { useQuery, useMutation } from '@tanstack/vue-query';
import type { MaybeRef } from 'vue';
import { getUser } from './getUser.fetch';  // или .axios.ts
import type { GetUserParams } from './getUser.params';
import type { User } from '../models/user/user';

export const getUserQueryKey = (params: GetUserParams) =>
  ['getUser', params] as const;

export const useGetUserQuery = (params: MaybeRef<GetUserParams>) =>
  useQuery({
    queryKey: computed(() => getUserQueryKey(unref(params))),
    queryFn: () => getUser(unref(params)),
  });
```

**Особенности Vue:**
- Параметры оборачиваются в `MaybeRef<T>` (реактивность)
- `computed()` для реактивного queryKey
- `unref()` для получения значения из ref

### 6.2 TanStack Query для Angular

**Транспорт ID:** `tanstack-angular`  
**Пакет:** `@tanstack/angular-query-experimental`

#### Генерируемый код

```typescript
// getUser/getUser.query.ts
import { injectQuery, injectMutation } from '@tanstack/angular-query-experimental';
import { signal, computed } from '@angular/core';
import { getUser } from './getUser.fetch';
import type { GetUserParams } from './getUser.params';
import type { User } from '../models/user/user';

export const getUserQueryKey = (params: GetUserParams) =>
  ['getUser', params] as const;

export const injectGetUserQuery = (params: () => GetUserParams) =>
  injectQuery(() => ({
    queryKey: getUserQueryKey(params()),
    queryFn: () => getUser(params()),
  }));
```

**Особенности Angular:**
- Используется `injectQuery` вместо `useQuery`
- Параметры — функции `() => T` (signals-compatible)
- Требует Angular 16+ (inject API)

### 6.3 TanStack Query для Solid

**Транспорт ID:** `tanstack-solid`  
**Пакет:** `@tanstack/solid-query`

#### Генерируемый код

```typescript
// getUser/getUser.query.ts
import { createQuery, createMutation } from '@tanstack/solid-query';
import { getUser } from './getUser.fetch';
import type { Accessor } from 'solid-js';
import type { GetUserParams } from './getUser.params';
import type { User } from '../models/user/user';

export const getUserQueryKey = (params: GetUserParams) =>
  ['getUser', params] as const;

export const createGetUserQuery = (params: Accessor<GetUserParams>) =>
  createQuery(() => ({
    queryKey: getUserQueryKey(params()),
    queryFn: () => getUser(params()),
  }));
```

**Особенности Solid:**
- `createQuery` вместо `useQuery`
- Параметры — `Accessor<T>` (reactive accessor)
- Конфигурация всегда через функцию `() => ({...})`

### 6.4 TanStack Query для Svelte

**Транспорт ID:** `tanstack-svelte`  
**Пакет:** `@tanstack/svelte-query`

#### Генерируемый код

```typescript
// getUser/getUser.query.ts
import { createQuery, createMutation } from '@tanstack/svelte-query';
import { derived, type Readable } from 'svelte/store';
import { getUser } from './getUser.fetch';
import type { GetUserParams } from './getUser.params';
import type { User } from '../models/user/user';

export const getUserQueryKey = (params: GetUserParams) =>
  ['getUser', params] as const;

export const createGetUserQuery = (params: Readable<GetUserParams>) =>
  createQuery(
    derived(params, ($params) => ({
      queryKey: getUserQueryKey($params),
      queryFn: () => getUser($params),
    }))
  );
```

**Особенности Svelte:**
- `createQuery` вместо `useQuery`
- Параметры как Svelte stores (`Readable<T>`)
- `derived` для реактивного queryKey

---

## 7. Карта AST нод и рендереров

### Новые AST ноды, необходимые

| Нода | Для чего | Проект |
|------|---------|--------|
| `KyCallNode` | `ky.get(...).json<T>()` | TypeScript.Ky |
| `UndiciRequestNode` | `request(url, opts)` | TypeScript.Undici |
| `AngularInjectableNode` | `@Injectable({...}) class ...` | TypeScript.Angular |
| `HttpParamsBuilderNode` | `new HttpParams().set(...)` | TypeScript.Angular |
| `ObservableTypeNode` | `Observable<T>` тип | TypeScript.Angular |
| `NgRxEffectNode` | `createEffect(() => ...)` | TypeScript.Angular |
| `MswHandlerNode` | `http.get('/path', handler)` | TypeScript.MSW |
| `MswJsonResponseNode` | `HttpResponse.json<T>({...})` | TypeScript.MSW |
| `SolidResourceNode` | `createResource(params, fn)` | TypeScript.Solid |
| `VueMaybeRefTypeNode` | `MaybeRef<T>` тип | TypeScript.TanStack |
| `SvelteReadableTypeNode` | `Readable<T>` тип | TypeScript.TanStack |

### INodeRenderer реализации

Каждый новый проект добавляет `ITypeScriptNodeRenderer`:

```
TypeScript.Ky           → KyNodeRenderer
TypeScript.Undici       → UndiciNodeRenderer
TypeScript.Ofetch       → OfetchNodeRenderer
TypeScript.Got          → GotNodeRenderer
TypeScript.Angular      → AngularNodeRenderer
TypeScript.Solid        → SolidNodeRenderer
TypeScript.MSW          → MswNodeRenderer
TypeScript.TanStack     → TanStackNodeRenderer (общий для всех фреймворков)
```

---

## 8. Структура файлов

### Новые проекты

```
ValIO.Generators.TypeScript.Ky/
├── KyGeneratorPlugin.cs
├── KyOptionsDescriptor.cs
├── Steps/
│   └── KyClientStep.cs
├── Ast/
│   ├── Nodes/KyCallNode.cs
│   ├── Builders/KyAstBuilder.cs
│   └── Renderers/KyNodeRenderer.cs
└── ValIO.Generators.TypeScript.Ky.csproj

ValIO.Generators.TypeScript.Angular/
├── AngularGeneratorPlugin.cs
├── AngularOptionsDescriptor.cs
├── Steps/
│   ├── AngularRxjsClientStep.cs
│   ├── AngularPromiseClientStep.cs
│   └── AngularNgrxStep.cs
├── Ast/
│   ├── Nodes/
│   │   ├── AngularInjectableNode.cs
│   │   ├── HttpParamsBuilderNode.cs
│   │   └── ObservableTypeNode.cs
│   ├── Visitors/IAngularAstVisitor.cs
│   ├── Builders/AngularAstBuilder.cs
│   └── Renderers/AngularNodeRenderer.cs
└── ValIO.Generators.TypeScript.Angular.csproj

ValIO.Generators.TypeScript.MSW/
├── MswGeneratorPlugin.cs
├── Steps/MswStep.cs
├── Ast/
│   ├── Nodes/
│   │   ├── MswHandlerNode.cs
│   │   └── MswJsonResponseNode.cs
│   ├── Visitors/IMswAstVisitor.cs
│   ├── Builders/MswAstBuilder.cs
│   └── Renderers/MswNodeRenderer.cs
└── ValIO.Generators.TypeScript.MSW.csproj

ValIO.Generators.TypeScript.TanStack/
├── TanStackGeneratorPlugin.cs
├── Steps/
│   ├── TanStackReactStep.cs
│   ├── TanStackVueStep.cs
│   ├── TanStackAngularStep.cs
│   ├── TanStackSolidStep.cs
│   └── TanStackSvelteStep.cs
├── Ast/
│   ├── Nodes/
│   │   ├── VueMaybeRefTypeNode.cs
│   │   └── SvelteReadableTypeNode.cs
│   ├── Builders/TanStackAstBuilder.cs
│   └── Renderers/TanStackNodeRenderer.cs
└── ValIO.Generators.TypeScript.TanStack.csproj
```

### Конфигурация в valio.config.json

```json
{
  "projects": {
    "api-angular": {
      "language": "typescript",
      "schema": "./openapi.json",
      "output": "./src/api",
      "options": {
        "transports": ["angular-rxjs"],
        "queryWrapper": "tanstack-angular",
        "validation": "zod4"
      }
    },
    "api-solid": {
      "language": "typescript",
      "schema": "./openapi.json",
      "output": "./src/api",
      "options": {
        "transports": ["solid"],
        "queryWrapper": "tanstack-solid"
      }
    },
    "api-vue": {
      "language": "typescript",
      "schema": "./openapi.json",
      "output": "./src/api",
      "options": {
        "transports": ["fetch"],
        "queryWrapper": "tanstack-vue",
        "msw": true
      }
    },
    "api-svelte": {
      "language": "typescript",
      "schema": "./openapi.json",
      "output": "./src/api",
      "options": {
        "transports": ["ky"],
        "queryWrapper": "tanstack-svelte"
      }
    }
  }
}
```
