---
title: Веб-приложение
description: Запуск ValIO.Web в браузере.
translationKey: web
---

`ValIO.Web` — приложение на ASP.NET Core 10 + Blazor Server. Оно
предоставляет тот же конвейер генерации, что и CLI, но управляемый
формой в браузере.

## Локальный запуск

```bash
dotnet run --project src/ValIO.Web
# http://localhost:5080
```

## Возможности

- **Источник схемы** — вставка URL или загрузка `.json` / `.yaml`-файла
  (до 10 МБ).
- **Выбор языка** — каждый плагин, зарегистрированный через
  `AddAllGeneratorPlugins`, появляется в выпадающем списке.
- **Динамическая форма опций плагина** — элементы строятся по списку
  `OptionDescription` каждого плагина: Bool → переключатель,
  EnumSingle → выпадающий список, EnumMulti → набор чипов и т. д.
- **Вывод в ZIP** — сервер стримит zip-архив, построенный в памяти
  через `ZipStreamOutputSink`; браузер скачивает его по blob-URL.
- **Переключатель языка** — меню в правом верхнем углу ставит cookie
  и перезагружает страницу с выбранной локалью (`en`, `ru`, `de`,
  `zh-CN`).

## API-эндпоинт

Форма отправляет `POST /api/generate` с JSON:

```json
{
  "schemaUrl":            "https://example.com/openapi.json",
  "schemaFileName":       null,
  "schemaContentBase64":  null,
  "languageId":           "typescript",
  "namespace":            null,
  "generateModels":       true,
  "generateClients":      true,
  "generateEnums":        true,
  "pluginOptions":        { "transports": ["fetch","axios"] }
}
```

Должно быть задано либо `schemaUrl`, либо `schemaContentBase64`, но
не оба сразу. Ответ — `application/zip` с
`Content-Disposition: attachment`.

## Безопасность

- Загрузчик по URL отклоняет схемы, не использующие `http(s)`, а также
  любые хосты, разрешающиеся в loopback, link-local или приватные
  диапазоны RFC1918.
- Размер загрузки ограничен 10 МБ.
- Все запросы выполняются в области запроса; межциркульного состояния
  нет.
