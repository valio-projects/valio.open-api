---
title: C#
description: C# 生成器的选项与产物。
translationKey: lang-csharp
---

C# 插件（`languageId: "csharp"`）会按 OpenAPI tag 输出 DTO 与一个
强类型客户端类。

## 选项

```json
{
  "language": "csharp",
  "namespace": "MyOrg.Api.Client",
  "options": {
    "nullableReferenceTypes": true
  }
}
```

## 生成内容

- DTO 类 —— 每个架构生成一个 `class`，并通过
  `[JsonPropertyName]` 特性保留原始字段命名风格。
- 枚举 —— 使用 `JsonStringEnumConverter` 注解的 C# `enum`。
- 客户端 —— 每个 tag 输出一个 `*Client`，构造函数接收
  `HttpClient`，每个操作均为 `async Task<TResult>`。
- 提供 `IServiceCollection` 扩展方法，可通过
  `HttpClientFactory` 注册客户端。
