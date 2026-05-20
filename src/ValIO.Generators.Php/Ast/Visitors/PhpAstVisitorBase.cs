using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.Ast.Nodes.Http;
using ValIO.Core.Ast.Nodes.Statements;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Php.Ast.Nodes;

namespace ValIO.Generators.Php.Ast.Visitors;

/// <summary>
/// Базовая реализация PHP visitor. Делегирует неподдерживаемые узлы в <see cref="BaseAstVisitor{TResult}.VisitDefault"/>.
/// </summary>
public abstract class PhpAstVisitorBase<TResult> : BaseAstVisitor<TResult>, IPhpAstVisitor<TResult>
{
    public override TResult Visit(AstNode node) => node.Accept(this);

    // Базовые узлы IAstVisitor / IBaseAstVisitor — переопределяем через VisitDefault.
    public override TResult VisitBlankLineStatement(BlankLineStatementNode node) => VisitDefault(node);
    public override TResult VisitRawType(RawTypeNode node) => VisitDefault(node);
    public override TResult VisitUnaryExpression(UnaryExpressionNode node) => VisitDefault(node);

    public virtual TResult VisitEnumDeclaration(EnumDeclarationNode node) => VisitDefault(node);
    public virtual TResult VisitInterfaceDeclaration(InterfaceDeclarationNode node) => VisitDefault(node);

    public virtual TResult VisitHttpMethod(HttpMethodNode node) => VisitDefault(node);
    public virtual TResult VisitRoute(RouteNode node) => VisitDefault(node);
    public virtual TResult VisitPathParameter(PathParameterNode node) => VisitDefault(node);
    public virtual TResult VisitQueryParameter(QueryParameterNode node) => VisitDefault(node);
    public virtual TResult VisitHeaderParameter(HeaderParameterNode node) => VisitDefault(node);
    public virtual TResult VisitRequestBody(RequestBodyNode node) => VisitDefault(node);
    public virtual TResult VisitResponseType(ResponseTypeNode node) => VisitDefault(node);

    // Абстрактные методы только для PHP-специфичных узлов.
    public abstract TResult VisitPhpSourceFile(PhpSourceFileNode node);
    public abstract TResult VisitPhpClassDeclaration(PhpClassDeclarationNode node);
    public abstract TResult VisitPhpConstructor(PhpConstructorDeclarationNode node);
    public abstract TResult VisitPhpUseStatement(PhpUseStatementNode node);
}
