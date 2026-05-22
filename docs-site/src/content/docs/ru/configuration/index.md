---
title: Файл конфигурации
description: Справочник по многопроектному valio.config.json.
translationKey: configuration
---

Файл конфигурации позволяет описать несколько целей генерации сразу
(например, одно TypeScript-приложение и один C#-сервер) и запустить
их все одним вызовом `valio generate -c valio.config.json`.

## Формат

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

## Поля

| Поле              | Тип                  | Описание |
|-------------------|----------------------|----------|
| `language`        | строка               | Идентификатор генератора, например `typescript`, `csharp`. |
| `schema`          | строка               | Путь или URL. |
| `outputPath`      | строка               | Папка или путь к `.zip`-файлу. |
| `namespace`       | строка               | Пространство имён C#/Java/Kotlin/PHP или префикс модуля TS. |
| `generateModels`  | bool (по умолчанию true) | |
| `generateClients` | bool (по умолчанию true) | |
| `generateEnums`   | bool (по умолчанию true) | |
| `fileOrganization`| объект               | Переопределение структуры файлов для конкретной цели. |
| `namingConvention`| объект               | Переопределение правил регистра для каждого вида идентификаторов. |
| `options`         | объект               | Опции, специфичные для плагина. См. [Языки](/ru/languages/). |

## Запуск

```bash
# Все проекты
valio generate -c ./valio.config.json

# Конкретные проекты
valio generate -c ./valio.config.json --projects web,server
```

Поддерживается и YAML (`valio.config.yaml`).
