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
using ValIO.Core.IR.Constraints;
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
public partial class TypeScriptAstBuilder : ITypeScriptAstBuilder
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
    /// Транспорт генерируемого endpoint-файла (axios / fetch). Устанавливается в <see cref="BuildSourceFileForOperation"/>.
    /// </summary>
    private TypeScriptEndpointTransport _activeEndpointTransport = TypeScriptEndpointTransport.Axios;
    
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
        
        var declarations = new List<DeclarationNode> { BuildInterface(model) };
        var constraintsDecl = BuildConstraintsDeclaration(model);
        if (constraintsDecl != null)
            declarations.Add(constraintsDecl);
        
        var exports = declarations.Select(d => new ExportNode(d, ExportKind.Named)).ToImmutableList();
        
        return new SourceFileNode(
            imports: imports,
            exports: exports,
            declarations: declarations.ToImmutableList());
    }
    
    /// <summary>
    /// Строит VariableDeclarationNode с const объектом метаданных валидации для Zod схем.
    /// Возвращает null если нет свойств с constraints.
    /// </summary>
    private VariableDeclarationNode? BuildConstraintsDeclaration(IrModel model)
    {
        var propConstraints = model.Properties
            .Where(p => p.Constraints != null && HasAnyConstraint(p.Constraints))
            .Select(p => new
            {
                p.Name,
                Constraints = p.Constraints!
            })
            .ToList();
        
        if (propConstraints.Count == 0)
            return null;
        
        var properties = propConstraints.Select(pc =>
        {
            var c = pc.Constraints;
            var props = new List<PropertyAssignmentNode>();
            if (c.Min.HasValue)
                props.Add(new PropertyAssignmentNode(new IdentifierNode("min"), new LiteralExpressionNode(c.Min.Value, LiteralKind.Number)));
            if (c.Max.HasValue)
                props.Add(new PropertyAssignmentNode(new IdentifierNode("max"), new LiteralExpressionNode(c.Max.Value, LiteralKind.Number)));
            if (c.ExclusiveMin.HasValue)
                props.Add(new PropertyAssignmentNode(new IdentifierNode("exclusiveMin"), new LiteralExpressionNode(c.ExclusiveMin.Value, LiteralKind.Number)));
            if (c.ExclusiveMax.HasValue)
                props.Add(new PropertyAssignmentNode(new IdentifierNode("exclusiveMax"), new LiteralExpressionNode(c.ExclusiveMax.Value, LiteralKind.Number)));
            if (c.MinLength.HasValue)
                props.Add(new PropertyAssignmentNode(new IdentifierNode("minLength"), new LiteralExpressionNode(c.MinLength.Value, LiteralKind.Number)));
            if (c.MaxLength.HasValue)
                props.Add(new PropertyAssignmentNode(new IdentifierNode("maxLength"), new LiteralExpressionNode(c.MaxLength.Value, LiteralKind.Number)));
            if (!string.IsNullOrEmpty(c.Pattern))
                props.Add(new PropertyAssignmentNode(new IdentifierNode("pattern"), new LiteralExpressionNode(c.Pattern, LiteralKind.String)));
            
            return new PropertyAssignmentNode(
                new IdentifierNode(pc.Name),
                new ObjectLiteralExpressionNode(
                    props.ToImmutableList(),
                    preferMultilineFormat: props.Count > 1,
                    asConstAssertion: true));
        }).ToImmutableList();
        
        var initializer = new ObjectLiteralExpressionNode(properties, preferMultilineFormat: true);
        return new VariableDeclarationNode(
            new IdentifierNode($"{model.Name}Constraints"),
            VariableDeclarationKind.Const,
            initializer: initializer,
            isExported: true,
            asConstAssertion: true);
    }
    
    private static bool HasAnyConstraint(IrPropertyConstraints c)
    {
        return c.Min.HasValue || c.Max.HasValue || c.ExclusiveMin.HasValue || c.ExclusiveMax.HasValue
            || c.MinLength.HasValue || c.MaxLength.HasValue || !string.IsNullOrEmpty(c.Pattern);
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
    /// Строит PropertyDeclarationNode из IrProperty.
    /// Для опциональных свойств response-моделей формирует nullish тип (T | null) в AST,
    /// т.к. API может вернуть null или не вернуть свойство.
    /// </summary>
    public PropertyDeclarationNode BuildProperty(IrProperty property)
    {
        var name = new IdentifierNode(property.Name);
        var type = BuildType(property.Type);
        
        // nullish: опциональное свойство → T | null в AST (явно, без костылей в рендерере)
        if (!property.IsRequired && !IsTypeNullable(type))
            type = new NullableTypeNode(type);
        
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
                return new SimpleTypeNode(
                    new IdentifierNode("string"),
                    isPrimitive: true,
                    literalValue: valueStr) as TypeNode;
            }
            
            if (enumModel.BaseType == IrEnumBaseType.Integer && long.TryParse(valueStr, out var longValue))
            {
                return new SimpleTypeNode(
                    new IdentifierNode("number"),
                    isPrimitive: true,
                    literalValue: longValue) as TypeNode;
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
    
    /// <summary>
    /// Проверяет, допускает ли TypeNode значение null (в объединении или как NullableType).
    /// </summary>
    private static bool IsTypeNullable(TypeNode type)
    {
        return type switch
        {
            NullableTypeNode => true,
            UnionTypeNode union when union.Types.Any(t =>
                t is SimpleTypeNode st && st.Name.Name == "null") => true,
            SimpleTypeNode simple when simple.IsNullable => true,
            ArrayTypeNode array when array.IsNullable => true,
            GenericTypeNode generic when generic.IsNullable => true,
            DictionaryTypeNode dict when dict.IsNullable => true,
            IntersectionTypeNode intersection when intersection.IsNullable => true,
            RawTypeNode raw when raw.IsNullable => true,
            _ => false
        };
    }
    
    private string MapPrimitiveToTypeScript(IrPrimitiveKind primitiveKind)
    {
        return primitiveKind switch
        {
            IrPrimitiveKind.String => "string",
            IrPrimitiveKind.Number => "number",
            IrPrimitiveKind.Integer => "number",
            IrPrimitiveKind.Long => (_options?.UseBigInt == true) ? "bigint" : "number",
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
        
        var useResultType = UseParamsStyleEndpointTransport()
            && operation.Responses.Any(r => r.Type != null);
        var returnType = useResultType
            ? new GenericTypeNode(
                new IdentifierNode("Promise"),
                new[] { new SimpleTypeNode(new IdentifierNode($"{_namingConverter.ToPascalCase(operation.MethodName)}Result"), isPrimitive: false) }.ToImmutableList())
            : ExtractReturnType(operation.Responses);
        
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
    /// Строит список SourceFile из IrDocument (модели, enum'ы и опционально endpoint-клиенты).
    /// </summary>
    /// <param name="includeOperationEndpointFiles">Если false — не генерировать файлы операций (их пишет <see cref="TypeScriptCodeGenerator"/> с суффиксами .axios/.fetch).</param>
    public IReadOnlyList<SourceFileNode> BuildSourceFiles(
        IrDocument document,
        string? importPath = null,
        FileOrganization? fileOrganization = null,
        bool includeOperationEndpointFiles = false)
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
        
        if (includeOperationEndpointFiles)
        {
            foreach (var client in document.Clients)
            {
                foreach (var operation in client.Operations)
                {
                    var transport = InferDefaultEndpointTransport();
                    sourceFiles.Add(BuildSourceFileForOperation(operation, importPath, fileOrganization, transport));
                }
            }
        }

        return sourceFiles;
    }

    private bool UseParamsStyleEndpointTransport() =>
        _activeEndpointTransport is TypeScriptEndpointTransport.Axios or TypeScriptEndpointTransport.Fetch;

    private TypeScriptEndpointTransport InferDefaultEndpointTransport()
    {
        var t = _options?.HttpTransports ?? TypeScriptHttpTransports.Fetch;
        if (t.HasFlag(TypeScriptHttpTransports.Axios))
            return TypeScriptEndpointTransport.Axios;
        return TypeScriptEndpointTransport.Fetch;
    }
    
    /// <summary>
    /// Строит SourceFile из IrClient (один файл со всеми операциями)
    /// </summary>
    public SourceFileNode BuildSourceFile(IrClient client, string? importPath = null, FileOrganization? fileOrganization = null)
    {
        var imports = BuildImports(ExtractDependencies(client), importPath, fileOrganization, FileCategory.Clients);
        var transport = InferDefaultEndpointTransport();
        _activeEndpointTransport = transport;
        try
        {
            var declarations = client.Operations.Select(op => (DeclarationNode)BuildFunction(op)).ToList();
            var exports = declarations.Select(d => new ExportNode(d, ExportKind.Named)).ToImmutableList();

            return new SourceFileNode(
                imports: imports,
                exports: exports,
                declarations: declarations);
        }
        finally
        {
            _activeEndpointTransport = TypeScriptEndpointTransport.Axios;
        }
    }
    
    /// <summary>
    /// Строит SourceFile для одной операции (endpoints/operationName/operationName.axios.ts или .fetch.ts).
    /// </summary>
    public SourceFileNode BuildSourceFileForOperation(
        IrOperation operation,
        string? importPath = null,
        FileOrganization? fileOrganization = null,
        TypeScriptEndpointTransport transport = TypeScriptEndpointTransport.Axios)
    {
        var client = new IrClient { Name = "Temp", Operations = new[] { operation } };
        _activeEndpointTransport = transport;
        try
        {
            var resultsAstBuilder = new TypeScriptResultsAstBuilder(_namingConverter, BuildType);
            var dependencies = UseParamsStyleEndpointTransport()
                ? Array.Empty<string>()
                : ExtractDependencies(client, excludeResponseModels: false);
            var imports = BuildImports(dependencies, importPath, fileOrganization, FileCategory.Clients, currentOperationName: operation.MethodName);

            imports = transport switch
            {
                TypeScriptEndpointTransport.Axios => AddAxiosAndParamsImports(imports, operation, fileOrganization),
                TypeScriptEndpointTransport.Fetch => AddFetchAndParamsImports(imports, operation, fileOrganization),
                _ => imports
            };

            var declarations = new List<DeclarationNode> { BuildFunction(operation, isExported: true) };
            var exports = declarations.Select(d => new ExportNode(d, ExportKind.Named)).ToImmutableList();

            return new SourceFileNode(
                imports: imports,
                exports: exports,
                declarations: declarations);
        }
        finally
        {
            _activeEndpointTransport = TypeScriptEndpointTransport.Axios;
        }
    }

    /// <summary>
    /// Возвращает имя axios instance и соответствующий ImportNode.
    /// Если заданы customInstancePath и customInstanceName — использует кастомный instance, иначе global axios.
    /// </summary>
    private (string InstanceName, ImportNode Import) GetAxiosInstanceImport()
    {
        var axios = _options?.Axios;
        var path = axios?.CustomInstancePath;
        var name = axios?.CustomInstanceName;
        if (!string.IsNullOrWhiteSpace(path) && !string.IsNullOrWhiteSpace(name))
        {
            return (name, new ImportNode(
                items: new[] { new IdentifierNode(name) }.ToImmutableList(),
                from: path,
                kind: ImportKind.Named));
        }
        return ("axios", new ImportNode(
            items: new[] { new IdentifierNode("axios") }.ToImmutableList(),
            from: "axios",
            kind: ImportKind.Default));
    }

    /// <summary>
    /// Добавляет импорты axios и params
    /// </summary>
    private IReadOnlyList<ImportNode> AddAxiosAndParamsImports(
        IReadOnlyList<ImportNode> existingImports,
        IrOperation operation,
        FileOrganization? fileOrganization)
    {
        var result = new List<ImportNode>();

        var (axiosInstanceName, axiosInstanceImport) = GetAxiosInstanceImport();
        result.Add(axiosInstanceImport);

        result.Add(new ImportNode(
            items: new[] { new IdentifierNode("AxiosRequestConfig"), new IdentifierNode("AxiosInstance") }.ToImmutableList(),
            from: "axios",
            kind: ImportKind.Named,
            isTypeOnly: true));

        var paramsParamsBuilder = new TypeScriptParamsAstBuilder(_namingConverter, BuildType);
        if (paramsParamsBuilder.HasParameters(operation))
        {
            var paramsTypeName = paramsParamsBuilder.ParamsInterfaceName(operation.MethodName);
            var operationFileName = GetModelFileName(operation.MethodName);
            result.Add(new ImportNode(
                items: new[] { new IdentifierNode(paramsTypeName) }.ToImmutableList(),
                from: $"./{operationFileName}.params",
                kind: ImportKind.Named,
                isTypeOnly: true));
        }

        var resultsAstBuilder = new TypeScriptResultsAstBuilder(_namingConverter, BuildType);
        if (resultsAstBuilder.HasResponsesWithBody(operation))
        {
            var resultTypeName = resultsAstBuilder.ResultTypeName(operation.MethodName);
            var operationFileName = GetModelFileName(operation.MethodName);
            result.Add(new ImportNode(
                items: new[] { new IdentifierNode(resultTypeName) }.ToImmutableList(),
                from: $"./{operationFileName}.results",
                kind: ImportKind.Named,
                isTypeOnly: true));
        }

        // При Zod добавляем импорты схем
        if (_options?.ValidationLibrary == ValidationLibrary.Zod && _options.SerializeWithSchemas)
        {
            var opName = _namingConverter.ToPascalCase(operation.MethodName);
            var operationFileName = GetModelFileName(operation.MethodName);
            var modelsPath = fileOrganization?.ModelsPath ?? "models";

            var paramsSchemaImports = new List<IdentifierNode>();
            if (operation.PathParameters.Count > 0)
                paramsSchemaImports.Add(new IdentifierNode($"{opName}RouteParamsSchema"));
            if (operation.QueryParameters.Count > 0)
                paramsSchemaImports.Add(new IdentifierNode($"{opName}QueryParamsSchema"));

            if (paramsSchemaImports.Count > 0)
            {
                result.Add(new ImportNode(
                    items: paramsSchemaImports.ToImmutableList(),
                    from: $"./{operationFileName}.params.schema",
                    kind: ImportKind.Named));
            }

            // Body schema - из models
            if (operation.RequestBody != null && operation.RequestBody.Type is IrModelReference bodyModelRef)
            {
                var bodyModelFileName = GetModelFileName(bodyModelRef.ModelName);
                result.Add(new ImportNode(
                    items: new[] { new IdentifierNode($"{bodyModelRef.ModelName}Schema") }.ToImmutableList(),
                    from: $"../../{modelsPath}/{bodyModelFileName}/{bodyModelFileName}.schema",
                    kind: ImportKind.Named));
            }

            // Response schemas - для всех ответов с телом (parse по status)
            var responseModelRefs = operation.Responses
                .Where(r => r.Type is IrModelReference)
                .Select(r => (IrModelReference)r.Type!)
                .DistinctBy(m => m.ModelName);
            foreach (var modelRef in responseModelRefs)
            {
                var responseModelFileName = GetModelFileName(modelRef.ModelName);
                result.Add(new ImportNode(
                    items: new[] { new IdentifierNode($"{modelRef.ModelName}Schema") }.ToImmutableList(),
                    from: $"../../{modelsPath}/{responseModelFileName}/{responseModelFileName}.schema",
                    kind: ImportKind.Named));
            }
        }

        result.AddRange(existingImports);
        return result;
    }

    /// <summary>
    /// Импорты для fetch endpoint: params, results, zod — по смыслу как axios; опционально кастомный fetcher.
    /// </summary>
    private IReadOnlyList<ImportNode> AddFetchAndParamsImports(
        IReadOnlyList<ImportNode> existingImports,
        IrOperation operation,
        FileOrganization? fileOrganization)
    {
        var result = new List<ImportNode>();

        var fetchOpts = _options?.Fetch;
        if (!string.IsNullOrWhiteSpace(fetchOpts?.CustomFetcherPath) && !string.IsNullOrWhiteSpace(fetchOpts?.CustomFetcherName))
        {
            result.Add(new ImportNode(
                items: new[] { new IdentifierNode(fetchOpts!.CustomFetcherName!) }.ToImmutableList(),
                from: fetchOpts.CustomFetcherPath!,
                kind: ImportKind.Named));
        }

        var paramsParamsBuilder = new TypeScriptParamsAstBuilder(_namingConverter, BuildType);
        if (paramsParamsBuilder.HasParameters(operation))
        {
            var paramsTypeName = paramsParamsBuilder.ParamsInterfaceName(operation.MethodName);
            var operationFileName = GetModelFileName(operation.MethodName);
            result.Add(new ImportNode(
                items: new[] { new IdentifierNode(paramsTypeName) }.ToImmutableList(),
                from: $"./{operationFileName}.params",
                kind: ImportKind.Named,
                isTypeOnly: true));
        }

        var resultsAstBuilder = new TypeScriptResultsAstBuilder(_namingConverter, BuildType);
        if (resultsAstBuilder.HasResponsesWithBody(operation))
        {
            var resultTypeName = resultsAstBuilder.ResultTypeName(operation.MethodName);
            var operationFileName = GetModelFileName(operation.MethodName);
            result.Add(new ImportNode(
                items: new[] { new IdentifierNode(resultTypeName) }.ToImmutableList(),
                from: $"./{operationFileName}.results",
                kind: ImportKind.Named,
                isTypeOnly: true));
        }

        if (_options?.ValidationLibrary == ValidationLibrary.Zod && _options.SerializeWithSchemas)
        {
            var opName = _namingConverter.ToPascalCase(operation.MethodName);
            var operationFileName = GetModelFileName(operation.MethodName);
            var modelsPath = fileOrganization?.ModelsPath ?? "models";

            var paramsSchemaImports = new List<IdentifierNode>();
            if (operation.PathParameters.Count > 0)
                paramsSchemaImports.Add(new IdentifierNode($"{opName}RouteParamsSchema"));
            if (operation.QueryParameters.Count > 0)
                paramsSchemaImports.Add(new IdentifierNode($"{opName}QueryParamsSchema"));

            if (paramsSchemaImports.Count > 0)
            {
                result.Add(new ImportNode(
                    items: paramsSchemaImports.ToImmutableList(),
                    from: $"./{operationFileName}.params.schema",
                    kind: ImportKind.Named));
            }

            if (operation.RequestBody != null && operation.RequestBody.Type is IrModelReference bodyModelRef)
            {
                var bodyModelFileName = GetModelFileName(bodyModelRef.ModelName);
                result.Add(new ImportNode(
                    items: new[] { new IdentifierNode($"{bodyModelRef.ModelName}Schema") }.ToImmutableList(),
                    from: $"../../{modelsPath}/{bodyModelFileName}/{bodyModelFileName}.schema",
                    kind: ImportKind.Named));
            }

            var responseModelRefs = operation.Responses
                .Where(r => r.Type is IrModelReference)
                .Select(r => (IrModelReference)r.Type!)
                .DistinctBy(m => m.ModelName);
            foreach (var modelRef in responseModelRefs)
            {
                var responseModelFileName = GetModelFileName(modelRef.ModelName);
                result.Add(new ImportNode(
                    items: new[] { new IdentifierNode($"{modelRef.ModelName}Schema") }.ToImmutableList(),
                    from: $"../../{modelsPath}/{responseModelFileName}/{responseModelFileName}.schema",
                    kind: ImportKind.Named));
            }
        }

        result.AddRange(existingImports);
        return result;
    }
    
    // Приватные вспомогательные методы для IR операций
    
    private IReadOnlyList<ParameterDeclarationNode> BuildFunctionParameters(IrOperation operation)
    {
        if (UseParamsStyleEndpointTransport())
        {
            var paramsBuilder = new TypeScriptParamsAstBuilder(_namingConverter, BuildType);
            var endpointParams = new List<ParameterDeclarationNode>();
            if (paramsBuilder.HasParameters(operation))
            {
                var paramsTypeName = paramsBuilder.ParamsInterfaceName(operation.MethodName);
                endpointParams.Add(new ParameterDeclarationNode(
                        name: new IdentifierNode("params"),
                        type: new SimpleTypeNode(new IdentifierNode(paramsTypeName), isPrimitive: false, isNullable: false, isOptional: false),
                        isRequired: true));
            }

            var optionsTypeName = _activeEndpointTransport == TypeScriptEndpointTransport.Axios
                ? "AxiosRequestConfig"
                : "RequestInit";
            var optionsType = new SimpleTypeNode(new IdentifierNode(optionsTypeName), isPrimitive: false, isNullable: false, isOptional: false);
            var optionsDoc = _activeEndpointTransport == TypeScriptEndpointTransport.Axios
                ? "Optional Axios request config (headers, timeout, etc.)"
                : "Optional RequestInit (headers, credentials, etc.)";
            endpointParams.Add(new ParameterDeclarationNode(
                name: new IdentifierNode("options"),
                type: optionsType,
                isRequired: false,
                documentation: new CommentNode(optionsDoc, CommentKind.JSDoc)));
            endpointParams.Add(new ParameterDeclarationNode(
                name: new IdentifierNode("signal"),
                type: new SimpleTypeNode(new IdentifierNode("AbortSignal"), isPrimitive: false, isNullable: false, isOptional: false),
                isRequired: false,
                documentation: new CommentNode("Optional AbortSignal for request cancellation", CommentKind.JSDoc)));
            return endpointParams.ToImmutableList();
        }

        var parameters = new List<ParameterDeclarationNode>();

        foreach (var param in operation.PathParameters)
        {
            var type = BuildType(param.Type);
            parameters.Add(new ParameterDeclarationNode(
                name: new IdentifierNode(param.Name),
                type: type,
                isRequired: true));
        }

        foreach (var param in operation.QueryParameters)
        {
            var type = BuildType(param.Type);
            parameters.Add(new ParameterDeclarationNode(
                name: new IdentifierNode(param.Name),
                type: type,
                isRequired: param.IsRequired));
        }

        foreach (var param in operation.HeaderParameters)
        {
            var type = BuildType(param.Type);
            parameters.Add(new ParameterDeclarationNode(
                name: new IdentifierNode(param.Name),
                type: type,
                isRequired: param.IsRequired));
        }

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
        return _activeEndpointTransport switch
        {
            TypeScriptEndpointTransport.Axios => BuildAxiosFunctionBody(operation),
            TypeScriptEndpointTransport.Fetch => BuildFetchFunctionBody(operation),
            _ => null
        };
    }
    
    /// <summary>
    /// Строит тело функции с Axios вызовами, params interface и Zod serialization/deserialization
    /// </summary>
    private BlockNode BuildAxiosFunctionBody(IrOperation operation)
    {
        var statements = new List<StatementNode>();
        var ctx = PrepareHttpParamSourcesWithOptionalZod(operation);
        statements.AddRange(ctx.LeadingStatements);

        var routeSource = ctx.RouteSource;
        var querySource = ctx.QuerySource;
        var bodySource = ctx.BodySource;
        var headersSource = ctx.HeadersSource;

        var useZod = _options?.ValidationLibrary == ValidationLibrary.Zod
            && (_options.SerializeWithSchemas || _options.DeserializeWithSchemas);

        var (axiosInstanceName, _) = GetAxiosInstanceImport();
        var axiosInstanceType = new SimpleTypeNode(new IdentifierNode("AxiosInstance"), isPrimitive: false);
        statements.Add(new VariableDeclarationStatementNode(
            new IdentifierNode("axiosInstance"),
            type: axiosInstanceType,
            initializer: new IdentifierExpressionNode(new IdentifierNode(axiosInstanceName))));
        statements.Add(CreateBlankLineStatement());

        var paramsSource = new Axios.Models.AxiosConfigParamsSource(routeSource, querySource, bodySource, headersSource);
        var axiosBuilder = new Axios.Ast.Builders.AxiosAstBuilder(_namingConverter, BuildType);
        var configNode = axiosBuilder.BuildAxiosConfigWithParams(operation, paramsSource);

        // Используем unknown — не добавляем импорт модели body, работаем с params
        var bodyTypeNode = new SimpleTypeNode(new IdentifierNode("unknown"), isPrimitive: true);
        var axiosConfigGeneric = new GenericTypeNode(
            new IdentifierNode("AxiosRequestConfig"),
            new[] { bodyTypeNode }.ToImmutableList());

        var useResultType = operation.Responses.Any(r => r.Type != null);
        var operationReturnType = useResultType
            ? new SimpleTypeNode(new IdentifierNode("unknown"), isPrimitive: false)
            : ExtractReturnType(operation.Responses);

        statements.Add(new VariableDeclarationStatementNode(
            new IdentifierNode("baseConfig"),
            type: axiosConfigGeneric,
            initializer: configNode));

        var optionsRef = new IdentifierExpressionNode(new IdentifierNode("options"));
        var signalRef = new IdentifierExpressionNode(new IdentifierNode("signal"));
        var emptyObject = new ObjectLiteralExpressionNode(ImmutableList<PropertyAssignmentNode>.Empty);
        var optionsSpread = new BinaryExpressionNode(optionsRef, BinaryOperator.NullishCoalescing, emptyObject);
        var signalSpread = new ConditionalExpressionNode(
            signalRef,
            new ObjectLiteralExpressionNode(new[]
            {
                new PropertyAssignmentNode(new IdentifierNode("signal"), signalRef)
            }.ToImmutableList()),
            emptyObject);

        var placeholderId = new IdentifierNode("_");
        var mergedConfigProps = new List<PropertyAssignmentNode>
        {
            new(placeholderId, new IdentifierExpressionNode(new IdentifierNode("baseConfig")), isSpread: true),
            new(placeholderId, optionsSpread, isSpread: true),
            new(placeholderId, signalSpread, isSpread: true)
        };
        if (useResultType)
        {
            mergedConfigProps.Add(new PropertyAssignmentNode(
                new IdentifierNode("validateStatus"),
                new ArrowFunctionExpressionNode(
                    ImmutableList<ParameterDeclarationNode>.Empty,
                    new LiteralExpressionNode(true, LiteralKind.Boolean))));
        }

        statements.Add(new VariableDeclarationStatementNode(
            new IdentifierNode("axiosConfig"),
            type: axiosConfigGeneric,
            initializer: new ObjectLiteralExpressionNode(mergedConfigProps.ToImmutableList(), type: null, metadata: null, preferMultilineFormat: true)));

        var axiosCall = new Axios.Ast.Nodes.AxiosCallExpressionNode(
            new IdentifierExpressionNode(new IdentifierNode("axiosInstance")),
            new IdentifierExpressionNode(new IdentifierNode("axiosConfig")));

        statements.Add(CreateBlankLineStatement());
        statements.Add(new VariableDeclarationStatementNode(
            new IdentifierNode("response"),
            type: null,
            initializer: new AwaitExpressionNode(axiosCall)));
        statements.Add(CreateBlankLineStatement());

        if (useResultType)
        {
            var responseDataRef = new MemberAccessExpressionNode(
                new IdentifierExpressionNode(new IdentifierNode("response")),
                new IdentifierNode("data"));
            var responseStatusRef = new MemberAccessExpressionNode(
                new IdentifierExpressionNode(new IdentifierNode("response")),
                new IdentifierNode("status"));
            var responsesWithType = operation.Responses
                .Where(r => r.Type is IrModelReference && int.TryParse(r.StatusCode, out _))
                .ToList();

            foreach (var resp in responsesWithType)
            {
                var statusCode = int.Parse(resp.StatusCode);
                var isSuccessful = statusCode >= 200 && statusCode < 300;
                var modelRef = (IrModelReference)resp.Type!;
                var schemaName = $"{modelRef.ModelName}Schema";
                var dataAccess = new MemberAccessExpressionNode(
                    new IdentifierExpressionNode(new IdentifierNode("response")),
                    new IdentifierNode("data"));
                var parseCall = useZod && _options!.DeserializeWithSchemas
                    ? CreateZodParseCall(schemaName, dataAccess)
                    : (ExpressionNode)dataAccess;

                var returnObj = new ObjectLiteralExpressionNode(new[]
                {
                    new PropertyAssignmentNode(new IdentifierNode("status"),
                        new LiteralExpressionNode(statusCode, LiteralKind.Number)),
                    new PropertyAssignmentNode(new IdentifierNode("body"), parseCall),
                    new PropertyAssignmentNode(new IdentifierNode("isSuccessful"),
                        new LiteralExpressionNode(isSuccessful, LiteralKind.Boolean)),
                    new PropertyAssignmentNode(new IdentifierNode("isFailed"),
                        new LiteralExpressionNode(!isSuccessful, LiteralKind.Boolean))
                }.ToImmutableList());

                var statusLiteral = new LiteralExpressionNode(statusCode, LiteralKind.Number);
                var condition = new BinaryExpressionNode(responseStatusRef, BinaryOperator.Equal, statusLiteral);

                statements.Add(new IfStatementNode(
                    condition,
                    new BlockNode(new[] { new ReturnStatementNode(returnObj) }.ToImmutableList()),
                    null));
            }

            statements.Add(new ReturnStatementNode(
                new ObjectLiteralExpressionNode(new[]
                {
                    new PropertyAssignmentNode(new IdentifierNode("status"), responseStatusRef),
                    new PropertyAssignmentNode(new IdentifierNode("body"), responseDataRef),
                    new PropertyAssignmentNode(new IdentifierNode("isSuccessful"),
                        new BinaryExpressionNode(
                            new BinaryExpressionNode(responseStatusRef, BinaryOperator.GreaterThanOrEqual, new LiteralExpressionNode(200, LiteralKind.Number)),
                            BinaryOperator.LogicalAnd,
                            new BinaryExpressionNode(responseStatusRef, BinaryOperator.LessThan, new LiteralExpressionNode(300, LiteralKind.Number)))),
                    new PropertyAssignmentNode(new IdentifierNode("isFailed"),
                        new UnaryExpressionNode(UnaryOperator.LogicalNot,
                            new BinaryExpressionNode(
                                new BinaryExpressionNode(responseStatusRef, BinaryOperator.GreaterThanOrEqual, new LiteralExpressionNode(200, LiteralKind.Number)),
                                BinaryOperator.LogicalAnd,
                                new BinaryExpressionNode(responseStatusRef, BinaryOperator.LessThan, new LiteralExpressionNode(300, LiteralKind.Number)))))
                }.ToImmutableList())));
        }
        else if (operationReturnType is not SimpleTypeNode { Name.Name: "void" })
        {
            statements.Add(new VariableDeclarationStatementNode(
                new IdentifierNode("responseData"),
                type: null,
                initializer: new MemberAccessExpressionNode(
                    new IdentifierExpressionNode(new IdentifierNode("response")),
                    new IdentifierNode("data"))));

            if (useZod && _options!.DeserializeWithSchemas)
            {
                var successResponse = operation.Responses.FirstOrDefault(r =>
                    int.TryParse(r.StatusCode, out var code) && code >= 200 && code < 300);
                if (successResponse?.Type is IrModelReference responseModelRef)
                {
                    var parseCall = CreateZodParseCall($"{responseModelRef.ModelName}Schema",
                        new IdentifierExpressionNode(new IdentifierNode("responseData")));
                    statements.Add(new VariableDeclarationStatementNode(
                        new IdentifierNode("parsedResponse"),
                        type: null,
                        initializer: parseCall));
                    statements.Add(new ReturnStatementNode(new IdentifierExpressionNode(new IdentifierNode("parsedResponse"))));
                }
                else
                {
                    statements.Add(new ReturnStatementNode(new IdentifierExpressionNode(new IdentifierNode("responseData"))));
                }
            }
            else
            {
                statements.Add(new ReturnStatementNode(new IdentifierExpressionNode(new IdentifierNode("responseData"))));
            }
        }

        return new BlockNode(statements.ToImmutableList());
    }

    /// <summary>
    /// Создаёт statement для пустой строки (форматирование)
    /// </summary>
    private static BlankLineStatementNode CreateBlankLineStatement()
    {
        return new BlankLineStatementNode();
    }

    /// <summary>
    /// Создаёт выражение Schema.parse(input)
    /// </summary>
    private static ExpressionNode CreateZodParseCall(string schemaName, ExpressionNode input)
    {
        var schemaAccess = new MemberAccessExpressionNode(
            new IdentifierExpressionNode(new IdentifierNode(schemaName)),
            new IdentifierNode("parse"));
        return new CallExpressionNode(schemaAccess, new[] { input }.ToImmutableList());
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
    
    private IReadOnlyList<string> ExtractDependencies(IrClient client, bool excludeResponseModels = false)
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
            
            // Зависимости из responses (пропускаем при excludeResponseModels — используется Result type)
            if (!excludeResponseModels)
            {
                foreach (var response in operation.Responses)
                {
                    if (response.Type != null)
                    {
                        ExtractDependenciesFromType(response.Type, dependencies);
                    }
                }
            }
        }
        
        return dependencies.ToList();
    }
}
