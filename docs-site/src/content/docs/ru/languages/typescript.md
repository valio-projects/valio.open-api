---
title: TypeScript
description: Опции и результаты генератора TypeScript.
translationKey: lang-typescript
---

Плагин TypeScript (`languageId: "typescript"`) — самый большой и
гибко настраиваемый среди встроенных генераторов.

## Что генерируется

- **Модели** — `interface` на каждую схему OpenAPI плюс
  размеченные объединения.
- **Перечисления** — литерально-строковые объединения или `enum`
  в зависимости от значения `EnumFormat`.
- **Params / Results** — по одному типу на операцию для `params`
  и `results`.
- **HTTP-клиенты** — один или несколько из: `fetch`, `axios`, `ky`,
  `undici`, `ofetch`, `got`, `angular` (HttpClient), `solid`
  (SolidStart).
- **Схемы валидации** — Zod (3 или 4), Valibot или TypeBox.
- **Обёртки запросов** — TanStack Query (React / Vue / Angular /
  Solid / Svelte) или SWR.
- **Моки** — обработчики Mock Service Worker.
- **Индексные файлы** — единое дерево реэкспортов.

## Опции

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

## Структура файлов

По умолчанию модели попадают в `models/<name>/<name>.ts`, а клиенты —
в `api/<operation>/<operation>.<transport>.ts`. Переопределяется
через `fileOrganization.modelsPath` и `fileOrganization.clientsPath`.
