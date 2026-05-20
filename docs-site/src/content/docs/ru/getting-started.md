---
title: Начало работы
description: Установите ValIO, укажите OpenAPI-схему, получите клиентов.
---

ValIO поставляется в виде трёх фронтендов поверх одного движка генерации:

- **CLI** — исполняемый файл `valio` (один .NET 10 бинарник).
- **Веб** — ASP.NET Core 10 + Blazor Server (`ValIO.Web`).
- **Десктоп** — Avalonia 11 (`ValIO.Desktop`), кросс-платформенный.

## Быстрый старт CLI

```bash
# Генерация TypeScript-клиентов в папку
valio generate -s ./openapi.json -o ./generated -l typescript

# То же самое, но zip-архив
valio generate -s ./openapi.json -o ./generated.zip -l typescript

# Или флаг --zip — расширение .zip добавится автоматически
valio generate -s ./openapi.json -o ./generated --zip -l typescript

# Сменить язык интерфейса (сообщения CLI + UI веб/десктоп)
valio --lang ru generate -s ./openapi.json -o ./generated -l typescript
```

## Поддерживаемые языки

| Идентификатор | Цель                                |
|---------------|-------------------------------------|
| `typescript`  | TS клиенты + Zod / Valibot / TypeBox + TanStack Query / SWR / MSW |
| `csharp`      | C# DTO и клиенты                    |
| `java`        | Java 21 + OkHttp                    |
| `kotlin`      | Kotlin + OkHttp + coroutines        |
| `python`      | Python 3.10+ + httpx + Pydantic     |
| `php`         | PHP 8 + Guzzle                      |
| `swift`       | Swift + URLSession                  |
| `cpp`         | C++ для Unreal Engine               |

Дальше — справочник [CLI](/ru/cli/) и [конфигурации](/ru/configuration/)
(остальные разделы пока на английском, переводы готовятся).
