---
title: CLI troubleshooting
description: Common errors and how to fix them.
---

The most frequent failure modes when running `valio` and what each
log line means.

## `valio: command not found`

The `dotnet tool install` succeeded but the shell can't find the
binary on `PATH`.

| OS        | Where the tool lives                        | Fix                                                                 |
|-----------|---------------------------------------------|---------------------------------------------------------------------|
| Windows   | `%USERPROFILE%\.dotnet\tools`               | Restart the shell. Confirm with `where valio`.                      |
| macOS     | `~/.dotnet/tools`                           | `echo 'export PATH="$HOME/.dotnet/tools:$PATH"' >> ~/.zshrc`        |
| Linux     | `~/.dotnet/tools`                           | `echo 'export PATH="$HOME/.dotnet/tools:$PATH"' >> ~/.bashrc`       |

See [Install](/install/) for OS-specific setup steps.

## `--schema, --output, and --language are required when not using --config`

You called `valio generate` without `-c` but didn't supply all three
positional drivers. Either:

```bash
valio generate -s ./openapi.json -o ./out -l typescript
```

…or use a config:

```bash
valio generate -c ./valio.config.json
```

## `No generator found for language: <id>`

The language id wasn't recognized. Possible causes:

- Typo (e.g. `typscript`). Run `valio --help` to see registered ids,
  or browse [Plugin IDs](/reference/plugin-ids/).
- Custom plugin not registered. The CLI only sees plugins that
  `AddAllGeneratorPlugins()` (or your custom equivalent) adds.

## `Invalid options for '<id>': …`

The plugin's `OptionsDescriptor.ParseOptions` rejected something in
your `options` block. The error message lists every offending key.
Cross-check the spelling and accepted values on the matching
[Languages](/languages/) page.

## `Failed to parse OpenAPI schema: …`

`Microsoft.OpenApi`'s reader rejected the document. The error message
usually pinpoints the line. Two quick checks:

- `valio validate -s ./openapi.json` runs the same parser without
  generating — fast iteration loop.
- Run the schema through <https://editor-next.swagger.io> or
  `redocly lint` if the error isn't obvious.

## `Project(s) not found: X. Available: Y, Z`

`-p X` didn't match any key under `projects` in your config. ValIO
prints the available names so you can spot typos quickly.

## `--config` file not found / wrong extension

Only `.json`, `.yaml`, and `.yml` are accepted. Anything else fails
with `NotSupportedException`. The path is resolved relative to the
shell's CWD, not the config file's directory.

## Web fetch failing behind a proxy

If `--schema https://…` fails to download, set the standard proxy
env vars before invoking:

```bash
export HTTPS_PROXY="http://proxy.corp:8080"
valio generate -s https://api.example.com/openapi.json -o ./out -l typescript
```

For authenticated schemas, download with `curl` first and pass the
local path:

```bash
curl -sSfH "Authorization: Bearer $TOKEN" \
    https://api.example.com/openapi.json > openapi.json
valio generate -s ./openapi.json -o ./out -l typescript
```

## Output files written but tests fail / IDE shows stale code

When using `DiskOutputSink`, ValIO overwrites only the files it
generates this run — it never deletes pre-existing files. If a model
was *renamed or removed* upstream, the stale file remains. Solutions:

- Add `output/` (or your chosen folder) to `.gitignore` and recreate
  from scratch in CI.
- Or run `rm -rf ./out/* && valio generate …` for a clean slate.

## Localized strings missing or English-only

If `--lang ru` (or any non-English value) still shows English, the
satellite assembly didn't load. Usually:

- Confirm `Resources/<key>.<culture>.resx` exists for the message.
  Missing keys fall back to English silently.
- On slim containers, make sure
  `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT` is *not* set to `1` — it
  disables culture support entirely.

## Long-running generation

There is no client-side timeout. Cancellation works via Ctrl-C; the
pipeline checks `CancellationToken` at file/model boundaries.

## Reporting bugs

If the message above doesn't help, open an issue with:

- ValIO version (`valio --version`).
- OS + .NET version (`dotnet --info`).
- A minimal OpenAPI document that reproduces the failure.
- The `--lang en` log so messages are unambiguous.
