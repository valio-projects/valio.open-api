---
title: Ruby
description: Ruby generator options and outputs.
sidebar:
  order: 19
---

## Overview

The Ruby plugin (`languageId: "ruby"`) emits plain Ruby classes plus a
[Faraday](https://lostisland.github.io/faraday/)-based HTTP client. Ruby is
dynamically typed, so the surface area is intentionally small — there are no
schema-validation libraries to wire up.

## What gets generated

- **Model classes** — one Ruby class per OpenAPI schema, with attribute
  readers/writers and a `from_hash` / `to_hash` pair for JSON round-tripping.
- **Enums** — emitted as plain Ruby modules with constants per allowed value
  (Ruby has no native enum).
- **HTTP client** — one `Client` class per tag, wrapping a `Faraday::Connection`.
  Each operation is a regular method returning a parsed model instance.

## Options

| Option | Default | Description |
|--------|---------|-------------|
| `moduleName` | `generated` | Output module/folder name for generated Ruby files. |

```json
{
  "language": "ruby",
  "options": {
    "moduleName": "PetStore"
  }
}
```

## File layout

```
<output>/
  pet_store/
    models/pet.rb
    enums/status.rb
    client/pets_client.rb
```

## Example

```bash
valio generate -s ./openapi.yaml -o ./out -l ruby
```
