---
title: valio validate
description: Ein OpenAPI-Schema parsen und Metadaten ausgeben.
translationKey: cli-validate
---

```
valio validate --schema <path-or-url>
```

Der Unterbefehl `validate` parst das Schema mit demselben Parser, den
auch der Generator verwendet, erzeugt aber keine Ausgabe. Nützlich als
CI-Check.

```bash
valio validate -s https://petstore3.swagger.io/api/v3/openapi.json
```

Beispielausgabe:

```
info: ValIO.Cli.Commands.ValidateCommand[0]
      OpenAPI schema is valid
      API Title: Swagger Petstore - OpenAPI 3.0
      API Version: 1.0.27
      Paths count: 13
```

Der Exit-Code ist `0`, wenn das Parsen erfolgreich war, sonst `1`.
