---
title: Python
description: Python generator options and outputs.
sidebar:
  order: 14
---

## Overview

The Python plugin (`languageId: "python"`, aliases `py`, `py3`) targets
Python 3.10+ and produces Pydantic v2 models plus an `httpx`-based client.
Both `httpx.AsyncClient` (async) and `httpx.Client` (sync) flavours are
available — pick one with the `httpxMode` option.

## What gets generated

- **Models** — Pydantic v2 `BaseModel` per schema, with field aliases that
  preserve original JSON casing and `model_config = ConfigDict(populate_by_name=True)`.
- **Enums** — `enum.Enum` subclasses with string values.
- **HTTP client** — one `*Client` per tag built on `httpx`. Operations are
  either `async def` (httpxMode=async, the default) or plain `def`
  (httpxMode=sync).

## Options

| Option | Default | Description |
|--------|---------|-------------|
| `pythonVersion` | `3.11` | Target Python version (`3.10`–`3.13`). Required. |
| `httpxMode` | `async` | Generate `async` or `sync` httpx clients. Required. |
| `validator` | `pydantic` | Model validator. Pydantic v2 supported; `msgspec` reserved. |
| `rootModule` | `generated` | Top-level Python module name for the package. Required. |

```json
{
  "language": "python",
  "options": {
    "pythonVersion": "3.11",
    "httpxMode": "async",
    "validator": "pydantic",
    "rootModule": "petstore_client"
  }
}
```

## File layout

```
<output>/
  petstore_client/
    __init__.py
    models/pet.py
    models/__init__.py
    enums/status.py
    client/pets_client.py
```

## Example

```bash
valio generate -s ./openapi.yaml -o ./out -l python
```
