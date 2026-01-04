# Анализ обработки allOf, oneOf, anyOf и предложения по рефакторингу

## Текущее состояние

### 1. Обработка allOf/oneOf/anyOf в разных компонентах

#### ModelExtractor (ValIO.Core)
- **Назначение**: Извлечение моделей из OpenAPI схем
- **Обработка allOf**: 
  - Объединяет свойства из всех под-схем
  - Определяет базовый класс (если есть ссылка на компонент)
  - Обрабатывает inline схемы в allOf
  - Объединяет required свойства
- **Результат**: `ModelTemplateData` с объединенными свойствами

#### CSharpTypeMapper (ValIO.Generators.CSharp)
- **Назначение**: Маппинг типов для C#
- **Обработка allOf**: 
  - Ищет базовый класс (первая ссылка на компонент)
  - Возвращает `TypeInfo` с базовым типом (наследование)
- **Обработка oneOf/anyOf**: 
  - Ищет общий базовый тип
  - Возвращает `TypeInfo` с общим типом или object

#### TypeScriptTypeMapper (ValIO.Generators.TypeScript)
- **Назначение**: Маппинг типов для TypeScript
- **Обработка allOf**: 
  - Собирает типы из всех под-схем
  - Возвращает первый тип (упрощенно, intersection не поддерживается в TypeInfo)
- **Обработка oneOf/anyOf**: 
  - Собирает типы из всех под-схем
  - Возвращает первый тип (упрощенно, union не поддерживается в TypeInfo)

#### ZodTypeMapper (ValIO.Generators.TypeScript.Zod)
- **Назначение**: Генерация Zod схем
- **Обработка allOf**: 
  - Объединяет свойства из всех под-схем
  - Генерирует `z.object({...})` с объединенными свойствами
  - Обрабатывает зависимости
- **Обработка oneOf/anyOf**: 
  - Генерирует `z.union([...])` из всех под-схем
  - Обрабатывает зависимости

#### ZodSchemaRenderer (ValIO.Generators.TypeScript.Zod)
- **Назначение**: Рендеринг Zod схем в TypeScript код
- **Обработка allOf**: 
  - Получает оригинальные схемы свойств (до разрешения)
  - Объединяет свойства из allOf и из самой схемы
  - Генерирует TypeScript интерфейсы для рекурсивных схем

## Проблемы и дублирование

### 1. Дублирование логики определения ссылок на компоненты

**Места дублирования:**
- `ZodTypeMapper.cs` (строки 48-61, 143-148, 354-367)
- `ZodSchemaRenderer.cs` (строки 154-167)
- `ModelExtractor.cs` (строки 135-145)

**Текущий код:**
```csharp
string? referencedSchemaName = null;
if (schema is IOpenApiReferenceHolder<OpenApiSchema, IOpenApiSchema, JsonSchemaReference> refHolder)
{
    var reference = refHolder.Reference;
    if (reference != null && !string.IsNullOrEmpty(reference.Id))
    {
        referencedSchemaName = reference.Id;
    }
}

if (string.IsNullOrEmpty(referencedSchemaName))
{
    referencedSchemaName = schemaResolver.GetSchemaName(schema, string.Empty);
}
```

**Решение**: Использовать `BaseTypeMapper.ResolveSchemaReference` или создать утилиту в `ValIO.Core.Utilities`

### 2. Дублирование логики получения оригинальных Properties

**Места дублирования:**
- `ZodSchemaRenderer.cs` (строки 179-189, 210-218, 273-284)
- `ModelExtractor.cs` (использует `resolvedSchema.Properties`, но для Zod нужны оригинальные)

**Текущий код:**
```csharp
IDictionary<string, IOpenApiSchema>? originalProperties = null;
if (schema is OpenApiSchema openApiSchema && openApiSchema.Properties != null)
{
    originalProperties = openApiSchema.Properties;
}
else if (resolvedSchema.Properties != null)
{
    originalProperties = resolvedSchema.Properties;
}
```

**Решение**: Создать helper метод в `ValIO.Core.Utilities.SchemaHelper` или расширение для `IOpenApiSchema`

### 3. Дублирование логики объединения свойств из allOf

**Места дублирования:**
- `ModelExtractor.cs` (строки 118-237) - объединяет свойства для моделей
- `ZodSchemaRenderer.cs` (строки 142-265) - объединяет свойства для Zod схем
- `ZodTypeMapper.cs` (строки 337-427) - объединяет свойства для генерации Zod схемы

**Различия:**
- `ModelExtractor` создает `PropertyTemplateData` с `TypeInfo`
- `ZodSchemaRenderer` получает оригинальные схемы для передачи в `ZodTypeMapper`
- `ZodTypeMapper` генерирует строки Zod схем

**Решение**: Вынести общую логику объединения свойств в helper, но оставить специфичную обработку в каждом генераторе

### 4. Логика обработки oneOf/anyOf

**Текущее состояние:**
- `CSharpTypeMapper`: Ищет общий базовый тип
- `TypeScriptTypeMapper`: Возвращает первый тип (упрощенно)
- `ZodTypeMapper`: Генерирует `z.union([...])` - правильная реализация

**Проблема**: `TypeScriptTypeMapper` не поддерживает union типы в `TypeInfo`

**Решение**: Расширить `TypeInfo` для поддержки union типов или оставить как есть (TypeScript interfaces не поддерживают union напрямую)

## Предложения по рефакторингу

### 1. Создать утилиту для работы со схемами

**Файл**: `ValIO.Core/Utilities/SchemaHelper.cs`

```csharp
public static class SchemaHelper
{
    /// <summary>
    /// Получает имя схемы из ссылки или из resolver
    /// </summary>
    public static string? GetSchemaName(
        IOpenApiSchema schema, 
        ISchemaResolver schemaResolver,
        INamingConverter? namingConverter = null)
    {
        if (schema is IOpenApiReferenceHolder<OpenApiSchema, IOpenApiSchema, JsonSchemaReference> refHolder)
        {
            var reference = refHolder.Reference;
            if (reference != null && !string.IsNullOrEmpty(reference.Id))
            {
                return namingConverter != null 
                    ? namingConverter.ToPascalCase(reference.Id) 
                    : reference.Id;
            }
        }
        
        var schemaName = schemaResolver.GetSchemaName(schema, string.Empty);
        if (!string.IsNullOrEmpty(schemaName) && schemaName != "object")
        {
            return namingConverter != null 
                ? namingConverter.ToPascalCase(schemaName) 
                : schemaName;
        }
        
        return null;
    }
    
    /// <summary>
    /// Получает оригинальные Properties схемы (до разрешения)
    /// </summary>
    public static IDictionary<string, IOpenApiSchema>? GetOriginalProperties(
        IOpenApiSchema schema,
        IOpenApiSchema resolvedSchema)
    {
        if (schema is OpenApiSchema openApiSchema && openApiSchema.Properties != null)
        {
            return openApiSchema.Properties;
        }
        
        return resolvedSchema.Properties;
    }
    
    /// <summary>
    /// Получает оригинальную схему свойства из allOf под-схемы
    /// </summary>
    public static IOpenApiSchema? GetOriginalPropertySchema(
        IOpenApiSchema subSchema,
        IOpenApiSchema resolvedSubSchema,
        string propertyName)
    {
        if (subSchema is OpenApiSchema openApiSubSchema && openApiSubSchema.Properties != null)
        {
            if (openApiSubSchema.Properties.TryGetValue(propertyName, out var originalPropSchema))
            {
                return originalPropSchema;
            }
        }
        
        if (resolvedSubSchema.Properties != null)
        {
            resolvedSubSchema.Properties.TryGetValue(propertyName, out var propSchema);
            return propSchema;
        }
        
        return null;
    }
    
    /// <summary>
    /// Объединяет required свойства из всех под-схем allOf и из самой схемы
    /// </summary>
    public static HashSet<string> MergeRequiredProperties(
        IList<IOpenApiSchema> allOfSchemas,
        IOpenApiSchema resolvedSchema,
        ISchemaResolver schemaResolver)
    {
        var allRequired = new HashSet<string>();
        
        foreach (var subSchema in allOfSchemas)
        {
            var resolvedSubSchema = schemaResolver.ResolveSchema(subSchema);
            if (resolvedSubSchema.Required != null)
            {
                foreach (var req in resolvedSubSchema.Required)
                {
                    allRequired.Add(req);
                }
            }
        }
        
        if (resolvedSchema.Required != null)
        {
            foreach (var req in resolvedSchema.Required)
            {
                allRequired.Add(req);
            }
        }
        
        return allRequired;
    }
}
```

### 2. Использовать утилиту в генераторах

**ZodTypeMapper.cs:**
```csharp
// Заменить дублирование на:
var referencedSchemaName = SchemaHelper.GetSchemaName(schema, schemaResolver, _namingConverter);
```

**ZodSchemaRenderer.cs:**
```csharp
// Заменить дублирование на:
var referencedSchemaName = SchemaHelper.GetSchemaName(subSchema, schemaResolver);
var originalPropSchema = SchemaHelper.GetOriginalPropertySchema(subSchema, resolvedSubSchema, prop.Key);
var allRequired = SchemaHelper.MergeRequiredProperties(resolvedSchema.AllOf, resolvedSchema, schemaResolver);
```

**ModelExtractor.cs:**
```csharp
// Заменить дублирование на:
var referencedSchemaName = SchemaHelper.GetSchemaName(subSchema, _schemaResolver, _namingConverter);
```

### 3. Вынести общую логику обработки allOf (опционально)

**Файл**: `ValIO.Core/Utilities/AllOfHelper.cs`

```csharp
public static class AllOfHelper
{
    /// <summary>
    /// Извлекает информацию о под-схемах allOf
    /// </summary>
    public class AllOfSubSchemaInfo
    {
        public IOpenApiSchema OriginalSchema { get; set; }
        public IOpenApiSchema ResolvedSchema { get; set; }
        public string? ReferencedSchemaName { get; set; }
        public bool IsComponentReference { get; set; }
    }
    
    public static List<AllOfSubSchemaInfo> ExtractAllOfSubSchemas(
        IList<IOpenApiSchema> allOfSchemas,
        ISchemaResolver schemaResolver,
        INamingConverter? namingConverter = null)
    {
        var result = new List<AllOfSubSchemaInfo>();
        
        foreach (var subSchema in allOfSchemas)
        {
            var resolvedSubSchema = schemaResolver.ResolveSchema(subSchema);
            var referencedSchemaName = SchemaHelper.GetSchemaName(subSchema, schemaResolver, namingConverter);
            
            result.Add(new AllOfSubSchemaInfo
            {
                OriginalSchema = subSchema,
                ResolvedSchema = resolvedSubSchema,
                ReferencedSchemaName = referencedSchemaName,
                IsComponentReference = !string.IsNullOrEmpty(referencedSchemaName)
            });
        }
        
        return result;
    }
}
```

**Использование:**
```csharp
var allOfSubSchemas = AllOfHelper.ExtractAllOfSubSchemas(
    resolvedSchema.AllOf, 
    schemaResolver, 
    _namingConverter);

foreach (var subSchemaInfo in allOfSubSchemas)
{
    // Обработка каждой под-схемы
}
```

## Рекомендации

### Высокий приоритет (сделать сейчас)
1. ✅ Создать `SchemaHelper` с методами для работы со схемами
2. ✅ Заменить дублирование в `ZodTypeMapper` и `ZodSchemaRenderer` на использование `SchemaHelper`
3. ✅ Заменить дублирование в `ModelExtractor` на использование `SchemaHelper`

### Средний приоритет (можно сделать позже)
4. Создать `AllOfHelper` для общей логики обработки allOf
5. Рассмотреть расширение `TypeInfo` для поддержки union/intersection типов

### Низкий приоритет (опционально)
6. Рефакторинг логики объединения свойств (слишком специфично для каждого генератора)

## Выводы

1. **Основное дублирование** - в определении ссылок на компоненты и получении оригинальных Properties
2. **Логика allOf/oneOf/anyOf** - специфична для каждого генератора, но общие части можно вынести
3. **TypeInfo** - не поддерживает union/intersection, что упрощает реализацию в TypeScriptTypeMapper, но ограничивает возможности

## Следующие шаги

1. Создать `ValIO.Core/Utilities/SchemaHelper.cs`
2. Заменить дублирование в генераторах на использование `SchemaHelper`
3. Протестировать изменения
4. Рассмотреть создание `AllOfHelper` при необходимости
