---
title: valio generate
description: Генерация кода по OpenAPI-схеме.
translationKey: cli-generate
---

```
valio [--lang <culture>] generate
    [-s|--schema    <path-or-url>]
    [-o|--output    <folder-or-zip-path>]
    [-l|--language  <id>]
    [-c|--config    <config-file>]
    [-p|--projects  <name>...]
    [--format       folder|zip|auto]
    [--zip]
```

## Опции

| Опция                 | Описание                                                                 |
|-----------------------|--------------------------------------------------------------------------|
| `-s` / `--schema`     | Путь или URL OpenAPI-схемы. Обязательна, если не указан `--config`.      |
| `-o` / `--output`     | Путь к папке или `.zip`-файлу. Обязательна, если не указан `--config`.    |
| `-l` / `--language`   | Идентификатор генератора (`typescript`, `csharp`, ...). Обязательна без `--config`. |
| `-c` / `--config`     | JSON- или YAML-файл конфигурации с одним или несколькими проектами.      |
| `-p` / `--projects`   | Имя (имена) проектов внутри файла конфигурации. По умолчанию — все.      |
| `--format`            | `folder`, `zip` или `auto`. По умолчанию `auto`.                         |
| `--zip`               | Сокращение для `--format zip`.                                           |

## Определение формата вывода

Автоопределение по расширению `--output`:

| `--output`     | `--format` | Результат                                  |
|----------------|------------|--------------------------------------------|
| `./out`        | (по умолч.)| папка `./out/`                             |
| `./out.zip`    | (по умолч.)| zip-файл `./out.zip`                       |
| `./out`        | `zip`      | zip-файл `./out.zip` (`.zip` добавлен)     |
| `./out.zip`    | `folder`   | папка с именем `./out.zip/`                |

## Примеры

```bash
# Вывод в папку
valio generate -s ./openapi.json -o ./generated -l typescript

# Вывод в zip (определяется автоматически)
valio generate -s ./openapi.json -o ./generated.zip -l typescript

# Многопроектный запуск из файла конфигурации
valio generate -c ./valio.config.json -p web,mobile
```
