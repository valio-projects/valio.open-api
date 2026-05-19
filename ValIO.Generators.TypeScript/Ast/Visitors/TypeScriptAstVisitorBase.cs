using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.Ast.Nodes.Statements;
using ValIO.Core.Ast.Nodes.Http;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.TypeScript.Ast.Nodes;
using ValIO.Generators.TypeScript.Ast.Nodes.Expressions;

namespace ValIO.Generators.TypeScript.Ast.Visitors;

/// <summary>
/// Базовый класс для посетителя TypeScript AST узлов
/// </summary>
public abstract class TypeScriptAstVisitorBase<TResult> : BaseAstVisitor<TResult>, ITypeScriptAstVisitor<TResult>
{
    /// <summary>
    /// Посещает узел (диспетчеризация по типу)
    /// </summary>
    public override TResult Visit(AstNode node)
    {
        return node.Accept(this);
    }
    
    // Реализация методов IAstVisitor - делегируем в VisitDefault для неизвестных узлов
    
    public override TResult VisitUnaryExpression(UnaryExpressionNode node) => VisitDefault(node);

    public override TResult VisitBlankLineStatement(BlankLineStatementNode node) => VisitDefault(node);
    public override TResult VisitRawType(RawTypeNode node) => VisitDefault(node);
    public virtual TResult VisitEnumDeclaration(EnumDeclarationNode node) => VisitDefault(node);
    public virtual TResult VisitInterfaceDeclaration(InterfaceDeclarationNode node) => VisitDefault(node);
    public virtual TResult VisitHttpMethod(HttpMethodNode node) => VisitDefault(node);
    public virtual TResult VisitRoute(RouteNode node) => VisitDefault(node);
    public virtual TResult VisitPathParameter(PathParameterNode node) => VisitDefault(node);
    public virtual TResult VisitQueryParameter(QueryParameterNode node) => VisitDefault(node);
    public virtual TResult VisitHeaderParameter(HeaderParameterNode node) => VisitDefault(node);
    public virtual TResult VisitRequestBody(RequestBodyNode node) => VisitDefault(node);
    public virtual TResult VisitResponseType(ResponseTypeNode node) => VisitDefault(node);
    
    // Абстрактные методы только для TypeScript узлов
    public abstract TResult VisitSourceFile(SourceFileNode node);
    public abstract TResult VisitTypeAliasDeclaration(TypeAliasDeclarationNode node);
    public abstract TResult VisitFunctionDeclaration(FunctionDeclarationNode node);
    public abstract TResult VisitVariableDeclaration(VariableDeclarationNode node);
    public abstract TResult VisitObjectLiteralExpression(ObjectLiteralExpressionNode node);
    public abstract TResult VisitArrayLiteralExpression(ArrayLiteralExpressionNode node);
    public abstract TResult VisitPropertyAssignment(PropertyAssignmentNode node);
    public abstract TResult VisitTemplateLiteralExpression(TemplateLiteralExpressionNode node);
    public abstract TResult VisitGenericParameter(GenericParameterNode node);
    public abstract TResult VisitPropertySignature(PropertySignatureNode node);
    public abstract TResult VisitSpreadExpression(SpreadExpressionNode node);
    public abstract TResult VisitArrowFunctionExpression(ArrowFunctionExpressionNode node);
}
