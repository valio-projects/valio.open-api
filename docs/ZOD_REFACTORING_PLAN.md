# План рефакторинга ZodTypeMapper и ZodSchemaRenderer

## Цель
Убрать генерацию строк типа "z.array()", "z.object()" и т.д. из ZodTypeMapper и перенести её в ZodSchemaRenderer.

## Принцип разделения ответственности
- **ZodTypeMapper**: Маппит OpenAPI схемы в структурированное представление (ZodSchemaExpression)
- **ZodSchemaRenderer**: Рендерит ZodSchemaExpression в строки TypeScript кода

## Выполнено
1. ✅ Создана модель `ZodSchemaExpression` и её варианты:
   - `ZodReferenceExpression` - ссылка на схему
   - `ZodLazyExpression` - циклическая ссылка
   - `ZodPrimitiveExpression` - примитивные типы
   - `ZodObjectExpression` - объекты
   - `ZodArrayExpression` - массивы
   - `ZodUnionExpression` - union (oneOf/anyOf)
   - `ZodIntersectionExpression` - intersection (allOf)
   - `ZodRecordExpression` - словари
   - `ZodEnumExpression` - enum
   - `ZodValidationConstraints` - ограничения валидации

2. ✅ Обновлен `ZodSchemaMappingResult` для использования `ZodSchemaExpression`

## Осталось сделать

### 1. Рефакторинг ZodTypeMapper
- [ ] Создать вспомогательные методы для создания выражений:
  - `CreatePrimitiveExpression()` - создание примитивных типов
  - `CreateReferenceExpression()` - создание ссылок
  - `CreateLazyExpression()` - создание lazy ссылок
  - `CreateObjectExpression()` - создание объектов
  - `CreateArrayExpression()` - создание массивов
  - `CreateUnionExpression()` - создание union
  - `CreateIntersectionExpression()` - создание intersection
  - `CreateRecordExpression()` - создание словарей
  - `CreateEnumExpression()` - создание enum
  - `CreateValidationConstraints()` - создание ограничений

- [ ] Рефакторить `MapToZodSchemaWithDependencies()`:
  - Заменить все `result.ZodSchema = "..."` на `result.Expression = Create...Expression()`
  - Убрать конкатенацию строк для валидаций

- [ ] Рефакторить `MapAllOfToZodSchema()`:
  - Создавать `ZodIntersectionExpression` или `ZodObjectExpression` с объединенными свойствами
  - Убрать генерацию строк `z.object({...})`

- [ ] Рефакторить `MapOneOfToZodSchema()` и `MapAnyOfToZodSchema()`:
  - Создавать `ZodUnionExpression`
  - Убрать генерацию строк `z.union([...])`

### 2. Рефакторинг ZodSchemaRenderer
- [ ] Создать метод `RenderExpression(ZodSchemaExpression expression)`:
  - Обрабатывать все типы выражений
  - Генерировать соответствующие строки

- [ ] Обновить `CreateZodModelTemplateData()`:
  - Использовать `result.Expression` вместо `result.ZodSchema`
  - Рендерить выражения в строки через `RenderExpression()`

- [ ] Обновить `GeneratePropertySchema()`:
  - Использовать `RenderExpression()` для рендеринга схем свойств

### 3. Обновление зависимостей
- [ ] Обновить все места, где используется `result.ZodSchema`:
  - `ZodSchemaRenderer.CreateZodProperty()` - использовать `RenderExpression()`
  - Проверить другие места использования

### 4. Тестирование
- [ ] Протестировать генерацию простых типов
- [ ] Протестировать генерацию объектов
- [ ] Протестировать генерацию массивов
- [ ] Протестировать генерацию union/intersection
- [ ] Протестировать генерацию enum
- [ ] Протестировать циклические ссылки
- [ ] Протестировать валидации

## Преимущества рефакторинга
1. **Разделение ответственности**: Маппер работает с метаданными, рендерер - со строками
2. **Тестируемость**: Можно тестировать маппинг и рендеринг отдельно
3. **Расширяемость**: Легко добавить новые типы выражений или способы рендеринга
4. **Поддерживаемость**: Код становится более структурированным и понятным
5. **Типобезопасность**: Работа с объектами вместо строк
