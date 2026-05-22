---
title: File organization
description: How generated files are laid out on disk.
---

`fileOrganization` controls where each generator puts its files inside
your `outputPath`. The same shape applies to every language; plugins
fill in sensible defaults when fields are omitted.

## Keys

```json
{
  "fileOrganization": {
    "groupingStrategy": "SingleFile",
    "separateModels":   true,
    "separateClients":  true,
    "modelsPath":       "models",
    "clientsPath":      "api"
  }
}
```

| Key                | Type    | Default        | Effect |
|--------------------|---------|----------------|--------|
| `groupingStrategy` | enum    | `SingleFile`   | `SingleFile`, `ByTag`, `ByRoute`. |
| `separateModels`   | bool    | `true`         | Put models in their own subfolder. |
| `separateClients`  | bool    | `true`         | Put clients in their own subfolder. |
| `modelsPath`       | string  | language-spec. | Subfolder name for models. |
| `clientsPath`      | string  | language-spec. | Subfolder name for clients. |

`null` for `modelsPath` / `clientsPath` means *use the plugin default*
(see below).

## Grouping strategy

| Strategy     | Output |
|--------------|--------|
| `SingleFile` | One file per type. `Pet` → `models/Pet/Pet.ts`. |
| `ByTag`      | All operations under the same OpenAPI tag share one client file. |
| `ByRoute`    | One client file per route path. |

`ByTag` is the most common choice for client folders when an API
exposes many operations grouped under a handful of tags.

## Per-language defaults

| Language     | Default `modelsPath` | Default `clientsPath` |
|--------------|----------------------|------------------------|
| `typescript` | `models`             | `api`                  |
| `csharp`     | `Models`             | `Clients`              |
| `java`       | `models`             | `clients`              |
| `kotlin`     | `models`             | `clients`              |
| `python`     | `models`             | `clients`              |
| `php`        | `Models`             | `Clients`              |
| `swift`      | `Models`             | `Clients`              |
| `dart`       | `models`             | `api`                  |
| `fsharp`     | `Models`             | `Clients`              |
| `ruby`       | `models`             | `clients`              |
| `rust`       | `models`             | `clients`              |

Casing matches the host language's filesystem convention (PascalCase
folders for .NET, lowercase for JS/Python/Ruby/Rust).

## Example: flat layout

```json
{
  "fileOrganization": {
    "separateModels":  false,
    "separateClients": false
  }
}
```

Every generated file lands directly under `outputPath`, no
subdirectories.

## Example: custom paths

```json
{
  "fileOrganization": {
    "modelsPath":  "schemas",
    "clientsPath": "endpoints"
  }
}
```

Useful when integrating into an existing repo where conventions don't
match the plugin default.
