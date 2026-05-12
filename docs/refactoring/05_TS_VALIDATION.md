# 05 — TypeScript Validation: Valibot, TypeBox, Zod v3/v4

> **Этап:** 2  
> **Приоритет:** Высокий  
> **Зависит от:** 01_AST_REFACTORING, 02_PLUGIN_ARCHITECTURE

---

## Содержание

1. [Текущее состояние Zod](#1-текущее-состояние-zod)
2. [Zod v3 vs Zod v4 — различия и поддержка обеих версий](#2-zod-v3-vs-zod-v4--различия-и-поддержка-обеих-версий)
3. [Valibot](#3-valibot)
4. [TypeBox](#4-typebox)
5. [Унифицированный ValidationAstBuilder](#5-унифицированный-validationastbuilder)
6. [Архитектура: IValidationSchemaRenderer](#6-архитектура-ivalidationschemasrenderer)
7. [Структура файлов](#7-структура-файлов)

---

## 1. Текущее состояние Zod

### Что генерируется сейчас

Текущий `ZodSchemaRenderer` генерирует для каждой модели файл `.schema.ts`:

```typescript
// models/user/user.schema.ts (текущий вывод)
import { z } from 'zod';

export const userSchema = z.object({
  id: z.string(),
  name: z.string(),
  email: z.string(),
  avatar: z.string().optional(),
  createdAt: z.string().datetime(),
});

export type User = z.infer<typeof userSchema>;
```

### Проблемы текущей реализации

1. Нет различия между Zod v3 и Zod v4 — разные пакеты, разный API
2. `ValidationLibrary` enum в Core знает только `Zod`, `Yup`, `Valli` (нет Valibot, TypeBox)
3. `ZodAstVisitorBase` дублирует TS методы вместо composition

---

## 2. Zod v3 vs Zod v4 — различия и поддержка обеих версий

### Ключевые различия

| Аспект | Zod v3 | Zod v4 |
|--------|--------|--------|
| Пакет | `zod` (≥3.x) | `zod` (≥4.0) или `zod/v4` |
| Import | `import { z } from 'zod'` | `import { z } from 'zod'` |
| `.datetime()` | `z.string().datetime()` | `z.iso.datetime()` |
| Error API | `z.ZodError` | `z.ZodError` (совместимо) |
| `.nullable()` | `z.nullable(t)` или `t.nullable()` | то же |
| Pipe | `z.string().pipe(z.coerce.number())` | `z.pipe(z.string(), z.coerce.number())` |
| Discriminated Union | `z.discriminatedUnion('type', [...])` | то же |
| Performance | Baseline | ~14x быстрее |
| Bundle size | ~13kb | ~2kb (mini) или ~7kb |

### Zod v4 основные изменения в API

```typescript
// Zod v3:
z.string().datetime()       // ISO datetime
z.string().email()
z.string().url()

// Zod v4:
z.iso.datetime()            // ISO datetime (новый namespace)
z.email()                   // теперь primitive, не string method
z.url()
z.uuid()
```

### Реализация: ZodV3SchemaRenderer и ZodV4SchemaRenderer

```csharp
// Базовый класс для Zod рендереров
public abstract class BaseZodSchemaRenderer : IValidationSchemaRenderer
{
    protected abstract string ZodDatetimeExpression { get; }
    protected abstract string ZodEmailExpression { get; }
    protected abstract string ZodUuidExpression { get; }

    // Общая логика маппинга IR → Zod выражения
    protected virtual string MapIrTypeToZod(IrType irType) => irType switch
    {
        IrPrimitiveType pt => pt.Kind switch
        {
            IrPrimitiveKind.String   => "z.string()",
            IrPrimitiveKind.Integer  => "z.number().int()",
            IrPrimitiveKind.Float    => "z.number()",
            IrPrimitiveKind.Boolean  => "z.boolean()",
            IrPrimitiveKind.Date     => "z.string().date()",
            IrPrimitiveKind.DateTime => ZodDatetimeExpression,
            IrPrimitiveKind.Uuid     => ZodUuidExpression,
            IrPrimitiveKind.Binary   => "z.string()",
            _ => "z.unknown()"
        },
        IrArrayType arr      => $"z.array({MapIrTypeToZod(arr.ElementType)})",
        IrDictionaryType d   => $"z.record({MapIrTypeToZod(d.KeyType)}, {MapIrTypeToZod(d.ValueType)})",
        IrNullableType n     => $"{MapIrTypeToZod(n.InnerType)}.nullable()",
        IrOptionalType o     => $"{MapIrTypeToZod(o.InnerType)}.optional()",
        IrModelReference ref => $"{ToCamelCase(ref.ModelName)}Schema",
        IrUnionType u        => $"z.union([{string.Join(", ", u.Types.Select(MapIrTypeToZod))}])",
        IrIntersectionType i => $"z.intersection({string.Join(", ", i.Types.Select(MapIrTypeToZod))})",
        _ => "z.unknown()"
    };
}

// Zod v3
public sealed class ZodV3SchemaRenderer : BaseZodSchemaRenderer
{
    public string ValidationLibraryId => "zod3";

    protected override string ZodDatetimeExpression => "z.string().datetime()";
    protected override string ZodEmailExpression    => "z.string().email()";
    protected override string ZodUuidExpression     => "z.string().uuid()";
}

// Zod v4
public sealed class ZodV4SchemaRenderer : BaseZodSchemaRenderer
{
    public string ValidationLibraryId => "zod4";

    protected override string ZodDatetimeExpression => "z.iso.datetime()";
    protected override string ZodEmailExpression    => "z.email()";
    protected override string ZodUuidExpression     => "z.uuid()";
}
```

### Генерируемый код Zod v4

```typescript
// models/user/user.schema.ts (Zod v4)
import { z } from 'zod';

export const userSchema = z.object({
  id: z.uuid(),
  name: z.string(),
  email: z.email(),
  avatar: z.string().optional(),
  createdAt: z.iso.datetime(),
});

export type User = z.infer<typeof userSchema>;
```

---

## 3. Valibot

**Библиотека:** `valibot` — modular, type-safe validation.  
**Особенности:** Дерево зависимостей (tree-shakeable), очень маленький bundle size.  
**Проект:** `ValIO.Generators.TypeScript.Valibot`

### API Valibot

```typescript
import * as v from 'valibot';  // или named imports

// Primitive
v.string()
v.number()
v.boolean()
v.null_()
v.undefined_()
v.unknown()

// String formats
v.pipe(v.string(), v.email())
v.pipe(v.string(), v.uuid())
v.pipe(v.string(), v.url())
v.pipe(v.string(), v.isoDateTime())

// Object
v.object({
  id: v.string(),
  name: v.string(),
  email: v.pipe(v.string(), v.email()),
})

// Array
v.array(v.string())

// Optional / Nullable
v.optional(v.string())           // optional (undefined allowed)
v.nullable(v.string())           // nullable (null allowed)
v.nullish(v.string())            // null | undefined allowed

// Union
v.union([v.string(), v.number()])

// Infer type
type User = v.InferOutput<typeof userSchema>;
```

### Генерируемый код Valibot

```typescript
// models/user/user.schema.ts
import * as v from 'valibot';

export const userSchema = v.object({
  id: v.pipe(v.string(), v.uuid()),
  name: v.string(),
  email: v.pipe(v.string(), v.email()),
  avatar: v.optional(v.string()),
  createdAt: v.pipe(v.string(), v.isoDateTime()),
  age: v.pipe(v.number(), v.integer(), v.minValue(0)),
  tags: v.array(v.string()),
  metadata: v.record(v.string(), v.unknown()),
});

export type User = v.InferOutput<typeof userSchema>;
```

### ValibotSchemaRenderer

```csharp
// ValIO.Generators.TypeScript.Valibot/ValibotSchemaRenderer.cs

public sealed class ValibotSchemaRenderer : IValidationSchemaRenderer
{
    public string ValidationLibraryId => "valibot";

    // Маппинг IrType → Valibot выражение
    private string MapIrTypeToValibot(IrType irType) => irType switch
    {
        IrPrimitiveType pt => pt.Kind switch
        {
            IrPrimitiveKind.String   => "v.string()",
            IrPrimitiveKind.Integer  => "v.pipe(v.number(), v.integer())",
            IrPrimitiveKind.Float    => "v.number()",
            IrPrimitiveKind.Boolean  => "v.boolean()",
            IrPrimitiveKind.Date     => "v.pipe(v.string(), v.isoDate())",
            IrPrimitiveKind.DateTime => "v.pipe(v.string(), v.isoDateTime())",
            IrPrimitiveKind.Uuid     => "v.pipe(v.string(), v.uuid())",
            IrPrimitiveKind.Binary   => "v.string()",
            _ => "v.unknown()"
        },
        IrArrayType arr      => $"v.array({MapIrTypeToValibot(arr.ElementType)})",
        IrDictionaryType d   => $"v.record({MapIrTypeToValibot(d.KeyType)}, {MapIrTypeToValibot(d.ValueType)})",
        IrNullableType n     => $"v.nullable({MapIrTypeToValibot(n.InnerType)})",
        IrOptionalType o     => $"v.optional({MapIrTypeToValibot(o.InnerType)})",
        IrModelReference ref => $"{ToCamelCase(ref.ModelName)}Schema",
        IrUnionType u        => $"v.union([{string.Join(", ", u.Types.Select(MapIrTypeToValibot))}])",
        _ => "v.unknown()"
    };

    public string RenderModelSchema(IrModel model) { /* ... */ }
    public string RenderEnumSchema(IrEnum irEnum) { /* ... */ }
    public string GetImportStatement() => "import * as v from 'valibot';";
    public string GetTypeInferExpression(string schemaName)
        => $"v.InferOutput<typeof {schemaName}>";
}
```

### Valibot AST ноды

Valibot использует вложенные вызовы функций (`v.pipe()`, `v.object()`, `v.array()`, etc.) — это обычные `CallExpressionNode` из Core. Специальные AST ноды для Valibot **не нужны** — достаточно базовых.

Единственное исключение — `v.pipe()` для форматированных строк:

```csharp
// Valibot pipe: v.pipe(v.string(), v.email())
// Это просто CallExpressionNode с MemberAccess + args
// Специальной ноды не нужно

// В рендерере строим напрямую:
private string BuildPipedString(params string[] validators)
{
    if (validators.Length == 1) return validators[0];
    return $"v.pipe({string.Join(", ", validators)})";
}
```

---

## 4. TypeBox

**Библиотека:** `@sinclair/typebox` — JSON Schema + TypeScript type inference.  
**Особенности:** Генерирует JSON Schema валидный для стандарта, совместим с Fastify, AJV.  
**Проект:** `ValIO.Generators.TypeScript.TypeBox`

### API TypeBox

```typescript
import { Type, Static } from '@sinclair/typebox';

const UserSchema = Type.Object({
  id: Type.String({ format: 'uuid' }),
  name: Type.String(),
  email: Type.String({ format: 'email' }),
  avatar: Type.Optional(Type.String()),
  age: Type.Integer({ minimum: 0 }),
  tags: Type.Array(Type.String()),
});

type User = Static<typeof UserSchema>;
```

### Генерируемый код TypeBox

```typescript
// models/user/user.schema.ts
import { Type, Static } from '@sinclair/typebox';

export const UserSchema = Type.Object({
  id: Type.String({ format: 'uuid' }),
  name: Type.String(),
  email: Type.String({ format: 'email' }),
  avatar: Type.Optional(Type.String()),
  createdAt: Type.String({ format: 'date-time' }),
  age: Type.Integer({ minimum: 0 }),
  tags: Type.Array(Type.String()),
  metadata: Type.Record(Type.String(), Type.Unknown()),
});

export type User = Static<typeof UserSchema>;
```

### Отличия TypeBox: PascalCase схемы

TypeBox следует конвенции PascalCase для схем (в отличие от camelCase в Zod/Valibot), так как это Type-level объекты.

```csharp
// TypeBoxSchemaRenderer — именование схем
private string GetSchemaName(string modelName) => modelName;  // UserSchema (PascalCase)

// В отличие от Zod:
// private string GetSchemaName(string modelName) => ToCamelCase(modelName) + "Schema";  // userSchema
```

### TypeBoxSchemaRenderer

```csharp
public sealed class TypeBoxSchemaRenderer : IValidationSchemaRenderer
{
    public string ValidationLibraryId => "typebox";

    private string MapIrTypeToTypeBox(IrType irType) => irType switch
    {
        IrPrimitiveType pt => MapPrimitive(pt.Kind),
        IrArrayType arr      => $"Type.Array({MapIrTypeToTypeBox(arr.ElementType)})",
        IrDictionaryType d   => $"Type.Record({MapIrTypeToTypeBox(d.KeyType)}, {MapIrTypeToTypeBox(d.ValueType)})",
        IrNullableType n     => $"Type.Union([{MapIrTypeToTypeBox(n.InnerType)}, Type.Null()])",
        IrOptionalType o     => $"Type.Optional({MapIrTypeToTypeBox(o.InnerType)})",
        IrModelReference ref => $"{ref.ModelName}Schema",
        IrUnionType u        => $"Type.Union([{string.Join(", ", u.Types.Select(MapIrTypeToTypeBox))}])",
        _ => "Type.Unknown()"
    };

    private static string MapPrimitive(IrPrimitiveKind kind) => kind switch
    {
        IrPrimitiveKind.String   => "Type.String()",
        IrPrimitiveKind.Integer  => "Type.Integer()",
        IrPrimitiveKind.Float    => "Type.Number()",
        IrPrimitiveKind.Boolean  => "Type.Boolean()",
        IrPrimitiveKind.Date     => "Type.String({ format: 'date' })",
        IrPrimitiveKind.DateTime => "Type.String({ format: 'date-time' })",
        IrPrimitiveKind.Uuid     => "Type.String({ format: 'uuid' })",
        IrPrimitiveKind.Binary   => "Type.String({ contentEncoding: 'base64' })",
        _ => "Type.Unknown()"
    };

    public string GetImportStatement()
        => "import { Type, Static } from '@sinclair/typebox';";

    public string GetTypeInferExpression(string schemaName)
        => $"Static<typeof {schemaName}>";
}
```

### Enum в TypeBox

```typescript
// Zod:  export const statusSchema = z.enum(['active', 'inactive']);
// Valibot: export const statusSchema = v.picklist(['active', 'inactive']);
// TypeBox:
export const StatusSchema = Type.Union([
  Type.Literal('active'),
  Type.Literal('inactive'),
]);
export type Status = Static<typeof StatusSchema>;
```

---

## 5. Унифицированный ValidationAstBuilder

Все validation библиотеки генерируют схожую структуру файла:

```
[import statement]

[for each model dependency: import schema]

export const [schemaName] = [validation expression];

export type [ModelName] = [type inference expression];
```

Можно выделить общий шаблон файла:

```csharp
// ValIO.Generators.TypeScript/Validation/ValidationSchemaFileBuilder.cs

public sealed class ValidationSchemaFileBuilder
{
    private readonly IValidationSchemaRenderer _renderer;
    private readonly INamingConverter _namingConverter;

    public SourceFileNode BuildModelSchemaFile(
        IrModel model,
        string? importPath,
        IReadOnlyList<IrModel> allModels)
    {
        var imports = new List<ImportNode>();

        // 1. Добавить импорт библиотеки
        imports.Add(ParseImportStatement(_renderer.GetImportStatement()));

        // 2. Добавить импорты зависимых схем
        var deps = CollectModelDependencies(model, allModels);
        foreach (var dep in deps)
        {
            var schemaName = _renderer.GetSchemaName(dep.Name);
            imports.Add(new ImportNode(
                ImportKind.Named,
                [schemaName],
                $"../{dep.Name}/{dep.Name}.schema"));
        }

        // 3. Построить export const schemaName = renderer.RenderModel(model)
        var schemaVarDecl = BuildSchemaVariableDecl(model);

        // 4. Построить export type ModelName = TypeInfer<typeof schemaName>
        var typeAlias = BuildTypeAlias(model);

        return new SourceFileNode(imports, [schemaVarDecl, typeAlias]);
    }

    private AstNode BuildSchemaVariableDecl(IrModel model)
    {
        var schemaExpressionCode = _renderer.RenderModelExpression(model);

        // Это raw string expression (уже рендеренный validation код)
        // Оборачиваем в VariableDeclarationNode с raw value
        return new VariableDeclarationNode(
            VariableDeclarationKind.Const,
            new IdentifierNode(_renderer.GetSchemaName(model.Name)),
            isExported: true,
            initializer: new RawExpressionNode(schemaExpressionCode));
            // RawExpressionNode — нода для уже рендеренных выражений
    }
}
```

---

## 6. Архитектура: IValidationSchemaRenderer

```csharp
// ValIO.Core/Interfaces/IValidationSchemaRenderer.cs

/// <summary>
/// Контракт рендерера схем валидации.
/// Каждая библиотека (Zod3, Zod4, Valibot, TypeBox) реализует этот интерфейс.
/// Паттерн: Strategy — взаимозаменяемые рендереры.
/// </summary>
public interface IValidationSchemaRenderer
{
    /// <summary>
    /// Идентификатор библиотеки: "zod3", "zod4", "valibot", "typebox"
    /// </summary>
    string ValidationLibraryId { get; }

    /// <summary>
    /// Строка импорта библиотеки
    /// Example: "import { z } from 'zod';"
    /// </summary>
    string GetImportStatement();

    /// <summary>
    /// Имя переменной схемы для модели.
    /// Zod: "userSchema" | TypeBox: "UserSchema"
    /// </summary>
    string GetSchemaName(string modelName);

    /// <summary>
    /// Выражение для вывода типа из схемы.
    /// Zod: "z.infer<typeof userSchema>" | Valibot: "v.InferOutput<typeof userSchema>"
    /// </summary>
    string GetTypeInferExpression(string schemaName);

    /// <summary>
    /// Рендерит выражение схемы для модели (без export const ... =)
    /// </summary>
    string RenderModelExpression(IrModel model);

    /// <summary>
    /// Рендерит выражение схемы для enum
    /// </summary>
    string RenderEnumExpression(IrEnum irEnum);
}
```

### Регистрация рендереров через DI

```csharp
// Регистрация в плагинах
services.AddSingleton<IValidationSchemaRenderer, ZodV3SchemaRenderer>();
services.AddSingleton<IValidationSchemaRenderer, ZodV4SchemaRenderer>();
services.AddSingleton<IValidationSchemaRenderer, ValibotSchemaRenderer>();
services.AddSingleton<IValidationSchemaRenderer, TypeBoxSchemaRenderer>();

// Резолюция в Step'е
public sealed class ValidationSchemasStep : ICodeGenerationStep
{
    private readonly IEnumerable<IValidationSchemaRenderer> _renderers;

    public bool ShouldExecute(GenerationContext context)
    {
        var opts = (TypeScriptGenerationOptions)context.PluginOptions!;
        return opts.ValidationLibrary != null;
    }

    public async Task<IReadOnlyDictionary<string, string>> ExecuteAsync(
        GenerationContext context, CancellationToken ct = default)
    {
        var opts = (TypeScriptGenerationOptions)context.PluginOptions!;
        
        // Находим нужный рендерер
        var renderer = _renderers.FirstOrDefault(r => r.ValidationLibraryId == opts.ValidationLibrary)
            ?? throw new InvalidOperationException($"No renderer for {opts.ValidationLibrary}");

        var builder = new ValidationSchemaFileBuilder(renderer, _namingConverter);
        var astRenderer = new TypeScriptAstRenderer();
        var files = new Dictionary<string, string>();

        foreach (var model in context.IrDocument.Models)
        {
            ct.ThrowIfCancellationRequested();
            var sourceFile = builder.BuildModelSchemaFile(model, null, context.IrDocument.Models);
            var code = astRenderer.Render(sourceFile);
            var slug = ToKebabCase(model.Name);
            files[PathResolver.ResolveModelPath($"{slug}/{slug}.schema.ts", context.Configuration)] = code;
        }

        foreach (var irEnum in context.IrDocument.Enums)
        {
            ct.ThrowIfCancellationRequested();
            // ... аналогично для enum'ов
        }

        return files;
    }
}
```

---

## 7. Структура файлов

```
ValIO.Generators.TypeScript.Zod/     ← рефакторинг существующего
├── ZodGeneratorPlugin.cs
├── Zod/
│   ├── ZodV3SchemaRenderer.cs
│   ├── ZodV4SchemaRenderer.cs
│   └── BaseZodSchemaRenderer.cs
├── Ast/
│   ├── Nodes/                        ← Zod AST ноды (ZodObjectNode и т.д.)
│   ├── Builders/ZodAstBuilder.cs
│   └── Renderers/ZodNodeRenderer.cs  ← ITypeScriptNodeRenderer impl
└── Steps/
    ├── ZodV3SchemasStep.cs
    └── ZodV4SchemasStep.cs

ValIO.Generators.TypeScript.Valibot/  ← NEW
├── ValibotGeneratorPlugin.cs
├── ValibotSchemaRenderer.cs           ← IValidationSchemaRenderer
├── Steps/ValibotSchemasStep.cs
└── ValIO.Generators.TypeScript.Valibot.csproj

ValIO.Generators.TypeScript.TypeBox/  ← NEW
├── TypeBoxGeneratorPlugin.cs
├── TypeBoxSchemaRenderer.cs           ← IValidationSchemaRenderer
├── Steps/TypeBoxSchemasStep.cs
└── ValIO.Generators.TypeScript.TypeBox.csproj
```

### Конфигурация

```json
{
  "projects": {
    "api-zod3": {
      "language": "typescript",
      "schema": "./openapi.json",
      "output": "./src/api",
      "options": {
        "transports": ["fetch"],
        "validation": "zod3"
      }
    },
    "api-zod4": {
      "language": "typescript",
      "schema": "./openapi.json",
      "output": "./src/api",
      "options": {
        "transports": ["fetch"],
        "validation": "zod4"
      }
    },
    "api-valibot": {
      "language": "typescript",
      "schema": "./openapi.json",
      "output": "./src/api",
      "options": {
        "transports": ["ky"],
        "validation": "valibot"
      }
    },
    "api-typebox": {
      "language": "typescript",
      "schema": "./openapi.json",
      "output": "./src/api",
      "options": {
        "transports": ["fetch"],
        "validation": "typebox"
      }
    }
  }
}
```

---

## Итог

| Библиотека | ID | Import | Schema expr | Type infer |
|-----------|-----|--------|------------|------------|
| Zod v3 | `zod3` | `import { z } from 'zod'` | `z.object({...})` | `z.infer<typeof schema>` |
| Zod v4 | `zod4` | `import { z } from 'zod'` | `z.object({...})` | `z.infer<typeof schema>` |
| Valibot | `valibot` | `import * as v from 'valibot'` | `v.object({...})` | `v.InferOutput<typeof schema>` |
| TypeBox | `typebox` | `import { Type, Static } from '@sinclair/typebox'` | `Type.Object({...})` | `Static<typeof Schema>` |
