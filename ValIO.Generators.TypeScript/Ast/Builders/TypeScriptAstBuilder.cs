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
using ValIO.Core.Models.TemplateData;
using ValIO.Generators.TypeScript.Models;

namespace ValIO.Generators.TypeScript.Ast.Builders;

/// <summary>
/// Построитель AST для TypeScript (Builder Pattern)
/// </summary>
public class TypeScriptAstBuilder : ITypeScriptAstBuilder
{
    private readonly INamingConverter _namingConverter;
    private readonly ITypeMapper _typeMapper;
    private readonly HttpAstBuilder _httpBuilder;
    
    public TypeScriptAstBuilder(INamingConverter namingConverter, ITypeMapper typeMapper)
    {
        _namingConverter = namingConverter ?? throw new ArgumentNullException(nameof(namingConverter));
        _typeMapper = typeMapper ?? throw new ArgumentNullException(nameof(typeMapper));
        _httpBuilder = new HttpAstBuilder(namingConverter, typeMapper);
    }
    
    public SourceFileNode BuildSourceFile(ModelTemplateData<TypeScriptModelTemplateDataOptions, TypeScriptPropertyTemplateDataOptions> data)
    {
        var imports = BuildImports(data.Dependencies, data.ImportPath);
        var declarations = BuildTypeDeclaration(data);
        var exports = declarations.Select(d => new ExportNode(d, ExportKind.Named)).ToImmutableList();
        
        return new SourceFileNode(
            imports: imports,
            exports: exports,
            declarations: declarations);
    }
    
    public SourceFileNode BuildSourceFile(ClientTemplateData<TypeScriptClientTemplateDataOptions, TypeScriptOperationTemplateDataOptions> data)
    {
        var imports = BuildImports(data.Dependencies, data.ImportPath);
        var declarations = BuildClientDeclarations(data);
        var exports = declarations.Select(d => new ExportNode(d, ExportKind.Named)).ToImmutableList();
        
        return new SourceFileNode(
            imports: imports,
            exports: exports,
            declarations: declarations);
    }
    
    public SourceFileNode BuildSourceFile(EnumTemplateData<TypeScriptEnumTemplateDataOptions> data)
    {
        var imports = BuildImports(Array.Empty<string>(), data.ImportPath);
        var declarations = BuildEnumDeclaration(data);
        var exports = declarations.Select(d => new ExportNode(d, ExportKind.Named)).ToImmutableList();
        
        return new SourceFileNode(
            imports: imports,
            exports: exports,
            declarations: declarations);
    }
    
    public InterfaceDeclarationNode BuildInterface(ModelTemplateData<TypeScriptModelTemplateDataOptions, TypeScriptPropertyTemplateDataOptions> data)
    {
        var name = new IdentifierNode(data.Name);
        var comment = !string.IsNullOrEmpty(data.Description) 
            ? new CommentNode(data.Description, CommentKind.JSDoc) 
            : null;
        
        var properties = data.Properties.Select(BuildProperty).ToList<DeclarationNode>();
        var baseTypes = !string.IsNullOrEmpty(data.BaseClassName)
            ? new[] { BuildSimpleType(data.BaseClassName) }.ToImmutableList<TypeNode>()
            : ImmutableList<TypeNode>.Empty;
        
        return new InterfaceDeclarationNode(
            name: name,
            accessModifier: AccessModifier.Public,
            baseTypes: baseTypes,
            members: properties.ToImmutableList(),
            documentation: comment);
    }
    
    public TypeAliasDeclarationNode BuildTypeAlias(string name, TypeNode type)
    {
        return new TypeAliasDeclarationNode(
            name: new IdentifierNode(name),
            type: type);
    }
    
    public FunctionDeclarationNode BuildFunction(OperationTemplateData<TypeScriptOperationTemplateDataOptions> data)
    {
        var name = new IdentifierNode(data.MethodName);
        var returnType = BuildType(data.ReturnTypeInfo);
        var comment = !string.IsNullOrEmpty(data.Description) 
            ? new CommentNode(data.Description, CommentKind.JSDoc) 
            : null;
        
        var parameters = BuildFunctionParameters(data);
        var body = BuildFunctionBody(data);
        
        return new FunctionDeclarationNode(
            name: name,
            returnType: returnType,
            parameters: parameters,
            body: body,
            isArrowFunction: false,
            isAsync: true,
            documentation: comment);
    }
    
    public PropertyDeclarationNode BuildProperty(PropertyTemplateData<TypeScriptPropertyTemplateDataOptions> data)
    {
        var name = new IdentifierNode(data.Name);
        var type = BuildType(data.TypeInfo);
        var comment = !string.IsNullOrEmpty(data.Description) 
            ? new CommentNode(data.Description, CommentKind.JSDoc) 
            : null;
        
        var defaultValue = data.DefaultValue != null
            ? BuildLiteral(data.DefaultValue, data.TypeInfo)
            : null;
        
        return new PropertyDeclarationNode(
            name: name,
            type: type,
            isRequired: data.IsRequired,
            accessor: PropertyAccessorKind.Get,
            defaultValue: defaultValue,
            documentation: comment);
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
    
    public LiteralExpressionNode BuildLiteral(object value, TypeInfo typeInfo)
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
        
        return new LiteralExpressionNode(value, literalKind);
    }
    
    // Приватные вспомогательные методы
    
    private IReadOnlyList<ImportNode> BuildImports(IReadOnlyList<string> dependencies, string? importPath)
    {
        // TODO: Реализовать построение импортов на основе зависимостей
        return ImmutableList<ImportNode>.Empty;
    }
    
    private IReadOnlyList<DeclarationNode> BuildTypeDeclaration(ModelTemplateData<TypeScriptModelTemplateDataOptions, TypeScriptPropertyTemplateDataOptions> data)
    {
        return new[] { (DeclarationNode)BuildInterface(data) };
    }
    
    private IReadOnlyList<DeclarationNode> BuildClientDeclarations(ClientTemplateData<TypeScriptClientTemplateDataOptions, TypeScriptOperationTemplateDataOptions> data)
    {
        var functions = data.Operations.Select(BuildFunction).ToList<DeclarationNode>();
        return functions;
    }
    
    private IReadOnlyList<DeclarationNode> BuildEnumDeclaration(EnumTemplateData<TypeScriptEnumTemplateDataOptions> data)
    {
        // Для TypeScript enum может быть либо enum, либо union type
        if (data.Format == EnumFormat.Union)
        {
            var unionType = BuildEnumUnionType(data);
            return new[] { (DeclarationNode)BuildTypeAlias(data.Name, unionType) };
        }
        
        // TODO: Реализовать построение TypeScript enum
        return ImmutableList<DeclarationNode>.Empty;
    }
    
    private UnionTypeNode BuildEnumUnionType(EnumTemplateData<TypeScriptEnumTemplateDataOptions> data)
    {
        var types = data.Values.Select(v => 
        {
            if (v.Value is string strValue)
                return new SimpleTypeNode(new IdentifierNode($"\"{strValue}\""), isPrimitive: true) as TypeNode;
            return BuildSimpleType("string");
        }).ToImmutableList();
        
        return new UnionTypeNode(types);
    }
    
    private IReadOnlyList<ParameterDeclarationNode> BuildFunctionParameters(OperationTemplateData<TypeScriptOperationTemplateDataOptions> data)
    {
        var parameters = new List<ParameterDeclarationNode>();
        
        // Path parameters
        foreach (var param in data.PathParameters)
        {
            var type = BuildType(param.TypeInfo);
            parameters.Add(new ParameterDeclarationNode(
                name: new IdentifierNode(param.VariableName),
                type: type,
                isRequired: true));
        }
        
        // Query parameters
        foreach (var param in data.QueryParameters)
        {
            var type = BuildType(param.TypeInfo);
            parameters.Add(new ParameterDeclarationNode(
                name: new IdentifierNode(param.VariableName),
                type: type,
                isRequired: param.IsRequired));
        }
        
        // Request body
        if (data.HasRequestBody && data.RequestBodyTypeInfo != null)
        {
            var bodyType = BuildType(data.RequestBodyTypeInfo);
            parameters.Add(new ParameterDeclarationNode(
                name: new IdentifierNode(data.RequestBodyParameter),
                type: bodyType,
                isRequired: data.IsRequestBodyRequired));
        }
        
        return parameters.ToImmutableList();
    }
    
    private BlockNode? BuildFunctionBody(OperationTemplateData<TypeScriptOperationTemplateDataOptions> data)
    {
        // TODO: Полная реализация построения тела функции с использованием HTTP узлов
        // Пока возвращаем null для type-only функции
        // 
        // В будущем здесь будет построение тела функции с использованием:
        // - HttpMethodNode - для определения HTTP метода
        // - RouteNode - для построения пути с path параметрами
        // - PathParameterNode, QueryParameterNode, HeaderParameterNode - для параметров
        // - RequestBodyNode - для тела запроса
        // - ResponseTypeNode - для обработки ответа
        //
        // Структура тела функции:
        // 1. Построение пути (const path = `...`)
        // 2. Построение query параметров (const queryParams = ...)
        // 3. Построение headers (const headers = {...})
        // 4. Построение body (если есть)
        // 5. Fetch/Axios вызов
        // 6. Обработка ответа и return
        //
        // Для полной реализации потребуется:
        // - Создание VariableDeclarationNode для промежуточных переменных
        // - Создание ExpressionStatementNode для вызовов
        // - Создание ReturnStatementNode для возврата результата
        // - Использование TemplateLiteralExpressionNode для путей
        // - Использование ObjectLiteralExpressionNode для headers, body
        // - Использование CallExpressionNode для fetch/axios вызовов
        return null;
    }
    
    /// <summary>
    /// Строит HTTP метод из OperationTemplateData
    /// </summary>
    public HttpMethodNode BuildHttpMethod(OperationTemplateData<TypeScriptOperationTemplateDataOptions> data)
    {
        return _httpBuilder.BuildHttpMethod(data.HttpMethod);
    }
    
    /// <summary>
    /// Строит Route из OperationTemplateData
    /// </summary>
    public RouteNode BuildRoute(OperationTemplateData<TypeScriptOperationTemplateDataOptions> data)
    {
        return _httpBuilder.BuildRoute(data.Path, data.PathParameters, _typeMapper);
    }
    
    /// <summary>
    /// Строит RequestBody из OperationTemplateData
    /// </summary>
    public RequestBodyNode? BuildRequestBody(OperationTemplateData<TypeScriptOperationTemplateDataOptions> data)
    {
        if (!data.HasRequestBody || data.RequestBodyTypeInfo == null)
            return null;
        
        return _httpBuilder.BuildRequestBody(
            data.RequestBodyTypeInfo,
            data.RequestBodyContentType,
            data.IsRequestBodyRequired,
            _typeMapper);
    }
    
    /// <summary>
    /// Строит список QueryParameterNode из OperationTemplateData
    /// </summary>
    public IReadOnlyList<QueryParameterNode> BuildQueryParameters(OperationTemplateData<TypeScriptOperationTemplateDataOptions> data)
    {
        return data.QueryParameters
            .Select(p => _httpBuilder.BuildQueryParameter(p, _typeMapper))
            .ToImmutableList();
    }
    
    /// <summary>
    /// Строит список HeaderParameterNode из OperationTemplateData
    /// </summary>
    public IReadOnlyList<HeaderParameterNode> BuildHeaderParameters(OperationTemplateData<TypeScriptOperationTemplateDataOptions> data)
    {
        return data.HeaderParameters
            .Select(p => _httpBuilder.BuildHeaderParameter(p, _typeMapper))
            .ToImmutableList();
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
    public SourceFileNode BuildSourceFile(IrModel model, string? importPath = null)
    {
        var imports = BuildImports(ExtractDependencies(model), importPath);
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
            documentation: comment);
    }
    
    /// <summary>
    /// Строит SourceFile из IrEnum
    /// </summary>
    public SourceFileNode BuildSourceFile(IrEnum enumModel, string? importPath = null)
    {
        var imports = BuildImports(Array.Empty<string>(), importPath);
        var declarations = BuildEnumDeclaration(enumModel);
        var exports = declarations.Select(d => new ExportNode(d, ExportKind.Named)).ToImmutableList();
        
        return new SourceFileNode(
            imports: imports,
            exports: exports,
            declarations: declarations);
    }
    
    /// <summary>
    /// Строит DeclarationNode из IrEnum (может быть TypeAlias для union или enum)
    /// </summary>
    private IReadOnlyList<DeclarationNode> BuildEnumDeclaration(IrEnum enumModel)
    {
        // Для TypeScript enum может быть либо union type, либо enum
        // Используем union type для строковых enum'ов
        if (enumModel.BaseType == IrEnumBaseType.String)
        {
            var unionType = BuildEnumUnionType(enumModel);
            return new[] { (DeclarationNode)BuildTypeAlias(enumModel.Name, unionType) };
        }
        
        // TODO: Реализовать построение TypeScript enum для числовых значений
        // Пока возвращаем union type
        var numericUnionType = BuildEnumUnionType(enumModel);
        return new[] { (DeclarationNode)BuildTypeAlias(enumModel.Name, numericUnionType) };
    }
    
    private UnionTypeNode BuildEnumUnionType(IrEnum enumModel)
    {
        var types = enumModel.Values.Select(v =>
        {
            if (v.Value is string strValue)
                return new SimpleTypeNode(new IdentifierNode($"\"{strValue}\""), isPrimitive: true) as TypeNode;
            if (v.Value is int or long or short or byte)
                return new SimpleTypeNode(new IdentifierNode(v.Value.ToString() ?? "0"), isPrimitive: true) as TypeNode;
            return BuildSimpleType("string");
        }).ToImmutableList();
        
        return new UnionTypeNode(types);
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
}
