---
title: Running ValIO in CI
description: GitHub Actions and GitLab CI snippets for a code-gen step.
---

ValIO is built to be a CI step: deterministic, no network unless you
ask for one, exits non-zero on failure. The pattern below is to run
codegen and fail the build if the working tree drifts from the
checked-in output.

## GitHub Actions

```yaml
name: Codegen check

on:
  pull_request:
  push:
    branches: [main]

jobs:
  codegen:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Install ValIO
        run: dotnet tool install --global ValIO.Cli

      - name: Generate
        run: valio generate -c ./valio.config.json

      - name: Fail if generated files drifted
        run: git diff --exit-code
```

If a contributor edits `openapi.json` without regenerating, the
`git diff --exit-code` step exits `1` and shows the missing changes
in the job log.

## GitHub Actions: matrix per project

When you fan a schema out to several languages, parallelize the
matrix so each project owns its own job runtime:

```yaml
strategy:
  matrix:
    target: [web, server, mobile]
steps:
  # ... checkout, setup-dotnet, install valio ...
  - run: valio generate -c ./valio.config.json -p ${{ matrix.target }}
  - run: git diff --exit-code
```

## GitLab CI

```yaml
codegen:
  image: mcr.microsoft.com/dotnet/sdk:10.0
  script:
    - dotnet tool install --global ValIO.Cli
    - export PATH="$PATH:$HOME/.dotnet/tools"
    - valio generate -c ./valio.config.json
    - git diff --exit-code
```

## Caching the tool install

The `dotnet tool install` step is fast (~1s on a warm runner), but if
you want to skip it entirely, pin the version in a tool manifest:

```bash
dotnet new tool-manifest
dotnet tool install ValIO.Cli
git add .config/dotnet-tools.json
git commit -m "Pin ValIO version"
```

Then CI becomes:

```yaml
- run: dotnet tool restore
- run: dotnet valio generate -c ./valio.config.json
```

`dotnet tool restore` is reproducible by commit hash.

## Localized log output

To suppress the localized prefixes in CI logs (handy when grepping):

```yaml
- run: valio --lang en generate -c ./valio.config.json
```

Otherwise CI honors the runner's culture (typically `C` / `en_US`,
which already falls back to English).

## Reading the exit code

| Exit code | Meaning                                       |
|-----------|-----------------------------------------------|
| `0`       | All selected projects generated successfully. |
| `1`       | One or more projects failed — see job log.    |

See [Reference / Exit codes](/reference/exit-codes/) for the full
table.

## Schema fetched from a URL

If your schema lives behind an authenticated endpoint, fetch it once
and pass the path:

```yaml
- name: Fetch OpenAPI
  run: curl -sSfH "Authorization: Bearer ${{ secrets.API_TOKEN }}" \
        https://api.example.com/openapi.json > openapi.json

- run: valio generate -c ./valio.config.json
```

Avoid passing the URL directly to `valio --schema` when it requires
auth — ValIO's HTTP client does not pick up arbitrary headers.
