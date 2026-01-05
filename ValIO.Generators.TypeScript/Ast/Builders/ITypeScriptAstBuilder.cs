using ValIO.Core.Ast.Nodes;
using ValIO.Core.Interfaces;
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
}
