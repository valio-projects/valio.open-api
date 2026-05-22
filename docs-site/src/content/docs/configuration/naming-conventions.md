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
