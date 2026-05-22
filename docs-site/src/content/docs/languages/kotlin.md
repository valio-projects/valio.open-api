---
title: Kotlin
description: Kotlin generator options and outputs.
sidebar:
  order: 13
---

## Overview

The Kotlin plugin (`languageId: "kotlin"`, alias `kt`) targets Kotlin 1.9+ with
coroutines and `kotlinx.serialization`. The generated client is built on
OkHttp; each operation is a `suspend` function.

## What gets generated

- **Data classes** — `@Serializable data class` per schema, with `@SerialName`
  preserving the original JSON property casing.
- **Enums** — `@Serializable enum class` with `@SerialName` per value.
- **HTTP client** — one `*Client` object per tag wrapping `OkHttpClient`; every
  operation is `suspend fun` returning the typed result.
- **Build files** — optional `build.gradle.kts` with the
  `kotlinx-serialization` and `okhttp3` dependencies when
  `generateBuildFiles` is true.

## Options

| Option | Default | Description |
|--------|---------|-------------|
| `package` | `com.example.generated` | Root Kotlin package. Required. |
| `serialization` | `kotlinx` | Serialization library. `kotlinx` is fully supported; `moshi` and `gson` are reserved. |
| `generateBuildFiles` | `false` | Emit Gradle build files alongside sources. |

```json
{
  "language": "kotlin",
  "options": {
    "package": "com.acme.petstore",
    "serialization": "kotlinx",
    "generateBuildFiles": false
  }
}
```

## File layout

```
<output>/
  com/acme/petstore/
    models/Pet.kt
    enums/Status.kt
    client/PetsClient.kt
  build.gradle.kts       (when generateBuildFiles=true)
```

## Example

```bash
valio generate -s ./openapi.yaml -o ./out -l kotlin
```
