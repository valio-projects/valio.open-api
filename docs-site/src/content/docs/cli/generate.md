---
title: valio generate
description: Generate code from an OpenAPI schema.
translationKey: cli-generate
---

```
valio [--lang <culture>] generate
    [-s|--schema    <path-or-url>]
    [-o|--output    <folder-or-zip-path>]
    [-l|--language  <id>]
    [-c|--config    <config-file>]
    [-p|--projects  <name>...]
    [--format       folder|zip|auto]
    [--zip]
```

## Options

| Option        | Description                                                 |
|---------------|-------------------------------------------------------------|
| `-s` / `--schema`     | Path or URL to OpenAPI schema. Required without `--config`. |
| `-o` / `--output`     | Folder or `.zip` file path. Required without `--config`.    |
| `-l` / `--language`   | Generator id (`typescript`, `csharp`, ...). Required without `--config`. |
| `-c` / `--config`     | JSON or YAML config file with one or more projects. |
| `-p` / `--projects`   | Project name(s) inside the config file. Default: all. |
| `--format`            | `folder`, `zip`, or `auto`. Default `auto`. |
| `--zip`               | Shortcut for `--format zip`. |

## Output format resolution

Auto-detection by `--output` extension:

| `--output`     | `--format` | Result                                  |
|----------------|------------|-----------------------------------------|
| `./out`        | (default)  | folder `./out/`                         |
| `./out.zip`    | (default)  | zip `./out.zip`                         |
| `./out`        | `zip`      | zip `./out.zip` (`.zip` appended)       |
| `./out.zip`    | `folder`   | folder named `./out.zip/`               |

## Examples

```bash
# Folder output
valio generate -s ./openapi.json -o ./generated -l typescript

# Zip output (auto-detected)
valio generate -s ./openapi.json -o ./generated.zip -l typescript

# Multi-project run from config
valio generate -c ./valio.config.json -p web,mobile
```
