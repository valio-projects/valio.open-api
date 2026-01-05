using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.Interfaces;
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
}
