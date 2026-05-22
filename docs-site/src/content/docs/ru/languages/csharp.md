---
title: C#
description: Опции и результаты генератора C#.
translationKey: lang-csharp
---

Плагин C# (`languageId: "csharp"`) генерирует DTO и строго
типизированный клиент-класс на каждый OpenAPI-тег.

## Опции

```json
{
  "language": "csharp",
  "namespace": "MyOrg.Api.Client",
  "options": {
    "nullableReferenceTypes": true
  }
}
```

## Что генерируется

- DTO-классы — один `class` на каждую схему, атрибуты
  `[JsonPropertyName]` сохраняют исходный регистр имён.
- Перечисления — C#-`enum` с аннотацией `JsonStringEnumConverter`.
- Клиенты — один `*Client` на тег; конструктор принимает
  `HttpClient`, каждая операция возвращает `async Task<TResult>`.
- Расширение `IServiceCollection` для регистрации клиентов через
  `HttpClientFactory`.
