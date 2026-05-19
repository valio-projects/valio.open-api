using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.Ast.Nodes.Http;
using ValIO.Core.Ast.Nodes.Statements;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Java.Ast.Nodes;

namespace ValIO.Generators.Java.Ast.Visitors;

/// <summary>
/// Базовая реализация Java visitor. Делегирует неподдерживаемые узлы в <see cref="BaseAstVisitor{TResult}.VisitDefault"/>.
/// </summary>
public abstract class JavaAstVisitorBase<TResult> : BaseAstVisitor<TResult>, IJavaAstVisitor<TResult>
{
    public override TResult Visit(AstNode node) => node.Accept(this);

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

    // Абстрактные методы только для Java-специфичных узлов.
    public abstract TResult VisitJavaSourceFile(JavaSourceFileNode node);
    public abstract TResult VisitJavaImport(JavaImportNode node);
    public abstract TResult VisitJavaRecord(JavaRecordDeclarationNode node);
    public abstract TResult VisitJavaClass(JavaClassDeclarationNode node);
}
