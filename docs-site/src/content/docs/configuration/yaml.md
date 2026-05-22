---
title: YAML configuration
description: Use valio.config.yaml instead of JSON.
---

ValIO reads both JSON and YAML configuration files. The format is
chosen by file extension by `ConfigurationLoader`:

| Extension              | Loader                |
|------------------------|-----------------------|
| `.json`                | `LoadConfigFromJson`  |
| `.yaml`, `.yml`        | `LoadConfigFromYaml`  |

Anything else is rejected with `NotSupportedException`.

## Equivalent files

The two snippets below are interchangeable.

### `valio.config.json`

```json
{
  "projects": {
    "web": {
      "language":   "typescript",
      "schema":     "./openapi.json",
      "outputPath": "./apps/web/src/api",
      "options": {
        "transports":        ["fetch", "axios"],
        "validationLibrary": "zod"
      }
    }
  }
}
```

### `valio.config.yaml`

```yaml
projects:
  web:
    language: typescript
    schema: ./openapi.json
    outputPath: ./apps/web/src/api
    options:
      transports:
        - fetch
        - axios
      validationLibrary: zod
```

The CLI picks the format from the path you pass to `-c`:

```bash
valio generate -c ./valio.config.yaml
valio generate -c ./valio.config.json
```

## When to prefer YAML

- **Comments.** YAML supports `# inline comments` natively.
- **Multi-line strings.** Useful for embedding small `description`
  blurbs or path templates without escaping.
- **Diffing.** Reviewers often find YAML diffs easier to read.

Stick with JSON if your tooling prefers it or if you generate the
config from another script — `JsonSerializer` is faster and stricter.
