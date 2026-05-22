---
title: Swift
description: Swift generator options and outputs.
sidebar:
  order: 17
---

## Overview

The Swift plugin (`languageId: "swift"`) targets Swift 5.7+ and produces
`Codable` structs plus a URLSession-based async/await client. No third-party
dependencies are required; everything compiles against the standard library
and Foundation.

## What gets generated

- **Structs** — one `Codable` `struct` per OpenAPI schema, with `CodingKeys`
  preserving original JSON casing.
- **Enums** — Swift `enum` with `String` raw values and `Codable` conformance.
- **HTTP client** — one `*Client` `final class` per tag, wrapping `URLSession`.
  Each operation is `async throws` returning the typed response.
- **AnyCodable wrapper** — emitted when `anyMapping = "anycodable"` for IR
  `Any` types.
- **Package.swift** — optional, when `generateBuildFiles` is true.

## Options

| Option | Default | Description |
|--------|---------|-------------|
| `moduleName` | `Generated` | Output module/folder name. Required. |
| `anyMapping` | `anycodable` | How IR `Any` maps in Swift: `anycodable` (wrapper) or `string` (lossy). |
| `generateBuildFiles` | `false` | Emit a `Package.swift` alongside sources. |

```json
{
  "language": "swift",
  "options": {
    "moduleName": "PetStoreAPI",
    "anyMapping": "anycodable",
    "generateBuildFiles": true
  }
}
```

## File layout

```
<output>/
  PetStoreAPI/
    Models/Pet.swift
    Enums/Status.swift
    Client/PetsClient.swift
    Support/AnyCodable.swift   (when anyMapping=anycodable)
  Package.swift                (when generateBuildFiles=true)
```

## Example

```bash
valio generate -s ./openapi.yaml -o ./out -l swift
```
