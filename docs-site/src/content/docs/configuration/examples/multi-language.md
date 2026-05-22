---
title: 'Example: multi-language fan-out'
description: One schema, four languages — web, mobile, server, and SDK.
---

When you ship a public API, one OpenAPI document typically powers
several first-party clients. This example fans out a single schema
into TypeScript, Kotlin (Android), Swift (iOS), and Python (internal
tooling) in a single command.

## `valio.config.json`

```json
{
  "projects": {
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
    "android": {
      "language":   "kotlin",
      "schema":     "./openapi.json",
      "outputPath": "./mobile/android/api/src/main/kotlin/com/example/api",
      "namespace":  "com.example.api",
      "options": {
        "useCoroutines": true
      }
    },
    "ios": {
      "language":   "swift",
      "schema":     "./openapi.json",
      "outputPath": "./mobile/ios/ApiClient/Sources/ApiClient",
      "options": {
        "useAsyncAwait": true
      }
    },
    "tools": {
      "language":   "python",
      "schema":     "./openapi.json",
      "outputPath": "./tools/api_client",
      "options": {
        "useAsync":  true,
        "useTyping": true
      }
    }
  }
}
```

## Running

```bash
# Full fan-out (CI runs this on every API release)
valio generate -c ./valio.config.json

# A subset (during local development)
valio generate -c ./valio.config.json -p web,tools
```

If `android` fails (say, an unsupported `format`), the run logs the
error and continues with `ios` and `tools`. The CLI still exits with
`1` overall, which keeps CI honest.

## Why one config?

- A single source of truth for the schema URL or path.
- Lockstep regeneration — no risk of one client lagging behind the
  others when the API changes.
- Naturally splits into named tasks for CI (`valio generate -p web`,
  `valio generate -p android`, …) when you want per-platform jobs
  while keeping the config DRY.

## Suggested CI layout

```yaml
jobs:
  - id: generate
    matrix: [web, android, ios, tools]
    run: valio generate -c ./valio.config.json -p ${{ matrix.target }}
```

See [Recipes / CI integration](/recipes/ci-integration/) for a fully
worked GitHub Actions example.
