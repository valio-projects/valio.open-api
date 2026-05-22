---
title: 配置文件
description: 多项目 valio.config.json 参考。
translationKey: configuration
---

配置文件让您可以同时描述多个生成目标（例如一个 TypeScript 应用和一
个 C# 服务端），并通过一条 `valio generate -c valio.config.json`
命令一次性运行所有目标。

## 格式

```json
{
  "projects": {
    "web": {
      "language":       "typescript",
      "schema":         "./openapi.json",
      "outputPath":     "./apps/web/src/api",
      "namespace":      "@my-org/api",
      "generateModels": true,
      "generateClients": true,
      "options": {
        "transports":       ["fetch", "axios"],
        "validationLibrary": "zod",
        "queryWrapper":      "tanstack"
      }
    },
    "server": {
      "language":   "csharp",
      "schema":     "./openapi.json",
      "outputPath": "./MyOrg.Api.Client/Generated",
      "namespace":  "MyOrg.Api.Client"
    }
  }
}
```

## 字段

| 字段              | 类型                | 说明 |
|-------------------|---------------------|------|
| `language`        | 字符串              | 生成器 ID，例如 `typescript`、`csharp`。 |
| `schema`          | 字符串              | 路径或 URL。 |
| `outputPath`      | 字符串              | 文件夹或 `.zip` 路径。 |
| `namespace`       | 字符串              | C#/Java/Kotlin/PHP 命名空间，或 TS 模块前缀。 |
| `generateModels`  | bool（默认 true）   | |
| `generateClients` | bool（默认 true）   | |
| `generateEnums`   | bool（默认 true）   | |
| `fileOrganization`| 对象                | 按目标覆盖文件结构。 |
| `namingConvention`| 对象                | 按标识符类型覆盖命名风格规则。 |
| `options`         | 对象                | 插件特定选项。参见 [语言](/zh-cn/languages/)。 |

## 运行

```bash
# 全部项目
valio generate -c ./valio.config.json

# 指定项目
valio generate -c ./valio.config.json --projects web,server
```

也支持 YAML 格式（`valio.config.yaml`）。
