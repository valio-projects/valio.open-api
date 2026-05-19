---
title: Configuration file
description: Multi-project valio.config.json reference.
---

A config file lets you describe several generation targets at once
(e.g. one TypeScript app and one C# server) and run them all from a
single `valio generate -c valio.config.json` invocation.

## Format

```json
{
  "projects": {
    "web": {
      "language":       "typescript",
      "schema":         "./openapi.json",
      "outputPath":     "./apps/web/src/api",
      "namespace":      "@my-org/api",
      "generateModels": true,
      "generateClients": true,
      "options": {
        "transports":       ["fetch", "axios"],
        "validationLibrary": "zod",
        "queryWrapper":      "tanstack"
      }
    },
    "server": {
      "language":   "csharp",
      "schema":     "./openapi.json",
      "outputPath": "./MyOrg.Api.Client/Generated",
      "namespace":  "MyOrg.Api.Client"
    }
  }
}
```

## Fields

| Field             | Type                | Notes |
|-------------------|---------------------|-------|
| `language`        | string              | Generator id, e.g. `typescript`, `csharp`. |
| `schema`          | string              | Path or URL. |
| `outputPath`      | string              | Folder or `.zip` path. |
| `namespace`       | string              | C#/Java/Kotlin/PHP namespace, or TS module prefix. |
| `generateModels`  | bool (default true) | |
| `generateClients` | bool (default true) | |
| `generateEnums`   | bool (default true) | |
| `fileOrganization`| object              | Override per-target file structure. |
| `namingConvention`| object              | Override casing rules per identifier kind. |
| `options`         | object              | Plugin-specific options. See [Languages](/languages/). |

## Running

```bash
# All projects
valio generate -c ./valio.config.json

# Specific projects
valio generate -c ./valio.config.json --projects web,server
```

YAML is also accepted (`valio.config.yaml`).
