---
title: Java
description: Java generator options and outputs.
sidebar:
  order: 12
---

## Overview

The Java plugin (`languageId: "java"`, aliases `java21`, `java-okhttp`) targets
modern Java (17+) and produces value classes plus a synchronous OkHttp-backed
client. JSON is handled with Jackson; Bean Validation annotations are emitted
on DTOs when applicable.

## What gets generated

- **DTOs** — one `public final class` per OpenAPI schema with constructor,
  getters and Jackson `@JsonProperty` mapping.
- **Enums** — Java `enum` types with `@JsonValue` / `@JsonCreator`.
- **HTTP client** — one `*Client` per OpenAPI tag built on top of OkHttp 3.x,
  each operation returning a typed response object.
- **Build files** — optional `build.gradle.kts` + `gradle.properties` with
  required dependencies, when `generateBuildFiles` is true.

## Options

| Option | Default | Description |
|--------|---------|-------------|
| `package` | `com.example.generated` | Root Java package for generated classes. Required. |
| `useHttpClient` | `okhttp` | HTTP client backend. Only `okhttp` is supported today. |
| `generateBuildFiles` | `false` | Emit `build.gradle.kts` + `gradle.properties` alongside sources. |

```json
{
  "language": "java",
  "options": {
    "package": "com.acme.petstore",
    "useHttpClient": "okhttp",
    "generateBuildFiles": true
  }
}
```

## File layout

```
<output>/
  com/acme/petstore/
    models/Pet.java
    models/PetTag.java
    enums/Status.java
    client/PetsClient.java
  build.gradle.kts       (when generateBuildFiles=true)
  gradle.properties      (when generateBuildFiles=true)
```

## Example

```bash
valio generate -s ./openapi.yaml -o ./out -l java
```
