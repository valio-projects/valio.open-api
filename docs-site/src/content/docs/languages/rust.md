---
title: Rust
description: Rust generator options and outputs.
sidebar:
  order: 18
---

## Overview

The Rust plugin (`languageId: "rust"`, alias `rs`) emits idiomatic Rust 2021
edition code using `serde` for serialization and `reqwest` for HTTP. The
generated client is async and assumes a Tokio runtime.

## What gets generated

- **Structs** — one `#[derive(Serialize, Deserialize)] struct` per schema,
  with `#[serde(rename = "...")]` preserving original JSON casing.
- **Enums** — Rust `enum` deriving `Serialize`/`Deserialize`, with
  `#[serde(rename_all = "snake_case")]` (or per-variant `rename`).
- **HTTP client** — one `*Client` struct per tag, wrapping `reqwest::Client`.
  Each operation is `async fn` returning `Result<TResp, Error>`.
- **Cargo.toml** — emitted when build-file generation is enabled.

## Options

| Option | Default | Description |
|--------|---------|-------------|
| `crateName` | `generated` | Output crate/folder name for generated files. |
| `anyMapping` | `json-value` | How IR `Any` maps in Rust: `json-value` (`serde_json::Value`) or `string`. |

```json
{
  "language": "rust",
  "options": {
    "crateName": "petstore_client",
    "anyMapping": "json-value"
  }
}
```

## File layout

```
<output>/
  petstore_client/
    src/
      lib.rs
      models/pet.rs
      models/mod.rs
      enums/status.rs
      client/pets_client.rs
    Cargo.toml
```

## Example

```bash
valio generate -s ./openapi.yaml -o ./out -l rust
```
