# ValIO.Generators.TypeScript

The **TypeScript** generator plugin (`LanguageId = "typescript"`, aliases `ts`) — the most
feature-complete generator. Turns the IR into TypeScript: models/interfaces, string-literal-union
enums (with values array + type guard), per-operation `params` and discriminated-union `results`,
HTTP clients, validation schemas, query-wrapper hooks, and MSW mocks. Every file is JSDoc-annotated.

## Features

- **Transports:** Fetch, Axios, Ky, Got, OFetch, Undici, Angular HttpClient, SolidStart.
- **Validation:** Zod 3/4, Valibot, TypeBox.
- **Query wrappers:** TanStack Query (React / Vue / Angular / Solid / Svelte), SWR.
- **Mocks:** Mock Service Worker handlers.
- **Options:** `transports`, `validation`, `queryWrapper`, `mocks`, `enumFormat`, `resultTypeSuffix`,
  `uuidFormat`, custom axios instance — see `Options/` and `docs-site/.../configuration/options-reference.md`.

## Layout

- `TypeScriptGeneratorPlugin.cs` — plugin entry point; builds the pipeline and node-renderer chain.
- `Steps/` — pipeline steps (`ICodeGenerationStep`), one per artifact kind.
- `Ast/` — TS AST nodes, builders (IR→AST), renderers (AST→source), and the JSDoc helper.
- `Zod/`, `Valibot/`, `TypeBox/`, `TanStackQuery/`, `Axios/`, `Ky/`, `Got/`, `OFetch/`, `Undici/`,
  `Angular/`, `Solid/`, `Msw/` — per-library/transport builders & renderers.
- `Options/` — option ids, typed options and the options descriptor.

See the [Developer Guide](../../docs/DEVELOPER_GUIDE.md) — §10b–10d explain how to add a transport,
validation library or query wrapper here.
