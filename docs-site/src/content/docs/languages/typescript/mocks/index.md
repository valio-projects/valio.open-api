---
title: TypeScript mocks
description: Mock handler generation for the TypeScript generator.
sidebar:
  order: 1
---

The TypeScript plugin can emit mock request handlers next to the generated
clients so the same OpenAPI schema drives both production code **and** the
mocks used in tests, Storybook stories, or local development.

| Library id | Library | What gets emitted |
|------------|---------|---------------------|
| `msw`      | [Mock Service Worker](https://mswjs.io) | `mocks/handlers.ts` with one `http.{verb}(...)` handler per operation. |

Only MSW is supported today; the option accepts either the
`mocks` or `mockLibrary` config key for symmetry with `validation` /
`validationLibrary`.

## How to enable

```json
{
  "language": "typescript",
  "options": {
    "transports": ["fetch"],
    "mocks":      "msw"
  }
}
```

The output sits at a fixed path (`mocks/handlers.ts`) — independent of
`clientsPath` and `modelsPath` — so handlers can be wired into both test and
dev setups without circular imports.
