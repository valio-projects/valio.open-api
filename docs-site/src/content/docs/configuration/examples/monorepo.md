---
title: 'Example: ValIO in a Nx / Turborepo monorepo'
description: Multi-package generation that lines up with workspace boundaries.
---

In a Nx or Turborepo workspace, each generated client typically lives
in its own package so it can be versioned and consumed independently.
ValIO works well here because each project in the config can target a
different workspace path.

## Layout

```
my-monorepo/
├── nx.json (or turbo.json)
├── package.json
├── valio.config.json
├── packages/
│   ├── api-types/                 ← Pure TS interfaces, no transport
│   │   └── src/
│   ├── api-web/                   ← Fetch + TanStack Query
│   │   └── src/
│   └── api-node/                  ← Undici, server-side use
│       └── src/
└── apps/
    └── api-mock/                  ← MSW handlers for Storybook
        └── src/
```

## `valio.config.json`

```json
{
  "projects": {
    "types": {
      "language":   "typescript",
      "schema":     "./openapi.json",
      "outputPath": "./packages/api-types/src",
      "generateModels":  true,
      "generateClients": false,
      "options": {
        "transports":  [],
        "enumFormat":  "string-union"
      }
    },
    "web": {
      "language":   "typescript",
      "schema":     "./openapi.json",
      "outputPath": "./packages/api-web/src",
      "options": {
        "transports":        ["fetch"],
        "validationLibrary": "zod",
        "queryWrapper":      "tanstack",
        "tanStackFramework": "react"
      }
    },
    "node": {
      "language":   "typescript",
      "schema":     "./openapi.json",
      "outputPath": "./packages/api-node/src",
      "options": {
        "transports":        ["undici"],
        "validationLibrary": "zod"
      }
    },
    "mocks": {
      "language":   "typescript",
      "schema":     "./openapi.json",
      "outputPath": "./apps/api-mock/src",
      "generateClients": false,
      "options": {
        "mockLibrary": "msw"
      }
    }
  }
}
```

## Nx wiring

Add a `codegen` target to the root `project.json` (or per-package
`project.json`s for finer cache keys):

```json
{
  "targets": {
    "codegen": {
      "executor": "nx:run-commands",
      "options": {
        "command": "valio generate -c ./valio.config.json"
      },
      "inputs": ["{workspaceRoot}/openapi.json", "{workspaceRoot}/valio.config.json"],
      "outputs": [
        "{workspaceRoot}/packages/api-types/src",
        "{workspaceRoot}/packages/api-web/src",
        "{workspaceRoot}/packages/api-node/src",
        "{workspaceRoot}/apps/api-mock/src"
      ]
    }
  }
}
```

Now `nx codegen` is fully cached: rerunning without an OpenAPI change
is a no-op.

## Turborepo wiring

```json
{
  "tasks": {
    "codegen": {
      "inputs": ["openapi.json", "valio.config.json"],
      "outputs": [
        "packages/api-types/src/**",
        "packages/api-web/src/**",
        "packages/api-node/src/**",
        "apps/api-mock/src/**"
      ]
    }
  }
}
```

`turbo run codegen` then runs `valio generate -c ./valio.config.json`
from each package script and caches the result keyed on the schema.

## Tips

- Add `packages/**/src/**` to `.gitignore` for generated folders if
  you'd rather not commit machine-output. Most teams *do* commit the
  generated code to make PR review and IDE jump-to-definition work.
- Pin the ValIO version in `package.json` (or `global.json` for
  .NET tool installs) so every contributor produces byte-identical
  output.
