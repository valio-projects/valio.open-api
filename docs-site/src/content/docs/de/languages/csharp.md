---
title: C#
description: Optionen und Ausgaben des C#-Generators.
translationKey: lang-csharp
---

Das C#-Plugin (`languageId: "csharp"`) erzeugt DTOs sowie eine
streng typisierte Client-Klasse pro OpenAPI-Tag.

## Optionen

```json
{
  "language": "csharp",
  "namespace": "MyOrg.Api.Client",
  "options": {
    "nullableReferenceTypes": true
  }
}
```

## Was generiert wird

- DTO-Klassen — eine `class` pro Schema; `[JsonPropertyName]`-Attribute
  bewahren die ursprüngliche Schreibweise.
- Enums — C#-`enum` mit `JsonStringEnumConverter`-Annotation.
- Clients — ein `*Client` pro Tag, der Konstruktor erwartet einen
  `HttpClient`, jede Operation ist `async Task<TResult>`.
- `IServiceCollection`-Erweiterung zur Registrierung der Clients über
  die `HttpClientFactory`.
