---
title: TypeScript
description: TypeScript 生成器的选项与产物。
translationKey: lang-typescript
---

TypeScript 插件（`languageId: "typescript"`）是内置生成器中最庞大、
可配置项最多的一个。

## 生成内容

- **模型** —— 每个 OpenAPI 架构对应一个 `interface`，同时支持判别式
  联合类型。
- **枚举** —— 根据 `EnumFormat` 选择字面量字符串联合或 `enum`。
- **Params / Results** —— 每个操作输出一个 `params` 类型与一个
  `results` 类型。
- **HTTP 客户端** —— 可选其一或多个：`fetch`、`axios`、`ky`、
  `undici`、`ofetch`、`got`、`angular`（HttpClient）、`solid`
  （SolidStart）。
- **校验架构** —— Zod（3 或 4）、Valibot 或 TypeBox。
- **查询封装** —— TanStack Query（React / Vue / Angular / Solid /
  Svelte）或 SWR。
- **Mock** —— Mock Service Worker handler。
- **索引文件** —— 统一的 re-export 树。

## 选项

```json
{
  "language": "typescript",
  "options": {
    "transports":         ["fetch", "axios"],
    "validationLibrary":  "zod",
    "queryWrapper":       "tanstack",
    "mockLibrary":        "msw",
    "enumFormat":         "string-union"
  }
}
```

## 文件布局

默认情况下，模型放在 `models/<name>/<name>.ts`，客户端放在
`api/<operation>/<operation>.<transport>.ts`。可通过
`fileOrganization.modelsPath` 与 `fileOrganization.clientsPath`
进行覆盖。
