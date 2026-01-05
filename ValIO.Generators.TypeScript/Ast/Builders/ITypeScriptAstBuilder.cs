using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Http;
using ValIO.Core.Interfaces;
using ValIO.Core.IR;
using ValIO.Core.IR.Types;
using ValIO.Core.Models;
using ValIO.Core.Models.TemplateData;
using ValIO.Generators.TypeScript.Models;

namespace ValIO.Generators.TypeScript.Ast.Builders;

/// <summary>
/// Интерфейс построителя AST для TypeScript
/// </summary>
public interface ITypeScriptAstBuilder
{
    /// <summary>
    /// Строит SourceFile из ModelTemplateData
    /// </summary>
    SourceFileNode BuildSourceFile(ModelTemplateData<TypeScriptModelTemplateDataOptions, TypeScriptPropertyTemplateDataOptions> data);
    
    /// <summary>
    /// Строит SourceFile из ClientTemplateData
    /// </summary>
    SourceFileNode BuildSourceFile(ClientTemplateData<TypeScriptClientTemplateDataOptions, TypeScriptOperationTemplateDataOptions> data);
    
    /// <summary>
    /// Строит SourceFile из EnumTemplateData
    /// </summary>
    SourceFileNode BuildSourceFile(EnumTemplateData<TypeScriptEnumTemplateDataOptions> data);
    
    /// <summary>
    /// Строит Interface
    /// </summary>
    InterfaceDeclarationNode BuildInterface(ModelTemplateData<TypeScriptModelTemplateDataOptions, TypeScriptPropertyTemplateDataOptions> data);
    
    /// <summary>
    /// Строит TypeAlias
    /// </summary>
    TypeAliasDeclarationNode BuildTypeAlias(string name, TypeNode type);
    
    /// <summary>
    /// Строит Function
    /// </summary>
    FunctionDeclarationNode BuildFunction(OperationTemplateData<TypeScriptOperationTemplateDataOptions> data);
    
    /// <summary>
    /// Строит Property
    /// </summary>
    PropertyDeclarationNode BuildProperty(PropertyTemplateData<TypeScriptPropertyTemplateDataOptions> data);
    
    /// <summary>
    /// Строит Type из TypeInfo
    /// </summary>
    TypeNode BuildType(TypeInfo typeInfo);
    
    /// <summary>
    /// Строит Literal Expression
    /// </summary>
    Expressions.LiteralExpressionNode BuildLiteral(object value, TypeInfo typeInfo);
    
    /// <summary>
    /// Строит HttpMethodNode из OperationTemplateData
    /// </summary>
    HttpMethodNode BuildHttpMethod(OperationTemplateData<TypeScriptOperationTemplateDataOptions> data);
    
    /// <summary>
    /// Строит RouteNode из OperationTemplateData
    /// </summary>
    RouteNode BuildRoute(OperationTemplateData<TypeScriptOperationTemplateDataOptions> data);
    
    /// <summary>
    /// Строит RequestBodyNode из OperationTemplateData
    /// </summary>
    RequestBodyNode? BuildRequestBody(OperationTemplateData<TypeScriptOperationTemplateDataOptions> data);
    
    /// <summary>
    /// Строит список QueryParameterNode из OperationTemplateData
    /// </summary>
    IReadOnlyList<QueryParameterNode> BuildQueryParameters(OperationTemplateData<TypeScriptOperationTemplateDataOptions> data);
    
    /// <summary>
    /// Строит список HeaderParameterNode из OperationTemplateData
    /// </summary>
    IReadOnlyList<HeaderParameterNode> BuildHeaderParameters(OperationTemplateData<TypeScriptOperationTemplateDataOptions> data);
    
    // IR → AST методы
    
    /// <summary>
    /// Строит TypeNode из IrType
    /// </summary>
    TypeNode BuildType(IrType irType);
    
    /// <summary>
    /// Строит SourceFile из IrModel
    /// </summary>
    SourceFileNode BuildSourceFile(IrModel model, string? importPath = null);
    
    /// <summary>
    /// Строит InterfaceDeclarationNode из IrModel
    /// </summary>
    InterfaceDeclarationNode BuildInterface(IrModel model);
    
    /// <summary>
    /// Строит PropertyDeclarationNode из IrProperty
    /// </summary>
    PropertyDeclarationNode BuildProperty(IrProperty property);
    
    /// <summary>
    /// Строит SourceFile из IrEnum
    /// </summary>
    SourceFileNode BuildSourceFile(IrEnum enumModel, string? importPath = null);
    
    /// <summary>
    /// Строит FunctionDeclarationNode из IrOperation
    /// </summary>
    FunctionDeclarationNode BuildFunction(IrOperation operation);
    
    /// <summary>
    /// Строит SourceFile из IrClient
    /// </summary>
    SourceFileNode BuildSourceFile(IrClient client, string? importPath = null);
}
