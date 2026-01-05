using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.Interfaces;
using ValIO.Core.IR;
using ValIO.Core.IR.Enums;
using ValIO.Core.IR.Types;
using ValIO.Core.Models;
using ValIO.Core.Models.TemplateData;
using ValIO.Generators.CSharp.Models;

namespace ValIO.Generators.CSharp.Ast.Builders;

/// <summary>
/// Построитель AST для C# (Builder Pattern)
/// </summary>
public class CSharpAstBuilder : ICSharpAstBuilder
{
    private readonly INamingConverter _namingConverter;
    private readonly ITypeMapper _typeMapper;
    
    public CSharpAstBuilder(INamingConverter namingConverter, ITypeMapper typeMapper)
    {
        _namingConverter = namingConverter ?? throw new ArgumentNullException(nameof(namingConverter));
        _typeMapper = typeMapper ?? throw new ArgumentNullException(nameof(typeMapper));
    }
    
    public CompilationUnitNode BuildCompilationUnit(ModelTemplateData<CSharpModelTemplateDataOptions, CSharpPropertyTemplateDataOptions> data)
    {
        var usings = BuildUsingDirectives(data.Dependencies);
        var namespaceNode = BuildNamespace(data.Namespace ?? string.Empty, BuildTypeDeclaration(data));
        
        return new CompilationUnitNode(
            usings: usings,
            namespaces: new[] { namespaceNode }.ToImmutableList(),
            declarations: ImmutableList<DeclarationNode>.Empty);
    }
    
    public CompilationUnitNode BuildCompilationUnit(ClientTemplateData<CSharpClientTemplateDataOptions, CSharpOperationTemplateDataOptions> data)
    {
        var usings = BuildUsingDirectives(data.Dependencies);
        var namespaceNode = BuildNamespace(data.Namespace ?? string.Empty, BuildClientClass(data));
        
        return new CompilationUnitNode(
            usings: usings,
            namespaces: new[] { namespaceNode }.ToImmutableList(),
            declarations: ImmutableList<DeclarationNode>.Empty);
    }
    
    public CompilationUnitNode BuildCompilationUnit(EnumTemplateData<CSharpEnumTemplateDataOptions> data)
    {
        var usings = BuildUsingDirectives(Array.Empty<string>());
        var namespaceNode = BuildNamespace(data.Namespace ?? string.Empty, BuildEnum(data));
        
        return new CompilationUnitNode(
            usings: usings,
            namespaces: new[] { namespaceNode }.ToImmutableList(),
            declarations: ImmutableList<DeclarationNode>.Empty);
    }
    
    public NamespaceDeclarationNode BuildNamespace(string name, IReadOnlyList<DeclarationNode> declarations)
    {
        var nameIdentifier = new IdentifierNode(name);
        return new NamespaceDeclarationNode(
            name: nameIdentifier,
            declarations: declarations.ToImmutableList());
    }
    
    public ClassDeclarationNode BuildClass(ModelTemplateData<CSharpModelTemplateDataOptions, CSharpPropertyTemplateDataOptions> data)
    {
        var name = new IdentifierNode(data.Name);
        var comment = !string.IsNullOrEmpty(data.Description) 
            ? new CommentNode(data.Description, CommentKind.Xml) 
            : null;
        
        var properties = data.Properties.Select(BuildProperty).ToList<DeclarationNode>();
        var baseTypes = !string.IsNullOrEmpty(data.BaseClassName)
            ? new[] { BuildSimpleType(data.BaseClassName) }.ToImmutableList<TypeNode>()
            : ImmutableList<TypeNode>.Empty;
        
        return new ClassDeclarationNode(
            name: name,
            accessModifier: AccessModifier.Public,
            isSealed: data.Options.IsSealed,
            isStatic: false,
            isPartial: false,
            baseTypes: baseTypes,
            members: properties.ToImmutableList(),
            documentation: comment);
    }
    
    public RecordDeclarationNode BuildRecord(ModelTemplateData<CSharpModelTemplateDataOptions, CSharpPropertyTemplateDataOptions> data)
    {
        var name = new IdentifierNode(data.Name);
        var comment = !string.IsNullOrEmpty(data.Description) 
            ? new CommentNode(data.Description, CommentKind.Xml) 
            : null;
        
        var properties = data.Properties.Select(BuildProperty).ToList<DeclarationNode>();
        var baseTypes = !string.IsNullOrEmpty(data.BaseClassName)
            ? new[] { BuildSimpleType(data.BaseClassName) }.ToImmutableList<TypeNode>()
            : ImmutableList<TypeNode>.Empty;
        
        return new RecordDeclarationNode(
            name: name,
            accessModifier: AccessModifier.Public,
            isRecordStruct: data.Options.ModelType == ModelType.RecordStruct,
            primaryConstructorParameters: ImmutableList<ParameterDeclarationNode>.Empty,
            baseTypes: baseTypes,
            members: properties.ToImmutableList(),
            documentation: comment);
    }
    
    public StructDeclarationNode BuildStruct(ModelTemplateData<CSharpModelTemplateDataOptions, CSharpPropertyTemplateDataOptions> data)
    {
        var name = new IdentifierNode(data.Name);
        var comment = !string.IsNullOrEmpty(data.Description) 
            ? new CommentNode(data.Description, CommentKind.Xml) 
            : null;
        
        var properties = data.Properties.Select(BuildProperty).ToList<DeclarationNode>();
        
        return new StructDeclarationNode(
            name: name,
            accessModifier: AccessModifier.Public,
            isReadOnly: false,
            baseTypes: ImmutableList<TypeNode>.Empty,
            members: properties.ToImmutableList(),
            documentation: comment);
    }
    
    public PropertyDeclarationNode BuildProperty(PropertyTemplateData<CSharpPropertyTemplateDataOptions> data)
    {
        var name = new IdentifierNode(data.Name);
        var type = BuildType(data.TypeInfo);
        var comment = !string.IsNullOrEmpty(data.Description) 
            ? new CommentNode(data.Description, CommentKind.Xml) 
            : null;
        
        var accessor = data.Options.AccessorType == PropertyAccessorType.Init
            ? PropertyAccessorKind.GetInit
            : PropertyAccessorKind.GetSet;
        
        var defaultValue = data.DefaultValue != null
            ? BuildLiteral(data.DefaultValue, data.TypeInfo)
            : null;
        
        return new PropertyDeclarationNode(
            name: name,
            type: type,
            isRequired: data.IsRequired,
            accessor: accessor,
            defaultValue: defaultValue,
            documentation: comment);
    }
    
    public MethodDeclarationNode BuildMethod(OperationTemplateData<CSharpOperationTemplateDataOptions> data)
    {
        var name = new IdentifierNode(data.MethodName);
        var returnType = BuildType(data.ReturnTypeInfo);
        var comment = !string.IsNullOrEmpty(data.Description) 
            ? new CommentNode(data.Description, CommentKind.Xml) 
            : null;
        
        var parameters = BuildMethodParameters(data);
        var body = BuildMethodBody(data);
        
        return new MethodDeclarationNode(
            name: name,
            returnType: returnType,
            parameters: parameters,
            body: body,
            isAsync: true,
            accessModifier: AccessModifier.Public,
            documentation: comment);
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
        
        return new LiteralExpressionNode(value, literalKind);
    }
    
    // Приватные вспомогательные методы
    
    private IReadOnlyList<UsingDirectiveNode> BuildUsingDirectives(IReadOnlyList<string> dependencies)
    {
        return dependencies
            .Select(dep => new UsingDirectiveNode(new IdentifierNode(dep)))
            .ToImmutableList();
    }
    
    private IReadOnlyList<DeclarationNode> BuildTypeDeclaration(ModelTemplateData<CSharpModelTemplateDataOptions, CSharpPropertyTemplateDataOptions> data)
    {
        return data.Options.ModelType switch
        {
            ModelType.Class => new[] { (DeclarationNode)BuildClass(data) },
            ModelType.Record or ModelType.RecordStruct => new[] { (DeclarationNode)BuildRecord(data) },
            ModelType.Struct => new[] { (DeclarationNode)BuildStruct(data) },
            _ => new[] { (DeclarationNode)BuildClass(data) }
        };
    }
    
    private ClassDeclarationNode BuildClientClass(ClientTemplateData<CSharpClientTemplateDataOptions, CSharpOperationTemplateDataOptions> data)
    {
        var name = new IdentifierNode(data.Name);
        var methods = data.Operations.Select(BuildMethod).ToList<DeclarationNode>();
        
        return new ClassDeclarationNode(
            name: name,
            accessModifier: AccessModifier.Public,
            members: methods.ToImmutableList());
    }
    
    private EnumDeclarationNode BuildEnum(EnumTemplateData<CSharpEnumTemplateDataOptions> data)
    {
        var name = new IdentifierNode(data.Name);
        var comment = !string.IsNullOrEmpty(data.Description) 
            ? new CommentNode(data.Description, CommentKind.Xml) 
            : null;
        
        var underlyingType = !string.IsNullOrEmpty(data.UnderlyingType)
            ? BuildSimpleType(data.UnderlyingType)
            : null;
        
        var members = data.Values.Select(v => new EnumMemberDeclarationNode(
            name: new IdentifierNode(v.Name),
            value: v.Value != null ? BuildLiteral(v.Value, TypeInfo.Simple("int")) : null,
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
    
    private IReadOnlyList<ParameterDeclarationNode> BuildMethodParameters(OperationTemplateData<CSharpOperationTemplateDataOptions> data)
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
        if (data.HasRequestBody)
        {
            var bodyType = BuildType(data.RequestBodyTypeInfo);
            parameters.Add(new ParameterDeclarationNode(
                name: new IdentifierNode("requestBody"),
                type: bodyType,
                isRequired: data.IsRequestBodyRequired));
        }
        
        return parameters.ToImmutableList();
    }
    
    private BlockNode? BuildMethodBody(OperationTemplateData<CSharpOperationTemplateDataOptions> data)
    {
        // TODO: Реализовать построение тела метода
        // Пока возвращаем null для абстрактного метода
        return null;
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
            ? new CommentNode(operation.Description, CommentKind.XmlDoc)
            : !string.IsNullOrEmpty(operation.Summary)
                ? new CommentNode(operation.Summary, CommentKind.XmlDoc)
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
            isStatic: false,
            documentation: comment);
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
        
        return parameters.ToImmutableList();
    }
    
    private BlockNode? BuildMethodBody(IrOperation operation)
    {
        // TODO: Полная реализация построения тела метода с использованием HTTP узлов
        // Пока возвращаем null для type-only метода
        return null;
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
        
        // Если нет успешного ответа, возвращаем Task (для async методов)
        return new SimpleTypeNode(
            name: new IdentifierNode("Task"),
            isPrimitive: false,
            isNullable: false,
            isOptional: false);
    }
    
    private ClassDeclarationNode BuildClientClass(IrClient client)
    {
        var name = new IdentifierNode(client.Name);
        var methods = client.Operations.Select(BuildMethod).ToList<DeclarationNode>();
        
        return new ClassDeclarationNode(
            name: name,
            accessModifier: AccessModifier.Public,
            members: methods.ToImmutableList());
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
