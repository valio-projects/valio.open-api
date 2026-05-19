---
title: Web UI
description: Running ValIO.Web in a browser.
---

`ValIO.Web` is an ASP.NET Core 10 + Blazor Server app. It exposes the same
generator pipeline as the CLI, but driven from a form in your browser.

## Running locally

```bash
dotnet run --project src/ValIO.Web
# http://localhost:5080
```

## Features

- **Schema input** — paste a URL or upload a `.json` / `.yaml` file (max 10 MB).
- **Language picker** — every plugin registered through `AddAllGeneratorPlugins`
  appears in the dropdown.
- **Dynamic plugin options form** — controls are built from each plugin's
  `OptionDescription` list. Bool → switch, EnumSingle → dropdown,
  EnumMulti → chip set, etc.
- **Output as ZIP** — the server streams a zip archive built in-memory
  via `ZipStreamOutputSink`; the browser downloads it via a blob URL.
- **Language switcher** — top-right menu sets a cookie and reloads with
  the chosen locale (`en`, `ru`, `de`, `zh-CN`).

## API endpoint

The form posts to `POST /api/generate` with JSON:

```json
{
  "schemaUrl":            "https://example.com/openapi.json",
  "schemaFileName":       null,
  "schemaContentBase64":  null,
  "languageId":           "typescript",
  "namespace":            null,
  "generateModels":       true,
  "generateClients":      true,
  "generateEnums":        true,
  "pluginOptions":        { "transports": ["fetch","axios"] }
}
```

Either `schemaUrl` **or** `schemaContentBase64` must be set, not both.
Response is `application/zip` with `Content-Disposition: attachment`.

## Security

- URL fetcher refuses non-`http(s)` URLs and any host resolving to
  loopback, link-local, or RFC1918 private ranges.
- Upload size capped at 10 MB.
- All requests run inside the request scope; no cross-circuit state.
