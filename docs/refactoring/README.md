# ValIO — Документация рефакторинга

Этот раздел содержит детальные планы рефакторинга и расширения проекта ValIO.

## Документы

| Файл | Описание | Этап | Приоритет |
|------|----------|------|-----------|
| [00_OVERVIEW.md](./00_OVERVIEW.md) | Общий обзор, карта зависимостей, roadmap | — | — |
| [01_AST_REFACTORING.md](./01_AST_REFACTORING.md) | Удаление AstNodeKind, чистый Visitor, унификация AST нод | 0 | Критический |
| [02_PLUGIN_ARCHITECTURE.md](./02_PLUGIN_ARCHITECTURE.md) | Plugin-архитектура, self-registering генераторы, ConfigurationLoader | 0 | Критический |
| [03_NEW_TS_CLIENTS.md](./03_NEW_TS_CLIENTS.md) | Новые TS HTTP клиенты, Angular, SolidStart, MSW, TanStack Query | 1, 3 | Высокий |
| [04_NEW_LANGUAGES.md](./04_NEW_LANGUAGES.md) | PHP, Python, Java, Kotlin, Swift, Dart, RestSharp | 4 | Средний |
| [05_TS_VALIDATION.md](./05_TS_VALIDATION.md) | Valibot, TypeBox, Zod v3/v4 | 2 | Высокий |
| [06_LEGACY_CLEANUP.md](./06_LEGACY_CLEANUP.md) | Полный каталог legacy-кода под удаление (53+ файла) | 0 | Критический |
| [07_IR_NODES_AND_BUILDERS.md](./07_IR_NODES_AND_BUILDERS.md) | IR иерархия, IrBuilder, IR→AST конвертация, IrTypeDependencyGraph | 0 | Критический |
| [08_AST_BUILDERS_DEEP.md](./08_AST_BUILDERS_DEEP.md) | Детальный разбор TypeScriptAstBuilder, CSharpAstBuilder, все алгоритмы | 0–1 | Высокий |
| [09_DESIGN_PATTERNS.md](./09_DESIGN_PATTERNS.md) | Применение GoF паттернов: Visitor, CoR, Strategy, Builder, Template Method, Command, Composite, Prototype, Iterator, Facade, State, Proxy | Сквозной | Архитектурный |

## Порядок реализации

```
Этап 0 (фундамент)
  ├── 01_AST_REFACTORING  → удалить AstNodeKind
  └── 02_PLUGIN_ARCHITECTURE → plugin system

Этап 1 (TS HTTP клиенты)     ← зависит от Этапа 0
  └── 03_NEW_TS_CLIENTS (HTTP транспорты)

Этап 2 (TS Validation)       ← зависит от Этапа 0
  └── 05_TS_VALIDATION

Этап 3 (TanStack для фреймворков)  ← зависит от Этапов 1, 2
  └── 03_NEW_TS_CLIENTS (TanStack секция)

Этап 4 (Новые языки)         ← зависит от Этапа 0
  └── 04_NEW_LANGUAGES
```
