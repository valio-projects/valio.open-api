using ValIO.Core.Ast.Nodes;
using ValIO.Core.Interfaces;
using ValIO.Core.IR;
using ValIO.Core.IR.Types;
using ValIO.Core.Models;
using ValIO.Generators.CSharp.Ast.Nodes;

namespace ValIO.Generators.CSharp.Ast.Builders;

/// <summary>
/// Интерфейс построителя AST для C#
/// </summary>
public interface ICSharpAstBuilder
{
    /// <summary>
    /// Строит Namespace
    /// </summary>
    NamespaceDeclarationNode BuildNamespace(string name, IReadOnlyList<DeclarationNode> declarations);
    
    /// <summary>
    /// Строит Type из TypeInfo
    /// </summary>
    TypeNode BuildType(TypeInfo typeInfo);
    
    /// <summary>
    /// Строит Literal Expression
    /// </summary>
    Expressions.LiteralExpressionNode BuildLiteral(object value, TypeInfo typeInfo);
    
    // IR → AST методы
    
    /// <summary>
    /// Строит TypeNode из IrType
    /// </summary>
    TypeNode BuildType(IrType irType);
    
    /// <summary>
    /// Строит CompilationUnit из IrModel
    /// </summary>
    CompilationUnitNode BuildCompilationUnit(IrModel model, string? @namespace = null);
    
    /// <summary>
    /// Строит ClassDeclarationNode из IrModel
    /// </summary>
    ClassDeclarationNode BuildClass(IrModel model);
    
    /// <summary>
    /// Строит PropertyDeclarationNode из IrProperty
    /// </summary>
    PropertyDeclarationNode BuildProperty(IrProperty property);
    
    /// <summary>
    /// Строит CompilationUnit из IrEnum
    /// </summary>
    CompilationUnitNode BuildCompilationUnit(IrEnum enumModel, string? @namespace = null);
    
    /// <summary>
    /// Строит EnumDeclarationNode из IrEnum
    /// </summary>
    EnumDeclarationNode BuildEnum(IrEnum enumModel);
    
    /// <summary>
    /// Строит MethodDeclarationNode из IrOperation
    /// </summary>
    MethodDeclarationNode BuildMethod(IrOperation operation);
    
    /// <summary>
    /// Строит CompilationUnit из IrClient
    /// </summary>
    CompilationUnitNode BuildCompilationUnit(IrClient client, string? @namespace = null);
    
    /// <summary>
    /// Строит список CompilationUnit из IrDocument (все модели, enum'ы и клиенты)
    /// </summary>
    IReadOnlyList<CompilationUnitNode> BuildCompilationUnits(IrDocument document, string? @namespace = null);
}
