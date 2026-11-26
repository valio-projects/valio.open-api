using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.TypeScript.Ast.Nodes;
using ValIO.Generators.TypeScript.Ast.Nodes.Expressions;

namespace ValIO.Generators.TypeScript.Ast.Visitors;

/// <summary>
/// Интерфейс посетителя для TypeScript специфичных AST узлов
/// Наследуется от IAstVisitor для поддержки общих узлов (Enum, Interface)
/// </summary>
public interface ITypeScriptAstVisitor<TResult> : IAstVisitor<TResult>
{
    // TypeScript специфичные узлы
    TResult VisitSourceFile(SourceFileNode node);
    TResult VisitTypeAliasDeclaration(TypeAliasDeclarationNode node);
    TResult VisitFunctionDeclaration(FunctionDeclarationNode node);
    TResult VisitVariableDeclaration(VariableDeclarationNode node);
    TResult VisitObjectLiteralExpression(ObjectLiteralExpressionNode node);
    TResult VisitArrayLiteralExpression(ArrayLiteralExpressionNode node);
    TResult VisitPropertyAssignment(PropertyAssignmentNode node);
    TResult VisitTemplateLiteralExpression(TemplateLiteralExpressionNode node);
    TResult VisitGenericParameter(GenericParameterNode node);
    TResult VisitPropertySignature(PropertySignatureNode node);
    TResult VisitSpreadExpression(SpreadExpressionNode node);
    TResult VisitArrowFunctionExpression(ArrowFunctionExpressionNode node);

    /// <summary>Visits a callback signature used as a TypeScript type.</summary>
    /// <param name="node">The function type to visit.</param>
    /// <returns>The visitor-specific result.</returns>
    TResult VisitFunctionType(FunctionTypeNode node) => DefaultVisit(node);

    /// <summary>Visits a re-export from another module.</summary>
    /// <param name="node">The exported clause.</param>
    /// <returns>The visitor-specific result.</returns>
    TResult VisitModuleReExport(ModuleReExportNode node) => DefaultVisit(node);

    /// <summary>Visits a type selected from a module export.</summary>
    /// <param name="node">The imported type to visit.</param>
    /// <returns>The visitor-specific result.</returns>
    TResult VisitImportType(ImportTypeNode node) => DefaultVisit(node);
}
