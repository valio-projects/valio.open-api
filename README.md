# ValIO - OpenAPI Code Generator

ValIO - это консольная утилита для генерации HTTP клиентов, API моделей и валидационных схем для приложений на разных языках программирования, используя OpenAPI/Swagger спецификации.

## Возможности

- ✅ Генерация кода для **C#** и **TypeScript**
- ✅ Поддержка OpenAPI 3.0, 3.1, 3.2
- ✅ Генерация моделей, enum'ов, клиентов
- ✅ Гибкая настройка через JSON/YAML конфигурацию
- ✅ Поддержка различных HTTP клиентов (Fetch, Axios для TypeScript)
- ✅ Интеграция с TanStack Query для TypeScript
- ✅ Валидационные схемы (Zod для TypeScript)
- ✅ Автоматическая сериализация/десериализация через схемы
- ✅ Поддержка рекурсивных схем, AllOf/OneOf/AnyOf
- ✅ Поддержка словарей, массивов, nullable типов

## Быстрый старт

### Установка

```bash
dotnet build
```

### Использование

1. Создайте конфигурационный файл `valio.config.json`:

```json
{
  "language": "TypeScript",
  "outputPath": "./generated",
  "generateEnums": true,
  "generateModels": true,
  "generateClients": true,
  "options": {
    "typescript": {
      "modelFormat": "Interfaces",
      "httpClientLibrary": "Fetch"
    }
  }
}
```

2. Запустите генерацию:

```bash
dotnet run -- --config valio.config.json --schema https://api.example.com/openapi.json
```

## Документация

- [TypeScript Generation Guide](docs/TYPESCRIPT_GENERATION.md) - подробное руководство по генерации TypeScript кода
- [Configuration Examples](valio.config.example.json) - примеры конфигураций

## Примеры конфигураций

### TypeScript с интерфейсами и Fetch

```json
{
  "language": "TypeScript",
  "outputPath": "./generated",
  "options": {
    "typescript": {
      "modelFormat": "Interfaces",
      "httpClientLibrary": "Fetch"
    }
  }
}
```

### TypeScript с Zod, Axios и TanStack Query

```json
{
  "language": "TypeScript",
  "outputPath": "./generated",
  "options": {
    "typescript": {
      "modelFormat": "ValidationSchemas",
      "validationLibrary": "Zod",
      "httpClientLibrary": "Axios",
      "useTanStackQuery": true,
      "serializeWithSchemas": true,
      "deserializeWithSchemas": true
    }
  }
}
```

## Структура проекта

```
ValIO/
├── ValIO.Core/              # Основная логика, интерфейсы, модели
├── ValIO.Configuration/     # Загрузка и парсинг конфигураций
├── ValIO.Parsers/          # Парсинг OpenAPI документов
├── ValIO.Generators.CSharp/    # Генератор C# кода
├── ValIO.Generators.TypeScript/    # Генератор TypeScript кода
├── ValIO.Generators.TypeScript.Zod/    # Генератор Zod схем
├── ValIO.Generators.TypeScript.TanStackQuery/    # Генератор TanStack Query хуков
└── ValIO/                  # CLI приложение
```

## Технологии

- **.NET 9.0** - основная платформа
- **Microsoft.OpenApi** - парсинг OpenAPI
- **System.CommandLine** - CLI интерфейс
- **RazorLight** - шаблонный движок для генерации кода
- **Microsoft.Extensions.DependencyInjection** - DI
- **Microsoft.Extensions.Logging** - логирование
- **NUnit** - тестирование

## Лицензия

[Укажите лицензию]
