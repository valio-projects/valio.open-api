---
title: valio validate
description: Parse an OpenAPI schema and print metadata.
translationKey: cli-validate
---

```
valio validate --schema <path-or-url>
```

The `validate` subcommand parses the schema with the same parser the
generator uses, but does not produce any output. Useful as a CI check.

```bash
valio validate -s https://petstore3.swagger.io/api/v3/openapi.json
```

Sample output:

```
info: ValIO.Cli.Commands.ValidateCommand[0]
      OpenAPI schema is valid
      API Title: Swagger Petstore - OpenAPI 3.0
      API Version: 1.0.27
      Paths count: 13
```

Exit code is `0` if parsing succeeded and `1` otherwise.
