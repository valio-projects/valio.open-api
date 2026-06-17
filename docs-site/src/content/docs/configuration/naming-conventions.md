---
title: Naming conventions
description: How identifiers are cased per identifier kind.
---

`namingConvention` overrides the default casing rule for each kind of
identifier. The four kinds are independent, so you can mix and match
(e.g. PascalCase models with snake_case methods).

## Keys

```json
{
  "namingConvention": {
    "models":     "PascalCase",
    "properties": "CamelCase",
    "methods":    "CamelCase",
    "enums":      "PascalCase"
  }
}
```

| Key          | Applies to                                  |
|--------------|---------------------------------------------|
| `models`     | Class, interface, record, struct names.     |
| `properties` | Fields / properties inside models.          |
| `methods`    | HTTP client method names.                   |
| `enums`      | Enum type names and enum member names.      |

## Allowed values

`NamingConvention` is an enum in `ValIO.Core.Models`:

| Value         | Example output      |
|---------------|---------------------|
| `PascalCase`  | `UserProfile`       |
| `CamelCase`   | `userProfile`       |
| `SnakeCase`   | `user_profile`      |
| `KebabCase`   | `user-profile`      |

Spelling is forgiving: `pascal-case`, `pascalcase`, `snake_case` and
`snake-case` are all accepted.

## Defaults

If `namingConvention` is omitted the language plugin picks idiomatic
defaults:

| Language     | Models       | Properties   | Methods      | Enums        |
|--------------|--------------|--------------|--------------|--------------|
| `typescript` | PascalCase   | camelCase    | camelCase    | PascalCase   |
| `csharp`     | PascalCase   | PascalCase   | PascalCase   | PascalCase   |
| `java`       | PascalCase   | camelCase    | camelCase    | PascalCase   |
| `kotlin`     | PascalCase   | camelCase    | camelCase    | PascalCase   |
| `python`     | PascalCase   | snake_case   | snake_case   | PascalCase   |
| `php`        | PascalCase   | camelCase    | camelCase    | PascalCase   |
| `swift`      | PascalCase   | camelCase    | camelCase    | PascalCase   |
| `dart`       | PascalCase   | camelCase    | camelCase    | PascalCase   |
| `fsharp`     | PascalCase   | PascalCase   | PascalCase   | PascalCase   |
| `ruby`       | PascalCase   | snake_case   | snake_case   | PascalCase   |
| `rust`       | PascalCase   | snake_case   | snake_case   | PascalCase   |

These match the host language style guides (PEP 8 for Python, Rust
formatting rules, etc.). Override only when you need to interoperate
with an existing codebase that uses different conventions.

## Prefix / suffix (affixes)

Beyond casing, you can wrap generated identifiers with an optional
prefix and/or suffix per identifier kind. This is the recommended way to
resolve clashes between a DTO type and an operation function that share a
name (e.g. an OpenAPI schema `CreateJournalSchema` and an `operationId`
`CreateJournalSchema`), which would otherwise produce an ambiguous
re-export (TypeScript **TS2308**) in the barrel.

```json
{
  "namingConvention": {
    "models":      "PascalCase",
    "methods":     "PascalCase",
    "enums":       "PascalCase",

    "modelPrefix":  "",
    "modelSuffix":  "Model",
    "methodPrefix": "",
    "methodSuffix": "",
    "enumPrefix":   "",
    "enumSuffix":   ""
  }
}
```

| Key            | Wraps                                                              |
|----------------|-------------------------------------------------------------------|
| `modelPrefix`  / `modelSuffix`  | Model (DTO) type names — and everything derived from them: type references, validation-schema constants, etc. |
| `methodPrefix` / `methodSuffix` | Operation/client function names (across every transport).        |
| `enumPrefix`   / `enumSuffix`   | Enum type names.                                                  |

The example above renders the schema as `CreateJournalSchemaModel` while
the client function stays `CreateJournalSchema` — distinct identifiers,
no collision. Prefix and suffix compose: `modelPrefix: "Api"` +
`modelSuffix: "Dto"` on `User` → `ApiUserDto`.

Affixes are applied once, at the language-agnostic IR layer, so they
propagate consistently to **every** target language, transport and
reference site — type references, imports, validation schemas, query
keys and the barrel — automatically.

**File names and import paths are *not* affixed.** Only the emitted
identifiers change; `models/createJournalSchema/createJournalSchema.ts`
keeps its path (and your existing deep-path imports keep working) while
the interface inside it is now `CreateJournalSchemaModel`.

Defaults are empty (no affix), so omitting these keys preserves the
current output exactly.

### Result/response-union type (TypeScript)

The per-operation result-union type has its own affix, configured in the
TypeScript generator **options** (it is a TS-specific synthesized type):

```json
{
  "options": {
    "resultTypeSuffix": "Results",
    "resultTypePrefix": ""
  }
}
```

`resultTypeSuffix` (default `Results`) and `resultTypePrefix` (default
empty) wrap the union alias, e.g. `getUser` → `ApiGetUserResults`.

## Reserved-keyword handling

The naming converter (`INamingConverter`) also sanitizes reserved
words. For example, an OpenAPI field called `class` becomes
`@class` in C# and `class_` in Python automatically — there is no
configuration knob for this; each plugin embeds its own keyword list.

## Example: snake_case Python with camelCase properties

```json
{
  "namingConvention": {
    "models":     "PascalCase",
    "properties": "CamelCase",
    "methods":    "SnakeCase",
    "enums":      "PascalCase"
  }
}
```

Useful when serving an API where the wire format keeps `camelCase`
keys but Python idiom wants `snake_case` callable methods.
