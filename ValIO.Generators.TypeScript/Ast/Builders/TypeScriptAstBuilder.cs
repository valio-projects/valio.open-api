using System.Collections.Immutable;
using ValIO.Core.Ast.Builders;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.Ast.Nodes.Http;
using ValIO.Core.Ast.Nodes.Statements;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.Interfaces;
using ValIO.Core.IR;
using ValIO.Core.IR.Enums;
using ValIO.Core.IR.Types;
using ValIO.Core.Models;
using ValIO.Core.Services;
using ValIO.Generators.TypeScript.Ast.Nodes;
using ValIO.Generators.TypeScript.Ast.Nodes.Expressions;
using ValIO.Generators.TypeScript.Models;
using ValIO.Generators.TypeScript.Axios;

namespace ValIO.Generators.TypeScript.Ast.Builders;

/// <summary>
/// Построитель AST для TypeScript (Builder Pattern)
/// Использует IR для построения AST узлов
/// </summary>
public class TypeScriptAstBuilder : ITypeScriptAstBuilder
{
    /// <summary>
    /// Категория файла для определения пути импорта
    /// </summary>
    private enum FileCategory
    {
        Models,
        Clients,
        Schemas
    }
    
    private readonly INamingConverter _namingConverter;
    private readonly ITypeMapper _typeMapper;
    private readonly HttpAstBuilder _httpBuilder;
    private readonly TypeScriptGenerationOptions? _options;
    
    /// <summary>
    /// Конструктор
    /// </summary>
    /// <param name="namingConverter">Конвертер имен</param>
    /// <param name="typeMapper">Маппер типов</param>
    /// <param name="options">Опции генерации TypeScript (опционально)</param>
    public TypeScriptAstBuilder(
        INamingConverter namingConverter, 
        ITypeMapper typeMapper,
        TypeScriptGenerationOptions? options = null)
    {
        _namingConverter = namingConverter ?? throw new ArgumentNullException(nameof(namingConverter));
        _typeMapper = typeMapper ?? throw new ArgumentNullException(nameof(typeMapper));
        _options = options;
        _httpBuilder = new HttpAstBuilder();
    }
    
    public TypeAliasDeclarationNode BuildTypeAlias(string name, TypeNode type)
    {
        return new TypeAliasDeclarationNode(
            name: new IdentifierNode(name),
            type: type);
    }
    
    public TypeNode BuildType(TypeInfo typeInfo)
    {
        if (typeInfo.IsCollection)
        {
            var elementType = BuildTypeFromBaseType(typeInfo.GenericArguments.FirstOrDefault() ?? TypeInfo.Simple(typeInfo.BaseType));
            return new ArrayTypeNode(elementType, isNullable: typeInfo.IsNullable, isOptional: typeInfo.IsOptional);
        }
        
        if (typeInfo.IsDictionary)
        {
            var keyType = BuildTypeFromBaseType(typeInfo.GenericArguments.ElementAtOrDefault(0) ?? TypeInfo.Simple("string"));
            var valueType = BuildTypeFromBaseType(typeInfo.GenericArguments.ElementAtOrDefault(1) ?? TypeInfo.Simple("object"));
            return new DictionaryTypeNode(keyType, valueType, isNullable: typeInfo.IsNullable, isOptional: typeInfo.IsOptional);
        }
        
        if (!string.IsNullOrEmpty(typeInfo.BaseType) && typeInfo.GenericArguments.Count > 0)
        {
            var genericArgs = typeInfo.GenericArguments.Select(BuildType).ToImmutableList();
            var genericType = new GenericTypeNode(
                name: new IdentifierNode(typeInfo.BaseType),
                typeArguments: genericArgs,
                isNullable: typeInfo.IsNullable,
                isOptional: typeInfo.IsOptional);
            return genericType;
        }
        
        return BuildTypeFromBaseType(typeInfo);
    }
    
    public LiteralExpressionNode BuildLiteral(object? value, TypeInfo typeInfo)
    {
        var literalKind = value switch
        {
            string => LiteralKind.String,
            int or long or short or byte => LiteralKind.Number,
            float or double or decimal => LiteralKind.Number,
            bool => LiteralKind.Boolean,
            null => LiteralKind.Null,
            _ => LiteralKind.String
        };
        
        return new LiteralExpressionNode(value ?? (object)"", literalKind);
    }
    
    // Приватные вспомогательные методы
    
    private IReadOnlyList<ImportNode> BuildImports(
        IReadOnlyList<string> dependencies, 
        string? importPath,
        FileOrganization? fileOrganization = null,
        FileCategory? currentFileCategory = null,
        string? currentModelOrEnumName = null,
        string? currentOperationName = null)
    {
        if (dependencies.Count == 0)
        {
            return ImmutableList<ImportNode>.Empty;
        }
        
        var importsByFile = new Dictionary<string, List<string>>();
        var modelsPath = fileOrganization?.ModelsPath ?? "./models";
        var clientsPath = fileOrganization?.ClientsPath ?? "./api";
        
        var currentBasePath = !string.IsNullOrEmpty(currentModelOrEnumName)
            ? $"{modelsPath}/{GetModelFileName(currentModelOrEnumName)}"
            : !string.IsNullOrEmpty(currentOperationName)
                ? $"{clientsPath}/{GetModelFileName(currentOperationName)}"
                : currentFileCategory switch
                {
                    FileCategory.Models => modelsPath,
                    FileCategory.Clients => clientsPath,
                    FileCategory.Schemas => "./schemas",
                    _ => "./"
                };
        
        foreach (var dependency in dependencies.Distinct().OrderBy(d => d))
        {
            var fileName = GetModelFileName(dependency);
            var dependencyDir = $"{modelsPath}/{fileName}";
            
            string importPathForFile;
            if (!string.IsNullOrEmpty(importPath))
            {
                importPathForFile = $"{importPath}/{fileName}";
            }
            else if (!string.IsNullOrEmpty(currentModelOrEnumName) || !string.IsNullOrEmpty(currentOperationName))
            {
                importPathForFile = CalculateRelativePath(currentBasePath, dependencyDir, fileName);
            }
            else if (currentBasePath == modelsPath)
            {
                importPathForFile = $"./{fileName}/{fileName}";
            }
            else
            {
                importPathForFile = CalculateRelativePath(currentBasePath, dependencyDir, fileName);
            }
            
            importPathForFile = NormalizePath(importPathForFile);
            
            if (!importsByFile.ContainsKey(importPathForFile))
            {
                importsByFile[importPathForFile] = new List<string>();
            }
            
            importsByFile[importPathForFile].Add(dependency);
        }
        
        // Создаем ImportNode для каждой группы
        var imports = new List<ImportNode>();
        foreach (var kvp in importsByFile.OrderBy(kvp => kvp.Key))
        {
            var items = kvp.Value.Select(name => new IdentifierNode(name)).ToImmutableList();
            var importNode = new ImportNode(
                items: items,
                from: kvp.Key,
                kind: ImportKind.Named);
            imports.Add(importNode);
        }
        
        return imports;
    }
    
    /// <summary>
    /// Вычисляет относительный путь между двумя папками
    /// </summary>
    private string CalculateRelativePath(string fromPath, string toPath, string fileName)
    {
        // Нормализуем пути
        fromPath = NormalizePath(fromPath);
        toPath = NormalizePath(toPath);
        
        // Если пути одинаковые, используем относительный путь в той же папке
        if (fromPath == toPath)
        {
            return $"./{fileName}";
        }
        
        // Разбиваем пути на части
        var fromParts = fromPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var toParts = toPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        
        // Находим общий префикс
        int commonLength = 0;
        int minLength = Math.Min(fromParts.Length, toParts.Length);
        while (commonLength < minLength && fromParts[commonLength] == toParts[commonLength])
        {
            commonLength++;
        }
        
        // Вычисляем количество уровней вверх
        int upLevels = fromParts.Length - commonLength;
        
        // Формируем относительный путь
        var relativeParts = new List<string>();
        
        // Добавляем уровни вверх
        for (int i = 0; i < upLevels; i++)
        {
            relativeParts.Add("..");
        }
        
        // Добавляем оставшиеся части пути к целевой папке
        for (int i = commonLength; i < toParts.Length; i++)
        {
            relativeParts.Add(toParts[i]);
        }
        
        // Добавляем имя файла
        relativeParts.Add(fileName);
        
        return string.Join("/", relativeParts);
    }
    
    /// <summary>
    /// Нормализует путь (убирает двойные слеши, заменяет обратные слеши)
    /// </summary>
    private string NormalizePath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return "./";
        
        // Заменяем обратные слеши на прямые
        path = path.Replace("\\", "/");
        
        // Убираем двойные слеши
        while (path.Contains("//"))
        {
            path = path.Replace("//", "/");
        }
        
        // Убираем начальный слеш, если это не абсолютный путь
        if (path.StartsWith("/") && !path.StartsWith("//") && !path.StartsWith("http"))
        {
            path = path.Substring(1);
        }
        
        // Убираем конечный слеш
        if (path.EndsWith("/") && path.Length > 1)
        {
            path = path.Substring(0, path.Length - 1);
        }
        
        return path;
    }
    
    /// <summary>
    /// Получает имя файла для модели или enum'а
    /// </summary>
    private string GetModelFileName(string typeName)
    {
        if (string.IsNullOrEmpty(typeName))
            return string.Empty;
        
        // Преобразуем PascalCase в camelCase для имени файла
        return _namingConverter.ToCamelCase(typeName);
    }
    
    private TypeNode BuildTypeFromBaseType(TypeInfo typeInfo)
    {
        var simpleType = new SimpleTypeNode(
            name: new IdentifierNode(typeInfo.BaseType),
            isPrimitive: typeInfo.IsPrimitive,
            isNullable: typeInfo.IsNullable,
            isOptional: typeInfo.IsOptional);
        return simpleType;
    }
    
    private SimpleTypeNode BuildSimpleType(string typeName)
    {
        return new SimpleTypeNode(new IdentifierNode(typeName), isPrimitive: false);
    }
    
    // IR → AST методы
    
    /// <summary>
    /// Строит TypeNode из IrType
    /// </summary>
    public TypeNode BuildType(IrType irType)
    {
        return irType.Kind switch
        {
            IrTypeKind.Primitive => BuildPrimitiveType((IrPrimitiveType)irType),
            IrTypeKind.ModelReference => BuildModelReference((IrModelReference)irType),
            IrTypeKind.Array => BuildArrayType((IrArrayType)irType),
            IrTypeKind.Dictionary => BuildDictionaryType((IrDictionaryType)irType),
            IrTypeKind.Union => BuildUnionType((IrUnionType)irType),
            IrTypeKind.Intersection => BuildIntersectionType((IrIntersectionType)irType),
            IrTypeKind.Nullable => BuildNullableType((IrNullableType)irType),
            IrTypeKind.Optional => BuildOptionalType((IrOptionalType)irType),
            _ => throw new NotSupportedException($"IrTypeKind {irType.Kind} is not supported")
        };
    }
    
    /// <summary>
    /// Строит SourceFile из IrModel
    /// </summary>
    public SourceFileNode BuildSourceFile(IrModel model, string? importPath = null, FileOrganization? fileOrganization = null)
    {
        var dependencies = ExtractDependencies(model);
        dependencies = dependencies.Where(d => d != model.Name).ToList();
        var imports = BuildImports(dependencies, importPath, fileOrganization, FileCategory.Models, model.Name);
        var declarations = new[] { (DeclarationNode)BuildInterface(model) };
        var exports = declarations.Select(d => new ExportNode(d, ExportKind.Named)).ToImmutableList();
        
        return new SourceFileNode(
            imports: imports,
            exports: exports,
            declarations: declarations);
    }
    
    /// <summary>
    /// Строит InterfaceDeclarationNode из IrModel
    /// </summary>
    public InterfaceDeclarationNode BuildInterface(IrModel model)
    {
        var name = new IdentifierNode(model.Name);
        var comment = !string.IsNullOrEmpty(model.Description)
            ? new CommentNode(model.Description, CommentKind.JSDoc)
            : null;
        
        var properties = model.Properties.Select(BuildProperty).ToList<DeclarationNode>();
        
        var baseTypes = ImmutableList<TypeNode>.Empty;
        if (model.Composition?.Kind == IrCompositionKind.AllOf && model.Composition.Types.Count > 0)
        {
            // AllOf в TypeScript - это intersection типов
            baseTypes = model.Composition.Types
                .Where(t => t is IrModelReference)
                .Select(t => BuildType(t))
                .ToImmutableList();
        }
        
        return new InterfaceDeclarationNode(
            name: name,
            accessModifier: AccessModifier.Public,
            baseTypes: baseTypes,
            members: properties.ToImmutableList(),
            documentation: comment);
    }
    
    /// <summary>
    /// Строит PropertyDeclarationNode из IrProperty
    /// </summary>
    public PropertyDeclarationNode BuildProperty(IrProperty property)
    {
        var name = new IdentifierNode(property.Name);
        var type = BuildType(property.Type);
        var comment = !string.IsNullOrEmpty(property.Description)
            ? new CommentNode(property.Description, CommentKind.JSDoc)
            : null;
        
        var defaultValue = property.Constraints?.DefaultValue != null
            ? BuildLiteral(property.Constraints.DefaultValue, InferTypeInfo(property.Type))
            : null;
        
        return new PropertyDeclarationNode(
            name: name,
            type: type,
            isRequired: property.IsRequired,
            accessor: PropertyAccessorKind.Get,
            defaultValue: defaultValue,
            documentation: comment,
            constraints: property.Constraints);
    }
    
    /// <summary>
    /// Строит SourceFile из IrEnum
    /// </summary>
    public SourceFileNode BuildSourceFile(IrEnum enumModel, string? importPath = null, FileOrganization? fileOrganization = null)
    {
        var imports = BuildImports(Array.Empty<string>(), importPath, fileOrganization, FileCategory.Models, enumModel.Name);
        var declarations = BuildEnumDeclaration(enumModel);
        var exports = declarations.Select(d => new ExportNode(d, ExportKind.Named)).ToImmutableList();
        
        return new SourceFileNode(
            imports: imports,
            exports: exports,
            declarations: declarations);
    }
    
    /// <summary>
    /// Строит DeclarationNode из IrEnum (может быть TypeAlias для union или enum)
    /// Также генерирует массив значений enum'а
    /// </summary>
    private IReadOnlyList<DeclarationNode> BuildEnumDeclaration(IrEnum enumModel)
    {
        var declarations = new List<DeclarationNode>();
        
        // Генерируем union type или enum
        if (enumModel.BaseType == IrEnumBaseType.String)
        {
            var unionType = BuildEnumUnionType(enumModel);
            declarations.Add(BuildTypeAlias(enumModel.Name, unionType));
        }
        else
        {
            // TODO: Реализовать построение TypeScript enum для числовых значений
            // Пока возвращаем union type
            var numericUnionType = BuildEnumUnionType(enumModel);
            declarations.Add(BuildTypeAlias(enumModel.Name, numericUnionType));
        }
        
        // Генерируем массив значений enum'а
        var valuesArray = BuildEnumValuesArray(enumModel);
        declarations.Add(valuesArray);
        
        // Генерируем helper функцию для проверки значений enum'а
        var typeGuardFunction = BuildEnumTypeGuard(enumModel);
        declarations.Add(typeGuardFunction);
        
        return declarations;
    }
    
    /// <summary>
    /// Строит VariableDeclarationNode с массивом значений enum'а
    /// Например: export const ChessboardProjectPropertyTypeValues = ["Property", "Apartment", ...] as const;
    /// </summary>
    private VariableDeclarationNode BuildEnumValuesArray(IrEnum enumModel)
    {
        // Создаем массив литералов для значений enum'а
        var elements = enumModel.Values.Select(v =>
        {
            // Получаем строковое представление значения
            var valueStr = v.Value?.ToString()?.Trim() ?? "";
            
            if (enumModel.BaseType == IrEnumBaseType.String)
            {
                // Для строковых enum'ов создаем строковый литерал
                return new LiteralExpressionNode(valueStr, LiteralKind.String) as ExpressionNode;
            }
            else
            {
                // Для числовых enum'ов создаем числовой литерал
                if (long.TryParse(valueStr, out var longValue))
                {
                    return new LiteralExpressionNode(longValue, LiteralKind.Number) as ExpressionNode;
                }
                return new LiteralExpressionNode(valueStr, LiteralKind.String) as ExpressionNode;
            }
        }).ToImmutableList();
        
        // Создаем ArrayLiteralExpressionNode
        var arrayLiteral = new ArrayLiteralExpressionNode(elements);
        
        // Создаем тип массива (readonly array)
        var elementType = enumModel.BaseType == IrEnumBaseType.String
            ? new SimpleTypeNode(new IdentifierNode("string"), isPrimitive: true)
            : new SimpleTypeNode(new IdentifierNode("number"), isPrimitive: true);
        var arrayType = new ArrayTypeNode(elementType, isReadOnly: true);
        
        // Имя переменной: EnumName + "Values"
        var variableName = $"{enumModel.Name}Values";
        
        // Создаем VariableDeclarationNode с const, export и "as const satisfies" assertion
        // Используем "as const satisfies readonly EnumType[]" для максимально строгой типизации
        var enumTypeNode = new SimpleTypeNode(new IdentifierNode(enumModel.Name), isPrimitive: false);
        var satisfiesType = new ArrayTypeNode(enumTypeNode, isReadOnly: true);
        
        return new VariableDeclarationNode(
            name: new IdentifierNode(variableName),
            declarationKind: VariableDeclarationKind.Const,
            type: arrayType,
            initializer: arrayLiteral,
            isExported: true,
            asConstSatisfies: true,
            satisfiesType: satisfiesType);
    }
    
    private UnionTypeNode BuildEnumUnionType(IrEnum enumModel)
    {
        var types = enumModel.Values.Select(v =>
        {
            // Получаем строковое представление значения
            // v.Value может быть IOpenApiAny или другим типом, поэтому используем ToString()
            var valueStr = v.Value?.ToString()?.Trim() ?? "";
            
            if (enumModel.BaseType == IrEnumBaseType.String)
            {
                // Для строковых enum'ов создаем литеральный тип
                // Храним чистое значение в metadata, без экранирования и кавычек
                return new SimpleTypeNode(
                    new IdentifierNode("string"), // Имя типа для идентификации
                    isPrimitive: true,
                    metadata: new Dictionary<string, object> 
                    { 
                        { "IsLiteralString", true },
                        { "LiteralValue", valueStr } // Храним чистое значение
                    }) as TypeNode;
            }
            
            // Для числовых enum'ов создаем числовой литерал
            if (enumModel.BaseType == IrEnumBaseType.Integer)
            {
                // Пытаемся преобразовать в число
                if (long.TryParse(valueStr, out var longValue))
                {
                    return new SimpleTypeNode(
                        new IdentifierNode("number"), // Имя типа для идентификации
                        isPrimitive: true,
                        metadata: new Dictionary<string, object> 
                        { 
                            { "IsLiteralNumber", true },
                            { "LiteralValue", longValue } // Храним чистое значение
                        }) as TypeNode;
                }
            }
            
            // Fallback - используем строку (не должно происходить для правильно определенных enum'ов)
            return BuildSimpleType("string");
        }).ToImmutableList();
        
        return new UnionTypeNode(types);
    }
    
    /// <summary>
    /// Строит helper функцию для проверки значений enum'а (type guard)
    /// Например: export const isChangeSource = (value: unknown): value is ChangeSource => ChangeSourceValues.includes(value as ChangeSource);
    /// </summary>
    private FunctionDeclarationNode BuildEnumTypeGuard(IrEnum enumModel)
    {
        // Имя функции: is{EnumName}
        var functionName = $"is{enumModel.Name}";
        
        // Параметр: value: unknown
        var valueParameter = new ParameterDeclarationNode(
            name: new IdentifierNode("value"),
            type: new SimpleTypeNode(new IdentifierNode("unknown"), isPrimitive: true));
        
        // Возвращаемый тип: value is EnumType (type predicate)
        var enumTypeNode = new SimpleTypeNode(new IdentifierNode(enumModel.Name), isPrimitive: false);
        
        // Тело функции: ChangeSourceValues.includes(value as ChangeSource)
        // 1. ChangeSourceValues - идентификатор массива значений
        var valuesArrayIdentifier = new IdentifierExpressionNode(new IdentifierNode($"{enumModel.Name}Values"));
        
        // 2. .includes - доступ к методу
        var includesMember = new MemberAccessExpressionNode(
            @object: valuesArrayIdentifier,
            member: new IdentifierNode("includes"));
        
        // 3. value as ChangeSource - приведение типа
        var valueIdentifier = new IdentifierExpressionNode(new IdentifierNode("value"));
        var castExpression = new CastExpressionNode(
            targetType: enumTypeNode,
            expression: valueIdentifier,
            useAsOperator: true); // Используем "as" оператор для TypeScript
        
        // 4. .includes(value as ChangeSource) - вызов метода
        var includesCall = new CallExpressionNode(
            target: includesMember,
            arguments: new[] { castExpression as ExpressionNode }.ToImmutableList());
        
        // 5. return ChangeSourceValues.includes(value as ChangeSource);
        var returnStatement = new ReturnStatementNode(value: includesCall);
        
        // 6. Блок с return statement
        var body = new BlockNode(statements: new[] { returnStatement }.ToImmutableList());
        
        // Создаем функцию как arrow function
        return new FunctionDeclarationNode(
            name: new IdentifierNode(functionName),
            returnType: enumTypeNode,
            parameters: new[] { valueParameter }.ToImmutableList(),
            body: body,
            isArrowFunction: true,
            documentation: new CommentNode($"Type guard function to check if a value is a valid {enumModel.Name}."),
            isExported: true,
            isTypePredicate: true,
            typePredicateParameterName: "value");
    }
    
    // Приватные вспомогательные методы для IR
    
    private TypeNode BuildPrimitiveType(IrPrimitiveType primitiveType)
    {
        var typeName = MapPrimitiveToTypeScript(primitiveType.PrimitiveKind);
        return new SimpleTypeNode(
            name: new IdentifierNode(typeName),
            isPrimitive: true,
            isNullable: false,
            isOptional: false);
    }
    
    private TypeNode BuildModelReference(IrModelReference modelReference)
    {
        return new SimpleTypeNode(
            name: new IdentifierNode(modelReference.ModelName),
            isPrimitive: false,
            isNullable: false,
            isOptional: false);
    }
    
    private TypeNode BuildArrayType(IrArrayType arrayType)
    {
        var elementType = BuildType(arrayType.ElementType);
        return new ArrayTypeNode(elementType, isNullable: false, isOptional: false);
    }
    
    private TypeNode BuildDictionaryType(IrDictionaryType dictionaryType)
    {
        var keyType = BuildType(dictionaryType.KeyType);
        var valueType = BuildType(dictionaryType.ValueType);
        return new DictionaryTypeNode(keyType, valueType, isNullable: false, isOptional: false);
    }
    
    private TypeNode BuildUnionType(IrUnionType unionType)
    {
        var types = unionType.Types.Select(BuildType).ToImmutableList();
        return new UnionTypeNode(types);
    }
    
    private TypeNode BuildIntersectionType(IrIntersectionType intersectionType)
    {
        var types = intersectionType.Types.Select(BuildType).ToImmutableList();
        return new IntersectionTypeNode(types);
    }
    
    private TypeNode BuildNullableType(IrNullableType nullableType)
    {
        var innerType = BuildType(nullableType.InnerType);
        return new NullableTypeNode(innerType);
    }
    
    private TypeNode BuildOptionalType(IrOptionalType optionalType)
    {
        var innerType = BuildType(optionalType.InnerType);
        return new OptionalTypeNode(innerType);
    }
    
    private string MapPrimitiveToTypeScript(IrPrimitiveKind primitiveKind)
    {
        return primitiveKind switch
        {
            IrPrimitiveKind.String => "string",
            IrPrimitiveKind.Number => "number",
            IrPrimitiveKind.Integer => "number",
            IrPrimitiveKind.Boolean => "boolean",
            IrPrimitiveKind.DateTime => "string", // ISO string в TypeScript
            IrPrimitiveKind.Date => "string",
            IrPrimitiveKind.Time => "string",
            IrPrimitiveKind.Binary => "Blob",
            IrPrimitiveKind.ByteArray => "Uint8Array",
            IrPrimitiveKind.Uuid => "string",
            IrPrimitiveKind.Any => "any",
            IrPrimitiveKind.Void => "void",
            IrPrimitiveKind.File => "Blob",
            _ => "unknown"
        };
    }
    
    private IReadOnlyList<string> ExtractDependencies(IrModel model)
    {
        var dependencies = new HashSet<string>();
        
        foreach (var property in model.Properties)
        {
            ExtractDependenciesFromType(property.Type, dependencies);
        }
        
        if (model.Composition != null)
        {
            foreach (var type in model.Composition.Types)
            {
                ExtractDependenciesFromType(type, dependencies);
            }
        }
        
        return dependencies.ToList();
    }
    
    private void ExtractDependenciesFromType(IrType type, HashSet<string> dependencies)
    {
        switch (type)
        {
            case IrModelReference modelRef:
                dependencies.Add(modelRef.ModelName);
                break;
            case IrArrayType arrayType:
                ExtractDependenciesFromType(arrayType.ElementType, dependencies);
                break;
            case IrDictionaryType dictType:
                ExtractDependenciesFromType(dictType.KeyType, dependencies);
                ExtractDependenciesFromType(dictType.ValueType, dependencies);
                break;
            case IrUnionType unionType:
                foreach (var t in unionType.Types)
                    ExtractDependenciesFromType(t, dependencies);
                break;
            case IrIntersectionType intersectionType:
                foreach (var t in intersectionType.Types)
                    ExtractDependenciesFromType(t, dependencies);
                break;
            case IrNullableType nullableType:
                ExtractDependenciesFromType(nullableType.InnerType, dependencies);
                break;
            case IrOptionalType optionalType:
                ExtractDependenciesFromType(optionalType.InnerType, dependencies);
                break;
        }
    }
    
    private TypeInfo InferTypeInfo(IrType irType)
    {
        // Простая реализация для определения TypeInfo из IrType
        // Используется только для BuildLiteral
        return irType.Kind switch
        {
            IrTypeKind.Primitive => TypeInfo.Simple(MapPrimitiveToTypeScript(((IrPrimitiveType)irType).PrimitiveKind), isPrimitive: true, language: GenerationLanguage.TypeScript),
            IrTypeKind.ModelReference => TypeInfo.Simple(((IrModelReference)irType).ModelName, language: GenerationLanguage.TypeScript),
            _ => TypeInfo.Simple("unknown", language: GenerationLanguage.TypeScript)
        };
    }
    
    /// <summary>
    /// Строит FunctionDeclarationNode из IrOperation
    /// </summary>
    public FunctionDeclarationNode BuildFunction(IrOperation operation, bool isExported = false)
    {
        var name = new IdentifierNode(operation.MethodName);
        
        var returnType = ExtractReturnType(operation.Responses);
        
        var comment = !string.IsNullOrEmpty(operation.Description)
            ? new CommentNode(operation.Description, CommentKind.JSDoc)
            : !string.IsNullOrEmpty(operation.Summary)
                ? new CommentNode(operation.Summary, CommentKind.JSDoc)
                : null;
        
        var parameters = BuildFunctionParameters(operation);
        var body = BuildFunctionBody(operation);
        
        return new FunctionDeclarationNode(
            name: name,
            returnType: returnType,
            parameters: parameters,
            body: body,
            isArrowFunction: false,
            isAsync: true,
            documentation: comment,
            isExported: isExported);
    }
    
    /// <summary>
    /// Строит список SourceFile из IrDocument (все модели, enum'ы и клиенты)
    /// </summary>
    public IReadOnlyList<SourceFileNode> BuildSourceFiles(IrDocument document, string? importPath = null, FileOrganization? fileOrganization = null)
    {
        var sourceFiles = new List<SourceFileNode>();
        
        // Генерируем модели в топологическом порядке
        var orderedModelNames = document.TypeDependencies.TopologicalOrder;
        var modelsByName = document.Models.ToDictionary(m => m.Name);
        
        foreach (var modelName in orderedModelNames)
        {
            if (modelsByName.TryGetValue(modelName, out var model))
            {
                sourceFiles.Add(BuildSourceFile(model, importPath, fileOrganization));
            }
        }
        
        // Добавляем модели, которые не были в топологическом порядке
        foreach (var model in document.Models)
        {
            if (!orderedModelNames.Contains(model.Name))
            {
                sourceFiles.Add(BuildSourceFile(model, importPath, fileOrganization));
            }
        }
        
        // Генерируем enum'ы
        foreach (var enumModel in document.Enums)
        {
            sourceFiles.Add(BuildSourceFile(enumModel, importPath, fileOrganization));
        }
        
        // Генерируем клиенты — по одному SourceFile на каждую операцию (endpoints/operationName/operationName.ts)
        foreach (var client in document.Clients)
        {
            foreach (var operation in client.Operations)
            {
                sourceFiles.Add(BuildSourceFileForOperation(operation, importPath, fileOrganization));
            }
        }
        
        return sourceFiles;
    }
    
    /// <summary>
    /// Строит SourceFile из IrClient (один файл со всеми операциями)
    /// </summary>
    public SourceFileNode BuildSourceFile(IrClient client, string? importPath = null, FileOrganization? fileOrganization = null)
    {
        var imports = BuildImports(ExtractDependencies(client), importPath, fileOrganization, FileCategory.Clients);
        var declarations = client.Operations.Select(op => (DeclarationNode)BuildFunction(op)).ToList();
        var exports = declarations.Select(d => new ExportNode(d, ExportKind.Named)).ToImmutableList();
        
        return new SourceFileNode(
            imports: imports,
            exports: exports,
            declarations: declarations);
    }
    
    /// <summary>
    /// Строит SourceFile для одной операции (endpoints/operationName/operationName.ts)
    /// </summary>
    public SourceFileNode BuildSourceFileForOperation(IrOperation operation, string? importPath = null, FileOrganization? fileOrganization = null)
    {
        var client = new IrClient { Name = "Temp", Operations = new[] { operation } };
        var imports = BuildImports(ExtractDependencies(client), importPath, fileOrganization, FileCategory.Clients, currentOperationName: operation.MethodName);
        var declarations = new List<DeclarationNode> { BuildFunction(operation, isExported: true) };
        var exports = declarations.Select(d => new ExportNode(d, ExportKind.Named)).ToImmutableList();
        
        return new SourceFileNode(
            imports: imports,
            exports: exports,
            declarations: declarations);
    }
    
    // Приватные вспомогательные методы для IR операций
    
    private IReadOnlyList<ParameterDeclarationNode> BuildFunctionParameters(IrOperation operation)
    {
        var parameters = new List<ParameterDeclarationNode>();
        
        // Path parameters
        foreach (var param in operation.PathParameters)
        {
            var type = BuildType(param.Type);
            parameters.Add(new ParameterDeclarationNode(
                name: new IdentifierNode(param.Name),
                type: type,
                isRequired: true));
        }
        
        // Query parameters
        foreach (var param in operation.QueryParameters)
        {
            var type = BuildType(param.Type);
            parameters.Add(new ParameterDeclarationNode(
                name: new IdentifierNode(param.Name),
                type: type,
                isRequired: param.IsRequired));
        }
        
        // Header parameters
        foreach (var param in operation.HeaderParameters)
        {
            var type = BuildType(param.Type);
            parameters.Add(new ParameterDeclarationNode(
                name: new IdentifierNode(param.Name),
                type: type,
                isRequired: param.IsRequired));
        }
        
        // Request body
        if (operation.RequestBody != null)
        {
            var bodyType = BuildType(operation.RequestBody.Type);
            parameters.Add(new ParameterDeclarationNode(
                name: new IdentifierNode("request"),
                type: bodyType,
                isRequired: operation.RequestBody.IsRequired));
        }
        
        return parameters.ToImmutableList();
    }
    
    /// <summary>
    /// Строит тело функции из IrOperation
    /// </summary>
    /// <param name="operation">IR операция</param>
    /// <returns>AST узел блока с statements или null для type-only функции</returns>
    private BlockNode? BuildFunctionBody(IrOperation operation)
    {
        // Если HttpClientLibrary = Axios, используем AxiosAstBuilder для построения Axios вызовов
        if (_options?.HttpClientLibrary == HttpClientLibrary.Axios)
        {
            return BuildAxiosFunctionBody(operation);
        }
        
        // Для Fetch или других библиотек можно добавить соответствующую реализацию
        // Пока возвращаем null для type-only функции
        return null;
    }
    
    /// <summary>
    /// Строит тело функции с Axios вызовами
    /// </summary>
    /// <param name="operation">IR операция</param>
    /// <returns>AST узел блока с statements</returns>
    private BlockNode BuildAxiosFunctionBody(IrOperation operation)
    {
        var statements = new List<StatementNode>();
        
        // Используем AxiosAstBuilder для построения Axios вызова
        var axiosBuilder = new Axios.Ast.Builders.AxiosAstBuilder(
            _namingConverter,
            BuildType);
        
        var axiosCall = axiosBuilder.BuildAxiosCall(operation);
        
        // Определяем тип возвращаемого значения
        var returnType = ExtractReturnType(operation.Responses);
        
        // Если есть успешный ответ, обрабатываем response.data
        if (returnType is not SimpleTypeNode { Name.Name: "void" })
        {
            // const response = await axios(...)
            var responseVar = new VariableDeclarationStatementNode(
                name: new IdentifierNode("response"),
                type: null, // TypeScript может вывести тип
                initializer: new AwaitExpressionNode(axiosCall));
            statements.Add(responseVar);
            
            // return response.data
            var dataAccess = new MemberAccessExpressionNode(
                @object: new IdentifierExpressionNode(new IdentifierNode("response")),
                member: new IdentifierNode("data"));
            statements.Add(new ReturnStatementNode(dataAccess));
        }
        else
        {
            // Для void просто вызываем axios без возврата
            statements.Add(new ExpressionStatementNode(
                new AwaitExpressionNode(axiosCall)));
        }
        
        return new BlockNode(statements.ToImmutableList());
    }
    
    private TypeNode ExtractReturnType(IReadOnlyList<IrResponse> responses)
    {
        // Ищем первый успешный ответ (200-299)
        var successResponse = responses.FirstOrDefault(r =>
        {
            if (int.TryParse(r.StatusCode, out var code))
                return code >= 200 && code < 300;
            return false;
        });
        
        if (successResponse?.Type != null)
        {
            return BuildType(successResponse.Type);
        }
        
        // Если нет успешного ответа, возвращаем void
        return new SimpleTypeNode(
            name: new IdentifierNode("void"),
            isPrimitive: true,
            isNullable: false,
            isOptional: false);
    }
    
    private IReadOnlyList<string> ExtractDependencies(IrClient client)
    {
        var dependencies = new HashSet<string>();
        
        foreach (var operation in client.Operations)
        {
            // Зависимости из параметров
            foreach (var param in operation.PathParameters.Concat(operation.QueryParameters).Concat(operation.HeaderParameters))
            {
                ExtractDependenciesFromType(param.Type, dependencies);
            }
            
            // Зависимости из request body
            if (operation.RequestBody != null)
            {
                ExtractDependenciesFromType(operation.RequestBody.Type, dependencies);
            }
            
            // Зависимости из responses
            foreach (var response in operation.Responses)
            {
                if (response.Type != null)
                {
                    ExtractDependenciesFromType(response.Type, dependencies);
                }
            }
        }
        
        return dependencies.ToList();
    }
}
