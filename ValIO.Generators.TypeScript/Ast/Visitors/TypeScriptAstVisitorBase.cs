using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.Ast.Visitors;

namespace ValIO.Generators.TypeScript.Ast.Visitors;

/// <summary>
/// Базовый класс для посетителя TypeScript AST узлов
/// </summary>
public abstract class TypeScriptAstVisitorBase<TResult> : BaseAstVisitor<TResult>, ITypeScriptAstVisitor<TResult>
{
    /// <summary>
    /// Переопределяем VisitExpression для обработки TypeScript-специфичных выражений
    /// </summary>
    public override TResult VisitExpression(ExpressionNode node)
    {
        return node.Kind switch
        {
            AstNodeKind.ObjectLiteralExpression => VisitObjectLiteralExpression((ObjectLiteralExpressionNode)node),
            AstNodeKind.TemplateLiteralExpression => VisitTemplateLiteralExpression((TemplateLiteralExpressionNode)node),
            _ => base.VisitExpression(node) // Делегируем базовым выражениям
        };
    }
    
    // Абстрактные методы только для TypeScript узлов
    public abstract TResult VisitSourceFile(SourceFileNode node);
    public abstract TResult VisitTypeAliasDeclaration(TypeAliasDeclarationNode node);
    public abstract TResult VisitFunctionDeclaration(FunctionDeclarationNode node);
    public abstract TResult VisitVariableDeclaration(VariableDeclarationNode node);
    public abstract TResult VisitObjectLiteralExpression(ObjectLiteralExpressionNode node);
    public abstract TResult VisitPropertyAssignment(PropertyAssignmentNode node);
    public abstract TResult VisitTemplateLiteralExpression(TemplateLiteralExpressionNode node);
    public abstract TResult VisitGenericParameter(GenericParameterNode node);
    public abstract TResult VisitPropertySignature(PropertySignatureNode node);
}
