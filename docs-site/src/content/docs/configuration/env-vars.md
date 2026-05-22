---
title: Environment variables
description: Variables that change ValIO's behavior outside the CLI flags.
---

ValIO reads a small set of environment variables. Anything not listed
here has no effect on the CLI.

## `VALIO_LANG`

UI culture for log messages and error text. Same accepted values as the
`--lang` CLI flag.

```bash
export VALIO_LANG=ru
valio generate -s ./openapi.json -o ./out -l typescript
```

Resolution order (first non-empty wins):

1. `--lang` flag on the command line.
2. `VALIO_LANG` environment variable.
3. The current OS culture.

Accepted values: `en`, `ru`, `de`, `zh-CN`. Anything else logs
`Unknown culture: <value>` to stderr and falls back to English.

## .NET runtime variables

ValIO is a .NET 10 binary, so any standard `DOTNET_*` variable still
applies. The ones most relevant to a code generator:

| Variable                              | Effect |
|---------------------------------------|--------|
| `DOTNET_ROOT`                         | Where the .NET runtime lives. Useful when you have multiple SDKs. |
| `DOTNET_NOLOGO=1`                     | Hide the first-run banner from `dotnet tool`. |
| `DOTNET_CLI_TELEMETRY_OPTOUT=1`       | Disable .NET CLI telemetry (does not affect ValIO itself — ValIO sends no telemetry). |
| `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=0` | Required if `--lang` doesn't resolve cultures on a slim container image. Keep this at `0` (the default). |

## File path resolution

All relative paths in `--schema`, `--output`, and config files resolve
against the current working directory at the time `valio` starts, not
the location of the config file itself.

```bash
# Reads ./openapi.json relative to the shell's cwd, not the config's folder.
valio generate -c ./configs/valio.config.json
```

If you script ValIO from a different directory, either `cd` into the
project first or use absolute paths inside the config.

## `HTTP_PROXY` / `HTTPS_PROXY`

ValIO's HTTP client respects the OS-level proxy variables when
downloading remote schemas via `--schema https://...`. No ValIO-specific
configuration is required.
