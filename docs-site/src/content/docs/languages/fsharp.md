---
title: F#
description: F# generator options and outputs.
sidebar:
  order: 20
---

## Overview

The F# plugin (`languageId: "fsharp"`, alias `fs`) emits idiomatic F# 7+ code
with records and discriminated unions, plus a
[RestSharp](https://restsharp.dev/)-based client. Response variants with
different status codes are surfaced as discriminated-union cases so callers
can pattern-match on them.

## What gets generated

- **Records** — one immutable `type` per schema with `[<JsonPropertyName>]`
  attributes preserving JSON casing.
- **Enums** — emitted either as discriminated unions of string-tagged cases or
  classic `enum` types depending on schema shape.
- **Response unions** — each operation gets a `*Response` discriminated union
  with one case per documented HTTP status.
- **HTTP client** — one `*Client` per tag built on top of RestSharp, with each
  operation exposed as a `Async<Response>` method.

## Options

| Option | Default | Description |
|--------|---------|-------------|
| `namespace` | `Generated` | Top-level F# namespace/module prefix for generated files. |

```json
{
  "language": "fsharp",
  "options": {
    "namespace": "Acme.PetStore"
  }
}
```

## File layout

```
<output>/
  Acme.PetStore/
    Models/Pet.fs
    Enums/Status.fs
    Client/PetsClient.fs
```

## Example

```bash
valio generate -s ./openapi.yaml -o ./out -l fsharp
```
