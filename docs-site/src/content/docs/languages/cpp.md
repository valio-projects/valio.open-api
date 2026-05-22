---
title: C++ (Unreal Engine)
description: C++ generator options and outputs targeting Unreal Engine.
sidebar:
  order: 16
---

## Overview

The C++ plugin (`languageId: "cpp"`) targets **Unreal Engine 5** rather than
generic C++. It emits `USTRUCT`/`UCLASS` types, Blueprint-callable
`UFUNCTION`s, and async actions derived from `UBlueprintAsyncActionBase`,
wired into UE's HTTP module via a small ValIO runtime.

## What gets generated

- **USTRUCT models** — one struct per OpenAPI schema, with `UPROPERTY`
  metadata for the Blueprint editor and Doxygen comments preserved from the
  schema description.
- **UENUM enums** — Blueprint-visible enums with `DisplayName` metadata.
- **Async client actions** — one `UCLASS` per operation, inheriting from
  `UValIOAsyncActionBase`. Exposed as a Blueprint async node returning the
  typed response.
- **Native API functions** — free C++ functions wrapping the async actions
  so the same operations are callable from regular C++ code.
- **Request builders** — helper code that translates path/query parameters
  into `FValIORequestBuilder` calls.

The plugin currently exposes **no user-facing options**; behaviour is
configured in `CppGenerationOptions` defaults inside the assembly.
Notable internal knobs include:

| Internal setting | Default | Purpose |
|------------------|---------|---------|
| `ModuleApiMacro` | `MODULE_API` | Macro inserted on `UCLASS`/`USTRUCT` (e.g. `MYPROJECT_API`). |
| `GeneratedHeaderSuffix` | `.generated.h` | UHT-required suffix for generated headers. |
| `BlueprintCategory` | `ValIO` | Default category in the Blueprint Editor. |
| `BaseAsyncActionClassName` | `UValIOAsyncActionBase` | Base class for async operations. |

## File layout

```
<output>/
  Generated/
    Models/FPet.h, FPet.cpp
    Enums/EStatus.h
    Endpoints/UGetPetsAsync.h, UGetPetsAsync.cpp
    Params/FPetsListParams.h
    Result/FPetsListResult.h
```

## Example

```bash
valio generate -s ./openapi.yaml -o ./Source/MyGame -l cpp
```
