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
using ValIO.Generators.CSharp.Ast.Nodes;

namespace ValIO.Generators.CSharp.Ast.Builders;

/// <summary>
/// Построитель AST для C# (Builder Pattern)
/// </summary>
public class CSharpAstBuilder : ICSharpAstBuilder
{
    private readonly INamingConverter _namingConverter;
    private readonly HttpAstBuilder _httpBuilder;
    
    public CSharpAstBuilder(INamingConverter namingConverter)
    {
        _namingConverter = namingConverter ?? throw new ArgumentNullException(nameof(namingConverter));
        _httpBuilder = new HttpAstBuilder();
    }
    
    public NamespaceDeclarationNode BuildNamespace(string name, IReadOnlyList<DeclarationNode> declarations)
    {
        var nameIdentifier = new IdentifierNode(name);
        return new NamespaceDeclarationNode(
            name: nameIdentifier,
            declarations: declarations.ToImmutableList());
    }
    
    public TypeNode BuildType(TypeInfo typeInfo)
    {
        if (typeInfo.IsCollection)
        {
            var elementType = BuildTypeFromBaseType(typeInfo.GenericArguments.FirstOrDefault() ?? TypeInfo.Simple(typeInfo.BaseType));
            return new ArrayTypeNode(elementType, isNullable: typeInfo.IsNullable);
        }
        
        if (typeInfo.IsDictionary)
        {
            var keyType = BuildTypeFromBaseType(typeInfo.GenericArguments.ElementAtOrDefault(0) ?? TypeInfo.Simple("string"));
            var valueType = BuildTypeFromBaseType(typeInfo.GenericArguments.ElementAtOrDefault(1) ?? TypeInfo.Simple("object"));
            return new DictionaryTypeNode(keyType, valueType, isNullable: typeInfo.IsNullable);
        }
        
        if (!string.IsNullOrEmpty(typeInfo.BaseType) && typeInfo.GenericArguments.Count > 0)
        {
            var genericArgs = typeInfo.GenericArguments.Select(BuildType).ToImmutableList();
            var genericType = new GenericTypeNode(
                name: new IdentifierNode(typeInfo.BaseType),
                typeArguments: genericArgs,
                isNullable: typeInfo.IsNullable);
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
        
        return new LiteralExpressionNode(value ?? string.Empty, literalKind);
    }
    
    // Приватные вспомогательные методы
    
    private IReadOnlyList<UsingDirectiveNode> BuildUsingDirectives(IReadOnlyList<string> dependencies)
    {
        return dependencies
            .Select(dep => new UsingDirectiveNode(new IdentifierNode(dep)))
            .ToImmutableList();
    }
    
    private TypeNode BuildTypeFromBaseType(TypeInfo typeInfo)
    {
        var simpleType = new SimpleTypeNode(
            name: new IdentifierNode(typeInfo.BaseType),
            isPrimitive: typeInfo.IsPrimitive,
            isNullable: typeInfo.IsNullable);
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
            IrTypeKind.Optional => BuildType(((IrOptionalType)irType).InnerType), // Optional не используется в C#
            _ => throw new NotSupportedException($"IrTypeKind {irType.Kind} is not supported")
        };
    }
    
    /// <summary>
    /// Строит CompilationUnit из IrModel
    /// </summary>
    public CompilationUnitNode BuildCompilationUnit(IrModel model, string? @namespace = null)
    {
        var usings = BuildUsingDirectives(ExtractDependencies(model));
        var namespaceNode = BuildNamespace(@namespace ?? string.Empty, new[] { BuildClass(model) }.ToList<DeclarationNode>());
        
        return new CompilationUnitNode(
            usings: usings,
            namespaces: new[] { namespaceNode }.ToImmutableList(),
            declarations: ImmutableList<DeclarationNode>.Empty);
    }
    
    /// <summary>
    /// Строит ClassDeclarationNode из IrModel
    /// </summary>
    public ClassDeclarationNode BuildClass(IrModel model)
    {
        var name = new IdentifierNode(model.Name);
        var comment = !string.IsNullOrEmpty(model.Description)
            ? new CommentNode(model.Description, CommentKind.Xml)
            : null;
        
        var properties = model.Properties.Select(BuildProperty).ToList<DeclarationNode>();
        
        return new ClassDeclarationNode(
            name: name,
            accessModifier: AccessModifier.Public,
            isSealed: false,
            isStatic: false,
            isPartial: false,
            baseTypes: ImmutableList<TypeNode>.Empty,
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
            ? new CommentNode(property.Description, CommentKind.Xml)
            : null;
        
        var defaultValue = property.Constraints?.DefaultValue != null
            ? BuildLiteral(property.Constraints.DefaultValue, InferTypeInfo(property.Type))
            : null;
        
        return new PropertyDeclarationNode(
            name: name,
            type: type,
            isRequired: property.IsRequired,
            accessor: PropertyAccessorKind.GetSet,
            defaultValue: defaultValue,
            documentation: comment);
    }
    
    /// <summary>
    /// Строит CompilationUnit из IrEnum
    /// </summary>
    public CompilationUnitNode BuildCompilationUnit(IrEnum enumModel, string? @namespace = null)
    {
        var usings = BuildUsingDirectives(Array.Empty<string>());
        var namespaceNode = BuildNamespace(@namespace ?? string.Empty, new[] { BuildEnum(enumModel) }.ToList<DeclarationNode>());
        
        return new CompilationUnitNode(
            usings: usings,
            namespaces: new[] { namespaceNode }.ToImmutableList(),
            declarations: ImmutableList<DeclarationNode>.Empty);
    }
    
    /// <summary>
    /// Строит EnumDeclarationNode из IrEnum
    /// </summary>
    public EnumDeclarationNode BuildEnum(IrEnum enumModel)
    {
        var name = new IdentifierNode(enumModel.Name);
        var comment = !string.IsNullOrEmpty(enumModel.Description)
            ? new CommentNode(enumModel.Description, CommentKind.Xml)
            : null;
        
        var underlyingType = enumModel.BaseType == IrEnumBaseType.Integer
            ? BuildSimpleType("int")
            : null;
        
        var members = enumModel.Values.Select(v => new EnumMemberDeclarationNode(
            name: new IdentifierNode(v.Name),
            value: v.Value != null ? BuildLiteral(v.Value, TypeInfo.Simple(enumModel.BaseType == IrEnumBaseType.Integer ? "int" : "string")) : null,
            documentation: !string.IsNullOrEmpty(v.Description)
                ? new CommentNode(v.Description, CommentKind.Xml)
                : null)).ToList<DeclarationNode>();
        
        return new EnumDeclarationNode(
            name: name,
            accessModifier: AccessModifier.Public,
            underlyingType: underlyingType,
            members: members.ToImmutableList(),
            documentation: comment);
    }
    
    // Приватные вспомогательные методы для IR
    
    private TypeNode BuildPrimitiveType(IrPrimitiveType primitiveType)
    {
        var typeName = MapPrimitiveToCSharp(primitiveType.PrimitiveKind);
        return new SimpleTypeNode(
            name: new IdentifierNode(typeName),
            isPrimitive: true,
            isNullable: false);
    }
    
    private TypeNode BuildModelReference(IrModelReference modelReference)
    {
        return new SimpleTypeNode(
            name: new IdentifierNode(modelReference.ModelName),
            isPrimitive: false,
            isNullable: false);
    }
    
    private TypeNode BuildArrayType(IrArrayType arrayType)
    {
        var elementType = BuildType(arrayType.ElementType);
        return new ArrayTypeNode(elementType, isNullable: false);
    }
    
    private TypeNode BuildDictionaryType(IrDictionaryType dictionaryType)
    {
        var keyType = BuildType(dictionaryType.KeyType);
        var valueType = BuildType(dictionaryType.ValueType);
        return new DictionaryTypeNode(keyType, valueType, isNullable: false);
    }
    
    private TypeNode BuildUnionType(IrUnionType unionType)
    {
        // В C# union обычно представляется как object или базовый класс
        // Для простоты возвращаем object
        return new SimpleTypeNode(
            name: new IdentifierNode("object"),
            isPrimitive: false,
            isNullable: false);
    }
    
    private TypeNode BuildIntersectionType(IrIntersectionType intersectionType)
    {
        // В C# intersection обычно представляется через наследование
        // Для простоты возвращаем первый тип
        if (intersectionType.Types.Count == 0)
            throw new InvalidOperationException("Intersection type must have at least one type");
        
        return BuildType(intersectionType.Types[0]);
    }
    
    private TypeNode BuildNullableType(IrNullableType nullableType)
    {
        var innerType = BuildType(nullableType.InnerType);
        return new NullableTypeNode(innerType);
    }
    
    private string MapPrimitiveToCSharp(IrPrimitiveKind primitiveKind)
    {
        return primitiveKind switch
        {
            IrPrimitiveKind.String => "string",
            IrPrimitiveKind.Number => "double",
            IrPrimitiveKind.Integer => "int",
            IrPrimitiveKind.Boolean => "bool",
            IrPrimitiveKind.DateTime => "DateTime",
            IrPrimitiveKind.Date => "DateOnly",
            IrPrimitiveKind.Time => "TimeOnly",
            IrPrimitiveKind.Binary => "byte[]",
            IrPrimitiveKind.ByteArray => "byte[]",
            IrPrimitiveKind.Uuid => "Guid",
            IrPrimitiveKind.Any => "object",
            IrPrimitiveKind.Void => "void",
            IrPrimitiveKind.File => "IFormFile",
            _ => "object"
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
            IrTypeKind.Primitive => TypeInfo.Simple(MapPrimitiveToCSharp(((IrPrimitiveType)irType).PrimitiveKind), isPrimitive: true),
            IrTypeKind.ModelReference => TypeInfo.Simple(((IrModelReference)irType).ModelName),
            _ => TypeInfo.Simple("object")
        };
    }
    
    /// <summary>
    /// Строит MethodDeclarationNode из IrOperation
    /// </summary>
    public MethodDeclarationNode BuildMethod(IrOperation operation)
    {
        var name = new IdentifierNode(operation.MethodName);
        
        // Определяем тип возвращаемого значения из responses
        var returnType = ExtractReturnType(operation.Responses);
        
        var comment = !string.IsNullOrEmpty(operation.Description)
            ? new CommentNode(operation.Description, CommentKind.Xml)
            : !string.IsNullOrEmpty(operation.Summary)
                ? new CommentNode(operation.Summary, CommentKind.Xml)
                : null;
        
        var parameters = BuildMethodParameters(operation);
        var body = BuildMethodBody(operation);
        
        return new MethodDeclarationNode(
            name: name,
            returnType: returnType,
            parameters: parameters,
            body: body,
            accessModifier: AccessModifier.Public,
            isAsync: true,
            documentation: comment);
    }
    
    /// <summary>
    /// Строит CompilationUnit из IrDocument (все модели)
    /// </summary>
    public IReadOnlyList<CompilationUnitNode> BuildCompilationUnits(IrDocument document, string? @namespace = null)
    {
        var compilationUnits = new List<CompilationUnitNode>();
        
        // Генерируем модели в топологическом порядке
        var orderedModelNames = document.TypeDependencies.TopologicalOrder;
        var modelsByName = document.Models.ToDictionary(m => m.Name);
        
        foreach (var modelName in orderedModelNames)
        {
            if (modelsByName.TryGetValue(modelName, out var model))
            {
                compilationUnits.Add(BuildCompilationUnit(model, @namespace));
            }
        }
        
        // Добавляем модели, которые не были в топологическом порядке
        foreach (var model in document.Models)
        {
            if (!orderedModelNames.Contains(model.Name))
            {
                compilationUnits.Add(BuildCompilationUnit(model, @namespace));
            }
        }
        
        // Генерируем enum'ы
        foreach (var enumModel in document.Enums)
        {
            compilationUnits.Add(BuildCompilationUnit(enumModel, @namespace));
        }
        
        // Генерируем клиенты
        foreach (var client in document.Clients)
        {
            compilationUnits.Add(BuildCompilationUnit(client, @namespace));
        }
        
        return compilationUnits;
    }
    
    /// <summary>
    /// Строит CompilationUnit из IrClient
    /// </summary>
    public CompilationUnitNode BuildCompilationUnit(IrClient client, string? @namespace = null)
    {
        var usings = BuildUsingDirectives(ExtractDependencies(client));
        var classNode = BuildClientClass(client);
        var namespaceNode = BuildNamespace(@namespace ?? string.Empty, new[] { (DeclarationNode)classNode }.ToList());
        
        return new CompilationUnitNode(
            usings: usings,
            namespaces: new[] { namespaceNode }.ToImmutableList(),
            declarations: ImmutableList<DeclarationNode>.Empty);
    }
    
    // Приватные вспомогательные методы для IR операций
    
    private IReadOnlyList<ParameterDeclarationNode> BuildMethodParameters(IrOperation operation)
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
            // В C# опциональные параметры делаются через nullable или default значения
            var nullableType = param.IsRequired ? type : new NullableTypeNode(type);
            parameters.Add(new ParameterDeclarationNode(
                name: new IdentifierNode(param.Name),
                type: nullableType,
                isRequired: param.IsRequired));
        }
        
        // Header parameters
        foreach (var param in operation.HeaderParameters)
        {
            var type = BuildType(param.Type);
            var nullableType = param.IsRequired ? type : new NullableTypeNode(type);
            parameters.Add(new ParameterDeclarationNode(
                name: new IdentifierNode(param.Name),
                type: nullableType,
                isRequired: param.IsRequired));
        }
        
        // Request body
        if (operation.RequestBody != null)
        {
            var bodyType = BuildType(operation.RequestBody.Type);
            var nullableType = operation.RequestBody.IsRequired ? bodyType : new NullableTypeNode(bodyType);
            parameters.Add(new ParameterDeclarationNode(
                name: new IdentifierNode("request"),
                type: nullableType,
                isRequired: operation.RequestBody.IsRequired));
        }
        
        // CancellationToken
        var cancellationTokenType = new SimpleTypeNode(new IdentifierNode("CancellationToken"), isPrimitive: false);
        parameters.Add(new ParameterDeclarationNode(
            name: new IdentifierNode("cancellationToken"),
            type: cancellationTokenType,
            isRequired: false));
        
        return parameters.ToImmutableList();
    }
    
    private BlockNode? BuildMethodBody(IrOperation operation)
    {
        var statements = new List<StatementNode>();
        
        // Находим success и error response types
        var successResponse = operation.Responses.FirstOrDefault(r =>
        {
            if (int.TryParse(r.StatusCode, out var code))
                return code >= 200 && code < 300;
            return false;
        });
        
        var errorResponses = operation.Responses
            .Where(r => !int.TryParse(r.StatusCode, out var code) || code < 200 || code >= 300)
            .ToList();
        
        var successType = successResponse?.Type != null ? BuildType(successResponse.Type) : null;
        var errorTypes = errorResponses
            .Where(r => r.Type != null)
            .Select(r => new { StatusCode = r.StatusCode, Type = BuildType(r.Type!) })
            .ToList();
        
        // 1. Строим URL через StringBuilder
        var urlBuilderVar = new VariableDeclarationStatementNode(
            name: new IdentifierNode("urlBuilder"),
            type: new SimpleTypeNode(new IdentifierNode("StringBuilder"), isPrimitive: false),
            initializer: new CallExpressionNode(
                target: new IdentifierExpressionNode(new IdentifierNode("StringBuilder")),
                arguments: ImmutableList<ExpressionNode>.Empty,
                metadata: new Dictionary<string, object> { ["IsNewExpression"] = true }));
        statements.Add(urlBuilderVar);
        
        // Строим URL по частям, заменяя {param} на значения
        var basePath = operation.Path;
        var appendMethod = new MemberAccessExpressionNode(
            @object: new IdentifierExpressionNode(new IdentifierNode("urlBuilder")),
            member: new IdentifierNode("Append"));
        
        // Парсим путь и добавляем части в StringBuilder
        var pathParts = new List<(string Text, string? ParamName)>();
        var currentIndex = 0;
        
        while (currentIndex < basePath.Length)
        {
            var paramStart = basePath.IndexOf('{', currentIndex);
            if (paramStart == -1)
            {
                // Остаток пути без параметров
                if (currentIndex < basePath.Length)
                {
                    pathParts.Add((basePath.Substring(currentIndex), null));
                }
                break;
            }
            
            // Добавляем текст до параметра
            if (paramStart > currentIndex)
            {
                pathParts.Add((basePath.Substring(currentIndex, paramStart - currentIndex), null));
            }
            
            // Находим конец параметра
            var paramEnd = basePath.IndexOf('}', paramStart);
            if (paramEnd == -1)
            {
                // Некорректный формат, добавляем как есть
                pathParts.Add((basePath.Substring(paramStart), null));
                break;
            }
            
            // Извлекаем имя параметра
            var paramName = basePath.Substring(paramStart + 1, paramEnd - paramStart - 1);
            var sanitizedParamName = _namingConverter.SanitizeIdentifier(paramName);
            pathParts.Add((Text: (string?)null, ParamName: sanitizedParamName));
            
            currentIndex = paramEnd + 1;
        }
        
        // Добавляем части в StringBuilder
        foreach (var (text, paramName) in pathParts)
        {
            if (text != null)
            {
                var appendTextCall = new CallExpressionNode(
                    target: appendMethod,
                    arguments: new[] { new LiteralExpressionNode($"\"{text}\"", LiteralKind.String) }.ToImmutableList());
                statements.Add(new ExpressionStatementNode(appendTextCall));
            }
            else if (paramName != null)
            {
                // Находим соответствующий path parameter
                var pathParam = operation.PathParameters.FirstOrDefault(p => p.Name == paramName);
                if (pathParam != null)
                {
                    var paramExpr = new IdentifierExpressionNode(new IdentifierNode(pathParam.Name));
                    var paramType = BuildType(pathParam.Type);
                    
                    // Если это строка, используем Uri.EscapeDataString
                    var isStringType = paramType is SimpleTypeNode simple && simple.Name.Name == "string";
                    
                    ExpressionNode paramValue;
                    if (isStringType)
                    {
                        // Uri.EscapeDataString(param)
                        var uriType = new IdentifierExpressionNode(new IdentifierNode("Uri"));
                        var escapeDataString = new MemberAccessExpressionNode(
                            @object: uriType,
                            member: new IdentifierNode("EscapeDataString"));
                        paramValue = new CallExpressionNode(
                            target: escapeDataString,
                            arguments: new[] { paramExpr }.ToImmutableList());
                    }
                    else
                    {
                        // Для не-строк просто ToString()
                        var toStringMethod = new MemberAccessExpressionNode(
                            @object: paramExpr,
                            member: new IdentifierNode("ToString"));
                        paramValue = new CallExpressionNode(
                            target: toStringMethod,
                            arguments: ImmutableList<ExpressionNode>.Empty);
                    }
                    
                    var appendParamCall = new CallExpressionNode(
                        target: appendMethod,
                        arguments: new[] { paramValue }.ToImmutableList());
                    statements.Add(new ExpressionStatementNode(appendParamCall));
                }
            }
        }
        
        // Добавляем query parameters
        if (operation.QueryParameters.Count > 0)
        {
            var firstParam = operation.QueryParameters[0];
            var appendQuerySeparator = new CallExpressionNode(
                target: appendMethod,
                arguments: new[] { new LiteralExpressionNode("\"?\"", LiteralKind.String) }.ToImmutableList());
            statements.Add(new ExpressionStatementNode(appendQuerySeparator));
            
            for (int i = 0; i < operation.QueryParameters.Count; i++)
            {
                var queryParam = operation.QueryParameters[i];
                if (i > 0)
                {
                    var appendAnd = new CallExpressionNode(
                        target: appendMethod,
                        arguments: new[] { new LiteralExpressionNode("\"&\"", LiteralKind.String) }.ToImmutableList());
                    statements.Add(new ExpressionStatementNode(appendAnd));
                }
                
                var paramName = new LiteralExpressionNode($"\"{queryParam.Name}=\"", LiteralKind.String);
                var appendName = new CallExpressionNode(
                    target: appendMethod,
                    arguments: new[] { paramName }.ToImmutableList());
                statements.Add(new ExpressionStatementNode(appendName));
                
                var paramExpr = new IdentifierExpressionNode(new IdentifierNode(queryParam.Name));
                var paramType = BuildType(queryParam.Type);
                var isStringType = paramType is SimpleTypeNode simple && simple.Name.Name == "string";
                
                ExpressionNode paramValue;
                if (isStringType)
                {
                    var uriType = new IdentifierExpressionNode(new IdentifierNode("Uri"));
                    var escapeDataString = new MemberAccessExpressionNode(
                        @object: uriType,
                        member: new IdentifierNode("EscapeDataString"));
                    paramValue = new CallExpressionNode(
                        target: escapeDataString,
                        arguments: new[] { paramExpr }.ToImmutableList());
                }
                else
                {
                    var toStringMethod = new MemberAccessExpressionNode(
                        @object: paramExpr,
                        member: new IdentifierNode("ToString"));
                    paramValue = new CallExpressionNode(
                        target: toStringMethod,
                        arguments: ImmutableList<ExpressionNode>.Empty);
                }
                
                var appendValue = new CallExpressionNode(
                    target: appendMethod,
                    arguments: new[] { paramValue }.ToImmutableList());
                statements.Add(new ExpressionStatementNode(appendValue));
            }
        }
        
        // Получаем финальный URL: urlBuilder.ToString()
        var urlBuilderToString = new MemberAccessExpressionNode(
            @object: new IdentifierExpressionNode(new IdentifierNode("urlBuilder")),
            member: new IdentifierNode("ToString"));
        var urlStringVar = new VariableDeclarationStatementNode(
            name: new IdentifierNode("url"),
            type: new SimpleTypeNode(new IdentifierNode("string"), isPrimitive: true),
            initializer: new CallExpressionNode(
                target: urlBuilderToString,
                arguments: ImmutableList<ExpressionNode>.Empty));
        statements.Add(urlStringVar);
        
        // 2. Создаем HttpRequestMessage
        var httpMethodType = new IdentifierExpressionNode(new IdentifierNode("HttpMethod"));
        var httpMethodValue = new LiteralExpressionNode($"\"{operation.HttpMethod}\"", LiteralKind.String);
        var httpMethodCtor = new CallExpressionNode(
            target: httpMethodType,
            arguments: new[] { httpMethodValue }.ToImmutableList(),
            metadata: new Dictionary<string, object> { ["IsNewExpression"] = true });
        
        var requestMessageType = new IdentifierExpressionNode(new IdentifierNode("HttpRequestMessage"));
        var urlExpr = new IdentifierExpressionNode(new IdentifierNode("url"));
        var requestMessageCtorArgs = new ExpressionNode[] { httpMethodCtor, urlExpr };
        var requestMessageCtor = new CallExpressionNode(
            target: requestMessageType,
            arguments: requestMessageCtorArgs.ToImmutableList(),
            metadata: new Dictionary<string, object> { ["IsNewExpression"] = true });
        
        var requestVar = new VariableDeclarationStatementNode(
            name: new IdentifierNode("request"),
            type: new SimpleTypeNode(new IdentifierNode("HttpRequestMessage"), isPrimitive: false),
            initializer: requestMessageCtor);
        statements.Add(requestVar);
        
        // 3. Устанавливаем Headers (Accept: application/json)
        var requestHeaders = new MemberAccessExpressionNode(
            @object: new IdentifierExpressionNode(new IdentifierNode("request")),
            member: new IdentifierNode("Headers"));
        var acceptHeader = new MemberAccessExpressionNode(
            @object: requestHeaders,
            member: new IdentifierNode("Accept"));
        var acceptAdd = new MemberAccessExpressionNode(
            @object: acceptHeader,
            member: new IdentifierNode("Add"));
        var mediaTypeJson = new IdentifierExpressionNode(new IdentifierNode("MediaTypeWithQualityHeaderValue"));
        var mediaTypeJsonCtor = new CallExpressionNode(
            target: mediaTypeJson,
            arguments: new[] { new LiteralExpressionNode("\"application/json\"", LiteralKind.String) }.ToImmutableList(),
            metadata: new Dictionary<string, object> { ["IsNewExpression"] = true });
        var acceptAddCall = new CallExpressionNode(
            target: acceptAdd,
            arguments: new[] { mediaTypeJsonCtor }.ToImmutableList());
        statements.Add(new ExpressionStatementNode(acceptAddCall));
        
        // 4. Добавляем request body если есть
        if (operation.RequestBody != null)
        {
            var requestBodyParam = new IdentifierExpressionNode(new IdentifierNode("request")); // Параметр метода request body
            var jsonSerializerForSerialize = new IdentifierExpressionNode(new IdentifierNode("JsonSerializer"));
            var serializeMethod = new MemberAccessExpressionNode(
                @object: jsonSerializerForSerialize,
                member: new IdentifierNode("Serialize"));
            var serializeCall = new CallExpressionNode(
                target: serializeMethod,
                arguments: new[] { requestBodyParam }.ToImmutableList());
            
            var stringContentTypeExpr = new IdentifierExpressionNode(new IdentifierNode("StringContent"));
            var encodingExpr = new IdentifierExpressionNode(new IdentifierNode("Encoding"));
            var encodingUtf8 = new MemberAccessExpressionNode(
                @object: encodingExpr,
                member: new IdentifierNode("UTF8"));
            var mediaTypeHeader = new LiteralExpressionNode("application/json", LiteralKind.String);
            
            var stringContentCtorArgs = new ExpressionNode[] { serializeCall, encodingUtf8, mediaTypeHeader };
            var stringContentCtor = new CallExpressionNode(
                target: stringContentTypeExpr,
                arguments: stringContentCtorArgs.ToImmutableList(),
                metadata: new Dictionary<string, object> { ["IsNewExpression"] = true });
            
            var requestContent = new MemberAccessExpressionNode(
                @object: new IdentifierExpressionNode(new IdentifierNode("request")),
                member: new IdentifierNode("Content"));
            var contentAssign = new BinaryExpressionNode(
                left: requestContent,
                @operator: BinaryOperator.Add,
                right: stringContentCtor,
                metadata: new Dictionary<string, object> { ["IsAssignment"] = true });
            statements.Add(new ExpressionStatementNode(contentAssign));
        }
        
        // 5. Отправляем запрос через HttpClient.SendAsync с using
        var httpClientField = new IdentifierExpressionNode(new IdentifierNode("_httpClient"));
        var sendAsyncMethod = new MemberAccessExpressionNode(
            @object: httpClientField,
            member: new IdentifierNode("SendAsync"));
        var cancellationTokenParam = new IdentifierExpressionNode(new IdentifierNode("cancellationToken"));
        var sendAsyncCall = new CallExpressionNode(
            target: sendAsyncMethod,
            arguments: new[] { new IdentifierExpressionNode(new IdentifierNode("request")), cancellationTokenParam }.ToImmutableList());
        var awaitSend = new AwaitExpressionNode(sendAsyncCall);
        
        var responseVar = new VariableDeclarationStatementNode(
            name: new IdentifierNode("response"),
            type: new SimpleTypeNode(new IdentifierNode("HttpResponseMessage"), isPrimitive: false),
            initializer: awaitSend);
        statements.Add(responseVar);
        
        // 6. Обрабатываем response с использованием using и проверкой статус кода
        // ReadAsStringAsync
        var contentAccess = new MemberAccessExpressionNode(
            @object: new IdentifierExpressionNode(new IdentifierNode("response")),
            member: new IdentifierNode("Content"));
        var readAsString = new MemberAccessExpressionNode(
            @object: contentAccess,
            member: new IdentifierNode("ReadAsStringAsync"));
        var readAsStringCall = new CallExpressionNode(
            target: readAsString,
            arguments: ImmutableList<ExpressionNode>.Empty);
        var awaitRead = new AwaitExpressionNode(readAsStringCall);
        
        var contentVar = new VariableDeclarationStatementNode(
            name: new IdentifierNode("content"),
            type: new SimpleTypeNode(new IdentifierNode("string"), isPrimitive: true),
            initializer: awaitRead);
        statements.Add(contentVar);
        
        // Проверяем статус код
        var statusCodeAccess = new MemberAccessExpressionNode(
            @object: new IdentifierExpressionNode(new IdentifierNode("response")),
            member: new IdentifierNode("StatusCode"));
        var statusCodeVar = new VariableDeclarationStatementNode(
            name: new IdentifierNode("statusCode"),
            type: new SimpleTypeNode(new IdentifierNode("HttpStatusCode"), isPrimitive: false),
            initializer: statusCodeAccess);
        statements.Add(statusCodeVar);
        
        // Проверяем статус код - используем (int)statusCode для получения числового значения
        var statusCodeExpr = new IdentifierExpressionNode(new IdentifierNode("statusCode"));
        // Создаем cast expression через BinaryExpression с metadata
        var castExpr = new BinaryExpressionNode(
            left: statusCodeExpr,
            @operator: BinaryOperator.Add, // Временно, будет заменено в рендерере
            right: new LiteralExpressionNode("0", LiteralKind.Number),
            metadata: new Dictionary<string, object> { ["IsCastExpression"] = true, ["CastTargetType"] = "int" });
        var intStatusCodeVar = new VariableDeclarationStatementNode(
            name: new IdentifierNode("statusCodeInt"),
            type: new SimpleTypeNode(new IdentifierNode("int"), isPrimitive: true),
            initializer: castExpr);
        statements.Add(intStatusCodeVar);
        
        var isSuccess = new BinaryExpressionNode(
            left: new IdentifierExpressionNode(new IdentifierNode("statusCodeInt")),
            @operator: BinaryOperator.GreaterThanOrEqual,
            right: new LiteralExpressionNode("200", LiteralKind.Number));
        var isSuccessAnd = new BinaryExpressionNode(
            left: isSuccess,
            @operator: BinaryOperator.LogicalAnd,
            right: new BinaryExpressionNode(
                left: new IdentifierExpressionNode(new IdentifierNode("statusCodeInt")),
                @operator: BinaryOperator.LessThan,
                right: new LiteralExpressionNode("300", LiteralKind.Number)));
        
        // If statement для обработки success/error
        var ifStatements = new List<StatementNode>();
        var elseStatements = new List<StatementNode>();
        
        // Success branch: десериализуем в success type
        if (successType != null)
        {
            var jsonSerializerExpr = new IdentifierExpressionNode(new IdentifierNode("JsonSerializer"));
            var deserializeAccess = new MemberAccessExpressionNode(
                @object: jsonSerializerExpr,
                member: new IdentifierNode("Deserialize"));
            var deserializeMetadata = new Dictionary<string, object> { ["GenericTypeArgument"] = successType };
            var deserializeCall = new CallExpressionNode(
                target: deserializeAccess,
                arguments: new[] { new IdentifierExpressionNode(new IdentifierNode("content")) }.ToImmutableList(),
                metadata: deserializeMetadata);
            var returnStatement = new ReturnStatementNode(deserializeCall);
            ifStatements.Add(returnStatement);
        }
        else
        {
            var nullReturn = new ReturnStatementNode(null);
            ifStatements.Add(nullReturn);
        }
        
        // Error branch: десериализуем в error type и выбрасываем исключение
        if (errorTypes.Count > 0)
        {
            // Используем первый error type (можно улучшить для множественных error types)
            var errorType = errorTypes[0].Type;
            var jsonSerializerExpr = new IdentifierExpressionNode(new IdentifierNode("JsonSerializer"));
            var deserializeAccess = new MemberAccessExpressionNode(
                @object: jsonSerializerExpr,
                member: new IdentifierNode("Deserialize"));
            var deserializeMetadata = new Dictionary<string, object> { ["GenericTypeArgument"] = errorType };
            var deserializeErrorCall = new CallExpressionNode(
                target: deserializeAccess,
                arguments: new[] { new IdentifierExpressionNode(new IdentifierNode("content")) }.ToImmutableList(),
                metadata: deserializeMetadata);
            
            var errorVar = new VariableDeclarationStatementNode(
                name: new IdentifierNode("error"),
                type: errorType,
                initializer: deserializeErrorCall);
            elseStatements.Add(errorVar);
            
            // Выбрасываем исключение
            var apiExceptionType = new IdentifierExpressionNode(new IdentifierNode("ApiException"));
            var statusCodeForException = new IdentifierExpressionNode(new IdentifierNode("statusCodeInt"));
            var errorForException = new IdentifierExpressionNode(new IdentifierNode("error"));
            var apiExceptionCtor = new CallExpressionNode(
                target: apiExceptionType,
                arguments: new[] { statusCodeForException, errorForException }.ToImmutableList(),
                metadata: new Dictionary<string, object> { ["IsNewExpression"] = true });
            var throwStatement = new ThrowStatementNode(apiExceptionCtor);
            elseStatements.Add(throwStatement);
        }
        else
        {
            // Если нет error type, просто выбрасываем общее исключение
            var apiExceptionType = new IdentifierExpressionNode(new IdentifierNode("ApiException"));
            var statusCodeForException = new IdentifierExpressionNode(new IdentifierNode("statusCodeInt"));
            var messageForException = new LiteralExpressionNode("\"Unexpected status code\"", LiteralKind.String);
            var apiExceptionCtorArgs = new ExpressionNode[] { statusCodeForException, messageForException };
            var apiExceptionCtor = new CallExpressionNode(
                target: apiExceptionType,
                arguments: apiExceptionCtorArgs.ToImmutableList(),
                metadata: new Dictionary<string, object> { ["IsNewExpression"] = true });
            var throwStatement = new ThrowStatementNode(apiExceptionCtor);
            elseStatements.Add(throwStatement);
        }
        
        var thenBlock = new BlockNode(ifStatements.ToImmutableList());
        var elseBlock = new BlockNode(elseStatements.ToImmutableList());
        var ifStatement = new IfStatementNode(
            condition: isSuccessAnd,
            thenBlock: thenBlock,
            elseBlock: elseBlock);
        statements.Add(ifStatement);
        
        // Dispose для request и response (через using в рендерере или явно)
        // В C# using statement будет обработан в рендерере, но можно добавить явный Dispose
        var requestDispose = new MemberAccessExpressionNode(
            @object: new IdentifierExpressionNode(new IdentifierNode("request")),
            member: new IdentifierNode("Dispose"));
        var requestDisposeCall = new CallExpressionNode(
            target: requestDispose,
            arguments: ImmutableList<ExpressionNode>.Empty);
        statements.Add(new ExpressionStatementNode(requestDisposeCall));
        
        var responseDispose = new MemberAccessExpressionNode(
            @object: new IdentifierExpressionNode(new IdentifierNode("response")),
            member: new IdentifierNode("Dispose"));
        var responseDisposeCall = new CallExpressionNode(
            target: responseDispose,
            arguments: ImmutableList<ExpressionNode>.Empty);
        statements.Add(new ExpressionStatementNode(responseDisposeCall));
        
        return new BlockNode(statements.ToImmutableList());
    }
    
    
    private ExpressionNode BuildUrlExpression(IrOperation operation)
    {
        // Строим строку с интерполяцией для path parameters
        var route = operation.Path;
        var parts = new List<string>();
        var currentIndex = 0;
        
        // Парсим путь и заменяем {param} на интерполяцию
        while (currentIndex < route.Length)
        {
            var paramStart = route.IndexOf('{', currentIndex);
            if (paramStart == -1)
            {
                // Остаток пути без параметров
                if (currentIndex < route.Length)
                {
                    parts.Add(route.Substring(currentIndex));
                }
                break;
            }
            
            // Добавляем текст до параметра
            if (paramStart > currentIndex)
            {
                parts.Add(route.Substring(currentIndex, paramStart - currentIndex));
            }
            
            // Находим конец параметра
            var paramEnd = route.IndexOf('}', paramStart);
            if (paramEnd == -1)
            {
                // Некорректный формат, добавляем как есть
                parts.Add(route.Substring(paramStart));
                break;
            }
            
            // Извлекаем имя параметра
            var paramName = route.Substring(paramStart + 1, paramEnd - paramStart - 1);
            var sanitizedParamName = _namingConverter.SanitizeIdentifier(paramName);
            
            // Находим соответствующий path parameter
            var pathParam = operation.PathParameters.FirstOrDefault(p => p.Name == sanitizedParamName);
            if (pathParam != null)
            {
                // Добавляем интерполяцию: {paramName}
                parts.Add($"{{{sanitizedParamName}}}");
            }
            else
            {
                // Параметр не найден, добавляем как есть
                parts.Add(route.Substring(paramStart, paramEnd - paramStart + 1));
            }
            
            currentIndex = paramEnd + 1;
        }
        
        // Собираем строку с интерполяцией
        var interpolatedRoute = string.Join("", parts);
        
        // Если есть path parameters, создаем строку с интерполяцией
        // Используем простую конкатенацию строк для упрощения
        // В будущем можно создать специальный узел для интерполяции
        if (operation.PathParameters.Count > 0)
        {
            // Для упрощения используем конкатенацию строк
            // Например: "/api/users/" + userId + "/posts"
            var expressions = new List<ExpressionNode>();
            var currentPart = "";
            
            foreach (var part in parts)
            {
                if (part.StartsWith("{") && part.EndsWith("}"))
                {
                    // Это параметр
                    if (!string.IsNullOrEmpty(currentPart))
                    {
                        expressions.Add(new LiteralExpressionNode($"\"{currentPart}\"", LiteralKind.String));
                        currentPart = "";
                    }
                    var paramName = part.Substring(1, part.Length - 2);
                    var sanitizedParamName = _namingConverter.SanitizeIdentifier(paramName);
                    expressions.Add(new IdentifierExpressionNode(new IdentifierNode(sanitizedParamName)));
                }
                else
                {
                    currentPart += part;
                }
            }
            
            if (!string.IsNullOrEmpty(currentPart))
            {
                expressions.Add(new LiteralExpressionNode($"\"{currentPart}\"", LiteralKind.String));
            }
            
            // Если есть только один элемент, возвращаем его
            if (expressions.Count == 1)
            {
                return expressions[0];
            }
            
            // Создаем цепочку конкатенаций: expr1 + expr2 + expr3
            ExpressionNode result = expressions[0];
            for (int i = 1; i < expressions.Count; i++)
            {
                result = new BinaryExpressionNode(
                    left: result,
                    @operator: BinaryOperator.Add,
                    right: expressions[i]);
            }
            
            return result;
        }
        
        return new LiteralExpressionNode($"\"{route}\"", LiteralKind.String);
    }
    
    private string GetHttpClientMethodName(string httpMethod)
    {
        return httpMethod.ToUpperInvariant() switch
        {
            "GET" => "GetAsync",
            "POST" => "PostAsync",
            "PUT" => "PutAsync",
            "DELETE" => "DeleteAsync",
            "PATCH" => "PatchAsync",
            _ => "SendAsync"
        };
    }
    
    /// <summary>
    /// Строит HTTP узлы из IrOperation (для использования в других местах)
    /// </summary>
    private (HttpMethodNode Method, RouteNode Route, RequestBodyNode? Body, IReadOnlyList<QueryParameterNode> QueryParams, IReadOnlyList<HeaderParameterNode> HeaderParams) BuildHttpNodes(IrOperation operation)
    {
        var method = _httpBuilder.BuildHttpMethod(operation.HttpMethod);
        var route = _httpBuilder.BuildRoute(operation.Path, operation.PathParameters, BuildType);
        
        var queryParams = operation.QueryParameters
            .Select(p => _httpBuilder.BuildQueryParameter(p, BuildType))
            .ToImmutableList();
        
        var headerParams = operation.HeaderParameters
            .Select(p => _httpBuilder.BuildHeaderParameter(p, BuildType))
            .ToImmutableList();
        
        var body = operation.RequestBody != null
            ? _httpBuilder.BuildRequestBody(operation.RequestBody, BuildType)
            : null;
        
        return (method, route, body, queryParams, headerParams);
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
            // Для async методов оборачиваем тип в Task<T>
            var responseType = BuildType(successResponse.Type);
            return new GenericTypeNode(
                name: new IdentifierNode("Task"),
                typeArguments: new[] { responseType }.ToImmutableList(),
                isNullable: false);
        }
        
        // Если нет успешного ответа, возвращаем Task (для async методов без возвращаемого значения)
        return new SimpleTypeNode(
            name: new IdentifierNode("Task"),
            isPrimitive: false,
            isNullable: false,
            isOptional: false);
    }
    
    private ClassDeclarationNode BuildClientClass(IrClient client)
    {
        var name = new IdentifierNode(client.Name);
        var members = new List<DeclarationNode>();
        
        // Добавляем поле _httpClient
        var httpClientField = new FieldDeclarationNode(
            name: new IdentifierNode("_httpClient"),
            type: new SimpleTypeNode(new IdentifierNode("HttpClient"), isPrimitive: false),
            accessModifier: AccessModifier.Private,
            isReadOnly: true,
            defaultValue: null);
        members.Add(httpClientField);
        
        // Добавляем конструктор
        var constructorParams = new List<ParameterDeclarationNode>
        {
            new ParameterDeclarationNode(
                name: new IdentifierNode("httpClient"),
                type: new SimpleTypeNode(new IdentifierNode("HttpClient"), isPrimitive: false),
                isRequired: true)
        };
        
        // Создаем присваивание через BinaryExpression с Add (временно, будет обработано в рендерере как присваивание)
        // Или используем простой expression statement - в C# присваивание это statement, не expression
        // Пока создаем пустое тело, присваивание будет сделано в рендерере через специальную логику
        // Или используем BinaryExpression с кастомным оператором через metadata
        var assignmentMetadata = new Dictionary<string, object> { ["IsAssignment"] = true };
        var assignmentExpr = new BinaryExpressionNode(
            left: new IdentifierExpressionNode(new IdentifierNode("_httpClient")),
            @operator: BinaryOperator.Add, // Временно, будет заменено в рендерере
            right: new IdentifierExpressionNode(new IdentifierNode("httpClient")),
            metadata: assignmentMetadata);
        
        var constructorBody = new BlockNode(new List<StatementNode>
        {
            new ExpressionStatementNode(assignmentExpr)
        }.ToImmutableList());
        
        var constructor = new ConstructorDeclarationNode(
            name: name,
            parameters: constructorParams.ToImmutableList(),
            body: constructorBody,
            accessModifier: AccessModifier.Public);
        members.Add(constructor);
        
        // Добавляем методы
        var methods = client.Operations.Select(BuildMethod).ToList<DeclarationNode>();
        members.AddRange(methods);
        
        return new ClassDeclarationNode(
            name: name,
            accessModifier: AccessModifier.Public,
            members: members.ToImmutableList());
    }
    
    private IReadOnlyList<string> ExtractDependencies(IrClient client)
    {
        var dependencies = new HashSet<string>();
        
        // Добавляем необходимые using для HTTP клиентов
        dependencies.Add("System");
        dependencies.Add("System.Net.Http");
        dependencies.Add("System.Text");
        dependencies.Add("System.Text.Json");
        dependencies.Add("System.Threading");
        dependencies.Add("System.Threading.Tasks");
        
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
