---
title: Angular HttpClient in an Nx monorepo
description: Generate Angular services + Zod schemas into a shared workspace library.
---

The TypeScript plugin emits Angular `HttpClient` services (RxJS or
Promise based) plus matching Zod schemas. This recipe drops them into
a shared Nx library so every Angular app in the workspace consumes the
same types.

## Layout

```
my-workspace/
├── nx.json
├── openapi.json
├── valio.config.json
└── libs/
    └── api/
        ├── project.json
        └── src/
            └── lib/
                ├── models/         ← Zod schemas + inferred types
                └── endpoints/      ← Angular services, one per tag
```

## `valio.config.json`

```json
{
  "projects": {
    "api": {
      "language":   "typescript",
      "schema":     "./openapi.json",
      "outputPath": "./libs/api/src/lib",
      "options": {
        "transports":        ["angular-rxjs"],
        "validationLibrary": "zod",
        "queryWrapper":      "tanstack",
        "tanStackFramework": "angular",
        "enumFormat":        "string-union"
      },
      "fileOrganization": {
        "groupingStrategy": "ByTag",
        "modelsPath":       "models",
        "clientsPath":      "endpoints"
      }
    }
  }
}
```

Pick `angular-promise` instead of `angular-rxjs` if your app already
migrated off Observables.

## Nx target

Add to `libs/api/project.json`:

```json
{
  "targets": {
    "codegen": {
      "executor": "nx:run-commands",
      "options": {
        "command": "valio generate -c valio.config.json -p api",
        "cwd": "{workspaceRoot}"
      },
      "inputs":  ["{workspaceRoot}/openapi.json", "{workspaceRoot}/valio.config.json"],
      "outputs": ["{projectRoot}/src/lib/**"]
    }
  }
}
```

Then:

```bash
nx run api:codegen
# Cached: re-runs only when openapi.json or the config changes.
```

## Consuming the output

```ts
// apps/portal/src/app/users-page.component.ts
import { UsersService } from '@my-workspace/api';
import { injectQuery } from '@tanstack/angular-query-experimental';

export class UsersPageComponent {
  private users = inject(UsersService);

  query = injectQuery(() => ({
    queryKey: ['users'],
    queryFn: () => firstValueFrom(this.users.list()),
  }));
}
```

Models and Zod schemas are re-exported through `libs/api/src/index.ts`
— so `import { User, UserSchema } from '@my-workspace/api'` works
across every app.

## CI

Wire the target into the `build` task pipeline so codegen always runs
before the app builds:

```json
{
  "targetDefaults": {
    "build": { "dependsOn": ["^codegen"] }
  }
}
```

Or run it explicitly as a CI step:

```bash
nx run-many --target=codegen
git diff --exit-code libs/api/src
```

The `--exit-code` check fails the build if developers forgot to commit
regenerated files.
