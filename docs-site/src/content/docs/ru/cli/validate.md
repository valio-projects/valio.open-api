---
title: valio validate
description: Разобрать OpenAPI-схему и вывести метаданные.
translationKey: cli-validate
---

```
valio validate --schema <path-or-url>
```

Подкоманда `validate` разбирает схему тем же парсером, что использует
генератор, но не создаёт никаких выходных файлов. Удобно как проверка
в CI.

```bash
valio validate -s https://petstore3.swagger.io/api/v3/openapi.json
```

Пример вывода:

```
info: ValIO.Cli.Commands.ValidateCommand[0]
      OpenAPI schema is valid
      API Title: Swagger Petstore - OpenAPI 3.0
      API Version: 1.0.27
      Paths count: 13
```

Код завершения — `0` при успешном разборе и `1` в противном случае.
