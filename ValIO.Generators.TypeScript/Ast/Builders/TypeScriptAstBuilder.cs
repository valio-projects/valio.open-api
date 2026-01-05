using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.Interfaces;
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
    
    public TypeScriptAstBuilder(INamingConverter namingConverter, ITypeMapper typeMapper)
    {
        _namingConverter = namingConverter ?? throw new ArgumentNullException(nameof(namingConverter));
        _typeMapper = typeMapper ?? throw new ArgumentNullException(nameof(typeMapper));
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
        // TODO: Реализовать построение тела функции
        // Пока возвращаем null для type-only функции
        return null;
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
}
