---
title: Global options
description: Options that apply to every valio subcommand.
---

These flags are recognized before the subcommand name and affect the
CLI globally. The full source lives in
`src/ValIO.Cli/Commands/RootCommand.cs`.

## `--lang <code>`

UI culture for log messages and error text. The CLI parses this option
*before* DI is built so that the localization layer initializes against
the requested culture.

```bash
valio --lang ru generate -s ./openapi.json -o ./out -l typescript
valio --lang=de validate -s ./openapi.json
```

Accepted values:

| Value     | Locale                |
|-----------|-----------------------|
| `en`      | English (default)     |
| `ru`      | Russian               |
| `de`      | German                |
| `zh-CN`   | Simplified Chinese    |

Unknown values are reported to stderr (`Unknown culture: <value>`) and
the CLI falls back to the OS default.

If `--lang` is omitted, ValIO falls back to the `VALIO_LANG`
environment variable, then to the OS culture.

## Environment variables

| Variable                              | Effect                                                                  |
|---------------------------------------|-------------------------------------------------------------------------|
| `VALIO_LANG`                          | UI culture if `--lang` is not provided.                                 |
| `DOTNET_NOLOGO=1`                     | Hide the .NET runtime first-run banner.                                 |
| `DOTNET_CLI_TELEMETRY_OPTOUT=1`       | Opt the .NET CLI out of telemetry (ValIO sends no telemetry of its own).|
| `HTTP_PROXY` / `HTTPS_PROXY`          | Honored when `--schema` is a remote URL.                                |

See [Configuration / Environment variables](/configuration/env-vars/)
for the full list.

## `--help` / `-h`

Standard System.CommandLine help. Works at every level:

```bash
valio --help
valio generate --help
valio validate --help
```

## `--version`

Prints the CLI version (from the assembly):

```bash
valio --version
```

## `--verbose`

Currently the CLI uses `Microsoft.Extensions.Logging.Console` at
`Information` level by default; there is no dedicated `--verbose`
toggle yet. Override via the standard
`Logging:LogLevel:Default=Debug` env var if you need a louder log:

```bash
Logging__LogLevel__Default=Debug valio generate ...
```

Future versions will expose a first-class `--verbose` flag.

## `--config` / `-c`

Although `--config` is technically a `generate` subcommand option (not
a true global), it's worth listing here because almost every real
invocation uses it. See [`valio generate`](/cli/generate/) for the
full shape and [Configuration / Projects](/configuration/projects/)
for the file format.
