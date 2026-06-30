# Дорожная карта улучшений ValIO — TypeScript-генератор

Документ описывает, **что** и **как** можно улучшить в генерации клиентов, схем, хуков и интеграции с Next.js.
Для каждого пункта приведено сравнение **«Сейчас» → «Предлагается»** с примерами кода.

> Часть пунктов уже реализована (отмечены ✅): дискриминируемый union ответов (BUG #6), `coerce` для query-параметров,
> Zod-метаданные `.describe()` / `.default()`, типизированные схемы параметров. Остальные пункты — предложения.

Содержание:
1. [Транспорты (axios / fetch / ky / ofetch / undici / got)](#1-транспорты)
2. [Схемы (zod / valibot / typebox)](#2-схемы)
3. [Хуки / queries / mutations (TanStack Query)](#3-хуки--queries--mutations)
4. [Next.js (clients / methods / endpoints / models)](#4-nextjs)

---

## 1. Транспорты

### 1.1. Режим «безопасного» парсинга ответа (safe parse)

**Проблема.** Валидация ответа делает `Schema.parse(...)`, который **бросает** `ZodError` прямо во время запроса.
Потребитель вынужден оборачивать каждый вызов в `try/catch`, теряя дискриминируемый union.

**Сейчас:**
```ts
const result = await GetUser();          // бросит ZodError, если тело не прошло валидацию
```

**Предлагается** опция `validation.onError: "throw" | "safe"`. В режиме `safe` — `safeParse` и отдельный вариант union:
```ts
// generated
const parsed = UserSchema.safeParse(response.data);
if (!parsed.success) {
  return { status: response.status, body: response.data, isSuccessful: false, isFailed: true,
           validationError: parsed.error } satisfies GetUserResultValidationError;
}
return { status: 200, body: parsed.data, isSuccessful: true, isFailed: false };
```
```ts
// consumer — без try/catch
const r = await GetUser();
if (r.isSuccessful) r.body.fullName;        // валидное тело
else if ("validationError" in r) { /* … */ }
```

### 1.2. Типизированные заголовки ответа

**Проблема.** Результат отдаёт только `status`/`body`. Заголовки (`X-Total-Count`, `RateLimit-*`) недоступны.

**Сейчас:**
```ts
interface GetUsersResult200 { status: 200; body: User[]; isSuccessful: true; isFailed: false; }
```
**Предлагается** добавить `headers` (типизированный для документированных в OpenAPI заголовков):
```ts
interface GetUsersResult200 {
  status: 200; body: User[]; isSuccessful: true; isFailed: false;
  headers: { "x-total-count"?: string };       // из responses.200.headers
}
```

### 1.3. Несколько content-type и бинарные/потоковые тела

**Проблема.** Берётся только первый content-type (`response.Content.FirstOrDefault()`); `application/octet-stream`,
`text/event-stream` (SSE), скачивание файлов не типизированы.

**Сейчас:**
```ts
const data = await response.json();           // всегда JSON
```
**Предлагается** ветвление по `Accept`/content-type:
```ts
// body: Blob для octet-stream
const body = await response.blob();
// или ReadableStream для text/event-stream
const body = response.body;                   // тип ReadableStream<Uint8Array>
```

### 1.4. Единый слой middleware для всех транспортов

**Проблема.** Перехват запроса есть только у axios (кастомный инстанс). Для fetch/ky/ofetch — нет единого механизма
(инъекция токена, логирование, повтор).

**Сейчас (только axios):**
```ts
const axiosInstance: AxiosInstance = apiMutator;   // кастомный инстанс
```
**Предлагается** общий seam `requestMiddleware` / `responseMiddleware`:
```ts
configureClient({
  requestMiddleware: (req) => { req.headers.Authorization = `Bearer ${getToken()}`; return req; },
  responseMiddleware: (res) => { logLatency(res); return res; },
});
```

### 1.5. Базовый URL из `servers` и окружения

**Проблема.** Базовый URL не выводится из OpenAPI `servers` и не конфигурируется единообразно.

**Сейчас:**
```ts
const response = await fetch(`/api/users/${id}`);     // относительный путь
```
**Предлагается:**
```ts
const BASE_URL = process.env.NEXT_PUBLIC_API_URL ?? "https://api.example.com"; // servers[0].url
const response = await fetch(`${BASE_URL}/api/users/${id}`);
```

### 1.6. Тайм-ауты и повторы (retry) единообразно

**Проблема.** Тайм-аут/повтор по-разному (или никак) у разных транспортов.

**Предлагается** общий `RequestPolicy`:
```ts
await GetUser({ timeout: 5000, retry: { attempts: 3, backoff: "exponential" } });
```

### 1.7. Эргономика результата (`isOk` / `unwrap`)

**Проблема.** Дискриминируемый union (после BUG #6) удобно сужать, но нет готовых хелперов.

**Предлагается** генерировать утилиты:
```ts
export const isOk = <R extends { isSuccessful: boolean }>(r: R): r is Extract<R, { isSuccessful: true }> => r.isSuccessful;

const r = await GetUser();
if (isOk(r)) r.body.fullName;            // сужение без приведений
const user = unwrap(await GetUser());    // бросит при ошибке, вернёт тело при успехе
```

### 1.8. Сквозной `AbortSignal`

**Проблема.** `signal` не всегда пробрасывается во все транспорты и в `queryFn` TanStack.

**Предлагается:**
```ts
useQuery({ queryKey, queryFn: ({ signal }) => GetUser(params, { signal }) });  // отмена при размонтировании
```

---

## 2. Схемы

### 2.1. Схема как единственный источник истины (`z.infer`)

**Проблема.** Интерфейс модели и Zod-схема генерируются **независимо** → могут разойтись.

**Сейчас (два независимых артефакта):**
```ts
// models/user/user.ts
export interface User { id: string; role?: UserRole | null; }
// models/user/user.schema.ts
export const UserSchema: ZodSchema<User> = z.object({ id: z.string(), role: UserRoleSchema.nullable().optional() });
```
**Предлагается** режим `typesFromSchemas`: тип выводится из схемы — рассинхрон невозможен:
```ts
export const UserSchema = z.object({ id: z.string(), role: UserRoleSchema.nullable().optional() });
export type User = z.infer<typeof UserSchema>;
```

### 2.2. Типизированные result-схемы

**Проблема.** Схемы параметров уже типизированы (`ZodSchema<T>` ✅), а result-схемы (`{op}.results.schemas.ts`) — нет.

**Сейчас:**
```ts
export const GetUsersResult200Schema = z.array(UserSchema);
```
**Предлагается** аннотировать **типом тела** (не конвертом `{status,body,…}`):
```ts
export const GetUsersResult200Schema: ZodSchema<User[]> = z.array(UserSchema);
```

### 2.3. `z.discriminatedUnion` для `oneOf` + discriminator

**Проблема.** `oneOf` с дискриминатором генерируется как `z.union([...])` — медленнее и хуже сообщения об ошибках.

**Сейчас:**
```ts
export const PetSchema = z.union([DogSchema, CatSchema]);
```
**Предлагается:**
```ts
export const PetSchema = z.discriminatedUnion("kind", [DogSchema, CatSchema]);
```

### 2.4. Метаданные: `example` и `deprecated`

**Проблема.** `.describe()` и `.default()` уже добавлены ✅; `example`/`deprecated` — ещё нет.

**Сейчас:**
```ts
limit: z.coerce.number().int().optional().default(20).describe("Page size."),
```
**Предлагается** (Zod v4 `.meta`):
```ts
limit: z.coerce.number().int().optional().default(20)
  .describe("Page size.").meta({ example: 20, deprecated: false }),
```

### 2.5. Строгость объектов (`strict` / `passthrough`)

**Проблема.** `additionalProperties` из OpenAPI не влияет на строгость Zod-объекта.

**Предлагается** маппинг:
```ts
// additionalProperties: false
export const UserSchema = z.object({ /* … */ }).strict();
// additionalProperties: true
export const UserSchema = z.object({ /* … */ }).passthrough();
```

---

## 3. Хуки / queries / mutations

### 3.1. `queryOptions()` (TanStack Query v5)

**Проблема.** Сейчас генерируется самописный объект опций; идиоматичный примитив v5 — `queryOptions()`,
который переиспользуется в `useQuery`, `prefetchQuery`, `useSuspenseQuery`, лоадерах роутера.

**Сейчас:**
```ts
export const getGetUsersQueryOptions = (options?: GetGetUsersQueryOptionsParams): GetGetUsersQueryOptionsResult => { /* … */ };
```
**Предлагается:**
```ts
export const getUsersQueryOptions = (params?: GetUsersParams) =>
  queryOptions({ queryKey: getGetUsersQueryKey(params), queryFn: ({ signal }) => GetUsers(params, { signal }) });

useQuery(getUsersQueryOptions(params));                 // клиент
queryClient.prefetchQuery(getUsersQueryOptions(params)); // сервер/прелоад
```

### 3.2. Suspense-варианты

**Сейчас:**
```ts
export const useGetUsers = (options?) => useQuery(/* … */);
```
**Предлагается** дополнительно:
```ts
export const useSuspenseGetUsers = (params?: GetUsersParams) => useSuspenseQuery(getUsersQueryOptions(params));
```

### 3.3. Реальный `getNextPageParam` для infinite-query

**Проблема.** Infinite-хук есть, но `getNextPageParam` обычно заглушка; пагинацию можно вывести из OpenAPI
(`page` / `offset` / `cursor`).

**Сейчас:**
```ts
getNextPageParam: () => undefined,           // заглушка
```
**Предлагается:**
```ts
getNextPageParam: (lastPage, pages) => lastPage.body.nextCursor ?? undefined,  // из схемы ответа
```

### 3.4. `enabled` по обязательным параметрам

**Проблема.** Запрос с обязательным `id` стартует даже при `id === undefined`.

**Предлагается:**
```ts
useQuery({ ...getUserQueryOptions(params), enabled: params?.id != null });
```

### 3.5. Типизированная ошибка мутации + инвалидация

**Проблема.** Ошибка хука — голый `Error`; нет связи мутации с затрагиваемыми query-ключами.

**Сейчас:**
```ts
export type CreateUserMutationError = Error;
```
**Предлагается** типизированную ошибку из union ответов + подсказки инвалидации:
```ts
export type CreateUserMutationError = CreateUserResult400 | CreateUserResult422;
// сгенерированная подсказка: какие ключи инвалидировать после успеха
export const createUserInvalidates = [getGetUsersQueryKey()] as const;
```

### 3.6. Реестр `queryKeys`

**Предлагается** единый типобезопасный объект ключей:
```ts
export const queryKeys = {
  getUsers: getGetUsersQueryKey,
  getUser: getGetUserQueryKey,
} as const;
queryClient.invalidateQueries({ queryKey: queryKeys.getUsers() });
```

---

## 4. Next.js

### 4.1. RSC-совместимый fetch-транспорт

**Проблема.** Паттерн глобального axios-инстанса ориентирован на клиент. В Server Components/Server Actions нужен
`fetch` без `"use client"`.

**Сейчас:**
```ts
import axios from "axios";
const axiosInstance: AxiosInstance = axios;       // клиентский инстанс
```
**Предлагается** серверный fetch-клиент + расширения кэша Next:
```ts
// server-safe, без "use client"
export async function GetUser(params: GetUserParams) {
  const res = await fetch(`${BASE_URL}/api/users/${params.id}`, {
    next: { revalidate: 60, tags: [`user:${params.id}`] },   // кэш Next.js
  });
  /* … */
}
```

### 4.2. Теги кэша + ревалидация

**Предлагается** генерировать тег на операцию и хелпер ревалидации:
```ts
export const userTag = (id: string) => `user:${id}`;
// в Server Action после мутации:
import { revalidateTag } from "next/cache";
revalidateTag(userTag(id));
```

### 4.3. Server Actions для мутаций

**Предлагается** обёртки `"use server"`:
```ts
"use server";
export async function createUserAction(_prev: unknown, formData: FormData) {
  const r = await CreateUser({ body: { name: String(formData.get("name")) } });
  if (r.isSuccessful) revalidateTag("users");
  return r;
}
// <form action={createUserAction}> … </form>
```

### 4.4. Прелоад на сервере + гидрация на клиенте

**Предлагается** (в паре с `queryOptions()` из 3.1):
```ts
// server component
const qc = new QueryClient();
await qc.prefetchQuery(getUsersQueryOptions());
return <HydrationBoundary state={dehydrate(qc)}><UsersList /></HydrationBoundary>;
// client — useGetUsers() подхватит данные без повторного запроса
```

### 4.5. Базовый URL по окружению (сервер/клиент)

**Предлагается:**
```ts
const BASE_URL = typeof window === "undefined"
  ? process.env.API_URL_INTERNAL            // внутренний адрес на сервере
  : process.env.NEXT_PUBLIC_API_URL;        // публичный на клиенте
```

### 4.6. Проброс cookie/заголовков в RSC

**Предлагается** хелпер для серверных запросов:
```ts
import { cookies } from "next/headers";
await GetUser(params, { headers: { cookie: cookies().toString() } });   // forward auth
```

### 4.7. Edge-runtime совместимость

**Проблема.** `axios` тянет Node-зависимости и несовместим с Edge.
**Предлагается** для маршрутов с `export const runtime = "edge"` использовать `fetch`-транспорт по умолчанию.

### 4.8. `generateStaticParams` из path-параметров

**Предлагается** хелпер для SSG/ISR:
```ts
export async function generateStaticParams() {
  const r = await GetUsers();
  return r.isSuccessful ? r.body.map((u) => ({ id: u.id })) : [];
}
```
