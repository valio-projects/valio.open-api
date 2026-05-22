---
title: Migrating from NSwag
description: Mapping NSwag config keys and CLI flags to ValIO.
---

NSwag and ValIO solve overlapping problems — generating clients from
OpenAPI — but the shapes of the configs are different. This page walks
through the most common translation points.

## CLI invocations

| NSwag                                                | ValIO                                                                 |
|------------------------------------------------------|-----------------------------------------------------------------------|
| `nswag run nswag.json`                               | `valio generate -c valio.config.json`                                 |
| `nswag openapi2csclient /input:o.json /output:c.cs`  | `valio generate -s o.json -o ./out -l csharp`                         |
| `nswag openapi2tsclient /input:o.json /output:c.ts`  | `valio generate -s o.json -o ./out -l typescript`                     |
| `nswag list-types`                                   | `valio validate -s o.json` (prints title/version/paths count)         |

## Config keys (C#)

| NSwag (`csharpClient` settings)      | ValIO (`options.csharp.*`)                | Notes |
|--------------------------------------|--------------------------------------------|-------|
| `namespace`                          | top-level `namespace` on the project       | C# namespace.        |
| `className`                          | n/a                                        | ValIO names clients after the OpenAPI tag.  |
| `useBaseUrl`                         | n/a                                        | The generated client always takes a base URL constructor arg. |
| `generateClientInterfaces`           | `generateInterfaces`                       | Bool. |
| `injectHttpClient`                   | n/a                                        | The C# client takes `HttpClient` via constructor. |
| `generateOptionalParameters`         | n/a                                        | Mirrors OpenAPI `required`. |
| `useBaseUrl` + `baseUrlTokenName`    | n/a                                        | Set the base URL when constructing the client. |
| `classStyle: "Record"`               | `modelType: "Record"`                      | Also accepts `Class`, `Struct`, `RecordStruct`. |
| `wrapResponses`                      | n/a                                        | Use `HttpResponseMessage` directly if you need full response. |
| `excludedTypeNames`                  | n/a                                        | Filter the schema before passing to ValIO. |

## Config keys (TypeScript)

| NSwag (`typeScriptClient` settings)  | ValIO (`options.*`)                        | Notes |
|--------------------------------------|--------------------------------------------|-------|
| `template`: `Fetch` / `Axios`        | `transports: ["fetch"]` / `["axios"]`       | Pick one or more. |
| `typeStyle`: `Interface` / `Class`   | n/a                                        | ValIO emits interfaces by default. |
| `useTransformOptionsMethod`          | `options.typescript.axios.customInstancePath` (for axios) / `options.typescript.fetch.customFetcherPath` | Plug a custom fetcher/axios instance. |
| `clientBaseClass`                    | n/a                                        | No base class — composition via custom fetcher. |
| `nullValue`: `Null` / `Undefined`    | `useStrictNullChecks`                       | |
| `enumStyle`: `Enum` / `StringLiteral`| `enumFormat`: `"Enum"` / `"UnionType"` (a.k.a. `string-union`) | |
| n/a                                  | `validationLibrary: "zod"` / `"valibot"` / `"typebox"` | NSwag has no schema validation output. |
| n/a                                  | `queryWrapper: "tanstack"` / `"swr"`        | NSwag has no React/Vue wrapper output. |
| n/a                                  | `mockLibrary: "msw"`                        | NSwag has no MSW handler output. |

## File organization

| NSwag                                | ValIO                                                |
|--------------------------------------|------------------------------------------------------|
| One output file (`output: "api.ts"`) | `fileOrganization.groupingStrategy: "SingleFile"` + folder output |
| One file per controller              | `fileOrganization.groupingStrategy: "ByTag"`         |
| Custom file naming                   | `fileOrganization.modelsPath` / `clientsPath` overrides |

## Things ValIO does not currently emit

If you rely on these, keep NSwag around for them:

- C# server stubs / ASP.NET controllers (NSwag's `openapi2cscontroller`).
- TypeScript Angular `HttpClient` services with NSwag-style `Observable<>` wrappers? — supported. With NSwag's specific runtime `ApiException` class? — different shape (ValIO uses idiomatic errors per transport).
- Swagger UI hosting — ValIO is generator-only.

## Suggested migration order

1. Run ValIO alongside NSwag into a separate folder. Compare outputs.
2. Switch consumers to the ValIO output one feature at a time
   (typically by tag).
3. Remove the NSwag step and check the `nswag.json` file out of the
   repo last.
