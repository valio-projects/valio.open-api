---
title: C#
description: C# generator options and outputs.
translationKey: lang-csharp
---

The C# plugin (`languageId: "csharp"`) emits DTOs and a strongly-typed
client class per OpenAPI tag.

## Options

```json
{
  "language": "csharp",
  "namespace": "MyOrg.Api.Client",
  "options": {
    "nullableReferenceTypes": true
  }
}
```

## What gets generated

- DTO classes — one `class` per schema, `[JsonPropertyName]` attributes
  preserve the original casing.
- Enums — C# `enum` with `JsonStringEnumConverter` annotation.
- Clients — one `*Client` per tag, constructor takes `HttpClient`,
  each operation is `async Task<TResult>`.
- `IServiceCollection` extension to register clients with
  `HttpClientFactory`.
