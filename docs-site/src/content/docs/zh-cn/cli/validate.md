---
title: valio validate
description: 解析 OpenAPI 架构并打印元数据。
translationKey: cli-validate
---

```
valio validate --schema <path-or-url>
```

`validate` 子命令使用与生成器相同的解析器解析架构，但不产生任何输出
文件。适合用作 CI 检查。

```bash
valio validate -s https://petstore3.swagger.io/api/v3/openapi.json
```

示例输出：

```
info: ValIO.Cli.Commands.ValidateCommand[0]
      OpenAPI schema is valid
      API Title: Swagger Petstore - OpenAPI 3.0
      API Version: 1.0.27
      Paths count: 13
```

解析成功时退出码为 `0`，否则为 `1`。
