---
title: Dart
description: Dart generator options and outputs.
sidebar:
  order: 21
---

## Overview

The Dart plugin (`languageId: "dart"`) emits null-safe Dart 3 code suitable
for Flutter or pure-Dart projects. Models are plain Dart classes with
hand-rolled `fromJson` / `toJson` methods. The default HTTP client is built
on top of [`dio`](https://pub.dev/packages/dio); the project can be retargeted
to `package:http` by editing the generated client.

## What gets generated

- **Classes** — one Dart `class` per schema with final fields, a const
  constructor, and `fromJson` / `toJson` methods that preserve original JSON
  property casing.
- **Enums** — Dart `enum` with `String` values and `fromJson` / `toJson`
  helpers.
- **HTTP client** — one `*Client` class per tag wrapping a `Dio` instance,
  each operation a `Future<TResp>`.

## Options

| Option | Default | Description |
|--------|---------|-------------|
| `packageName` | `generated` | Top-level Dart package name. |
| `anyMapping` | `dynamic` | How IR `Any` maps: `dynamic` (default), `object`, or `string`. |

```json
{
  "language": "dart",
  "options": {
    "packageName": "petstore_client",
    "anyMapping": "dynamic"
  }
}
```

## File layout

```
<output>/
  petstore_client/
    lib/
      models/pet.dart
      enums/status.dart
      client/pets_client.dart
    pubspec.yaml
```

## Example

```bash
valio generate -s ./openapi.yaml -o ./out -l dart
```
