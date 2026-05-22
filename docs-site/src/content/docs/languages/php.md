---
title: PHP
description: PHP generator options and outputs.
sidebar:
  order: 15
---

## Overview

The PHP plugin (`languageId: "php"`, aliases `php8`, `php-guzzle`) targets
PHP 8.1+ and emits typed value objects plus a Guzzle 7-based client. Responses
flow through PSR-7 `ResponseInterface`. Laravel HTTP support is reserved for
a future release.

## What gets generated

- **Classes** — one `final class` per OpenAPI schema with promoted constructor
  parameters and typed properties (PHP 8 syntax).
- **Enums** — backed PHP 8.1 `enum` declarations.
- **HTTP client** — one `*Client` per tag wrapping `GuzzleHttp\Client`, each
  operation returning a typed DTO.
- **composer.json** — optional, when `generateComposerJson` is true.

## Options

| Option | Default | Description |
|--------|---------|-------------|
| `namespace` | `App\Generated` | Root PHP namespace for generated classes. Required. |
| `useHttpClient` | `guzzle` | HTTP client. `guzzle` is supported; `laravel` is reserved. |
| `generateComposerJson` | `false` | Emit `composer.json` with required dependencies. |

```json
{
  "language": "php",
  "options": {
    "namespace": "Acme\\PetStore",
    "useHttpClient": "guzzle",
    "generateComposerJson": true
  }
}
```

## File layout

```
<output>/
  Acme/PetStore/
    Models/Pet.php
    Enums/Status.php
    Client/PetsClient.php
  composer.json          (when generateComposerJson=true)
```

## Example

```bash
valio generate -s ./openapi.yaml -o ./out -l php
```
