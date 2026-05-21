using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Http;
using ValIO.Core.Ast.Nodes.Statements;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Ruby.Ast.Nodes;

namespace ValIO.Generators.Ruby.Ast.Visitors;

/// <summary>
/// Base for Ruby-specific visitors: dispatches via <see cref="AstNode.Accept{TResult}"/>
/// and leaves the concrete visit methods abstract.
/// </summary>
public abstract class RubyAstVisitorBase<TResult> : BaseAstVisitor<TResult>, IRubyAstVisitor<TResult>
{
    public override TResult Visit(AstNode node) => node.Accept(this);

    public override TResult VisitBlankLineStatement(BlankLineStatementNode node) => VisitDefault(node);

    // Ruby-specific nodes.
    public abstract TResult VisitRubyFile(RubyFileNode node);
    public abstract TResult VisitRubyClass(RubyClassNode node);
    public abstract TResult VisitRubyMethod(RubyMethodNode node);
    public abstract TResult VisitRubyAttribute(RubyAttributeNode node);

    // IAstVisitor — shared.
    public abstract TResult VisitEnumDeclaration(EnumDeclarationNode node);
    public abstract TResult VisitInterfaceDeclaration(InterfaceDeclarationNode node);

    // HTTP nodes — Ruby plugin does not consume them directly; default to VisitDefault.
    public virtual TResult VisitHttpMethod(HttpMethodNode node) => VisitDefault(node);
    public virtual TResult VisitRoute(RouteNode node) => VisitDefault(node);
    public virtual TResult VisitPathParameter(PathParameterNode node) => VisitDefault(node);
    public virtual TResult VisitQueryParameter(QueryParameterNode node) => VisitDefault(node);
    public virtual TResult VisitHeaderParameter(HeaderParameterNode node) => VisitDefault(node);
    public virtual TResult VisitRequestBody(RequestBodyNode node) => VisitDefault(node);
    public virtual TResult VisitResponseType(ResponseTypeNode node) => VisitDefault(node);
}
