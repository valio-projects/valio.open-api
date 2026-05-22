---
title: TypeScript
description: Optionen und Ausgaben des TypeScript-Generators.
translationKey: lang-typescript
---

Das TypeScript-Plugin (`languageId: "typescript"`) ist der größte und
konfigurierbarste der mitgelieferten Generatoren.

## Was generiert wird

- **Modelle** — ein `interface` pro OpenAPI-Schema sowie diskriminierte
  Unions.
- **Enums** — String-Literal-Unions oder `enum`s, je nach
  `EnumFormat`.
- **Params / Results** — ein Typ pro Operation für `params` und
  `results`.
- **HTTP-Clients** — eine oder mehrere Varianten aus: `fetch`, `axios`,
  `ky`, `undici`, `ofetch`, `got`, `angular` (HttpClient), `solid`
  (SolidStart).
- **Validierungsschemas** — Zod (3 oder 4), Valibot oder TypeBox.
- **Query-Wrapper** — TanStack Query (React / Vue / Angular / Solid /
  Svelte) oder SWR.
- **Mocks** — Mock-Service-Worker-Handler.
- **Index-Dateien** — ein einheitlicher Re-Export-Baum.

## Optionen

```json
{
  "language": "typescript",
  "options": {
    "transports":         ["fetch", "axios"],
    "validationLibrary":  "zod",
    "queryWrapper":       "tanstack",
    "mockLibrary":        "msw",
    "enumFormat":         "string-union"
  }
}
```

## Dateilayout

Standardmäßig landen Modelle unter `models/<name>/<name>.ts`, Clients
unter `api/<operation>/<operation>.<transport>.ts`. Überschreibbar via
`fileOrganization.modelsPath` und `fileOrganization.clientsPath`.
