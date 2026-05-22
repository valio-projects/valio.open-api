---
title: 'Example: web + server from one schema'
description: One OpenAPI schema, a C# backend client, and a TypeScript frontend.
---

A common setup: one OpenAPI document is the source of truth, and you
want both a strongly-typed C# client (for the server-side BFF) and a
TypeScript fetch/Zod client (for the SPA). Two projects, one config.

## Repo layout

```
my-app/
├── openapi.json
├── valio.config.json
├── apps/
│   └── web/
│       └── src/
│           └── api/          ← TS output lands here
└── server/
    └── MyOrg.Api.Client/
        └── Generated/        ← C# output lands here
```

## `valio.config.json`

```json
{
  "projects": {
    "web": {
      "language":   "typescript",
      "schema":     "./openapi.json",
      "outputPath": "./apps/web/src/api",
      "options": {
        "transports":         ["fetch"],
        "validationLibrary":  "zod",
        "queryWrapper":       "tanstack",
        "tanStackFramework":  "react"
      },
      "fileOrganization": {
        "groupingStrategy": "ByTag",
        "modelsPath":       "models",
        "clientsPath":      "endpoints"
      }
    },
    "server": {
      "language":   "csharp",
      "schema":     "./openapi.json",
      "outputPath": "./server/MyOrg.Api.Client/Generated",
      "namespace":  "MyOrg.Api.Client",
      "options": {
        "csharp": {
          "modelType":                   "Record",
          "propertyAccessor":            "Init",
          "useRequired":                 true,
          "useSealed":                   true,
          "generateValidationAttributes":true,
          "queryParametersStyle":        "ModelClass"
        }
      }
    }
  }
}
```

## Running

```bash
# Both
valio generate -c ./valio.config.json

# Just the frontend (during SPA dev)
valio generate -c ./valio.config.json -p web

# Just the server, after the C# DTOs drift
valio generate -c ./valio.config.json -p server
```

## What you get

- `apps/web/src/api/models/*.ts` — Zod schemas + inferred TS types.
- `apps/web/src/api/endpoints/<tag>.ts` — one TanStack Query hook
  module per OpenAPI tag.
- `server/MyOrg.Api.Client/Generated/Models/*.cs` — `record` DTOs with
  `[Required]` / `[MinLength]` validation attributes.
- `server/MyOrg.Api.Client/Generated/Clients/*.cs` — async sealed
  classes implementing the same operations.

Both halves are wire-compatible because they share the schema; CI
typically runs `valio generate -c ./valio.config.json` plus a
`git diff --exit-code` check to fail builds when the OpenAPI changes
without regenerated code being committed.
