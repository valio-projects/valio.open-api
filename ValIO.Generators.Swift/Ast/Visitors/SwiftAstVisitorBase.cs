using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Http;
using ValIO.Core.Ast.Nodes.Statements;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Swift.Ast.Nodes;

namespace ValIO.Generators.Swift.Ast.Visitors;

/// <summary>
/// Base for Swift-specific visitors: dispatches via <see cref="AstNode.Accept{TResult}"/>
/// and leaves the concrete visit methods abstract.
/// </summary>
public abstract class SwiftAstVisitorBase<TResult> : BaseAstVisitor<TResult>, ISwiftAstVisitor<TResult>
{
    public override TResult Visit(AstNode node) => node.Accept(this);

    public override TResult VisitBlankLineStatement(BlankLineStatementNode node) => VisitDefault(node);

    // Swift-specific nodes.
    public abstract TResult VisitSwiftFile(SwiftFileNode node);
    public abstract TResult VisitSwiftImport(SwiftImportNode node);
    public abstract TResult VisitSwiftAttribute(SwiftAttributeNode node);
    public abstract TResult VisitSwiftStruct(SwiftStructDeclarationNode node);
    public abstract TResult VisitSwiftClassDeclaration(SwiftClassDeclarationNode node);
    public abstract TResult VisitSwiftProperty(SwiftPropertyNode node);
    public abstract TResult VisitSwiftEnumDeclaration(SwiftEnumDeclarationNode node);
    public abstract TResult VisitSwiftEnumMember(SwiftEnumMemberNode node);
    public abstract TResult VisitSwiftFunctionDeclaration(SwiftFunctionDeclarationNode node);
    public abstract TResult VisitSwiftParameter(SwiftParameterNode node);

    // IAstVisitor — shared.
    public abstract TResult VisitEnumDeclaration(EnumDeclarationNode node);
    public abstract TResult VisitInterfaceDeclaration(InterfaceDeclarationNode node);

    // HTTP nodes — Swift plugin does not consume them directly; default to VisitDefault.
    public virtual TResult VisitHttpMethod(HttpMethodNode node) => VisitDefault(node);
    public virtual TResult VisitRoute(RouteNode node) => VisitDefault(node);
    public virtual TResult VisitPathParameter(PathParameterNode node) => VisitDefault(node);
    public virtual TResult VisitQueryParameter(QueryParameterNode node) => VisitDefault(node);
    public virtual TResult VisitHeaderParameter(HeaderParameterNode node) => VisitDefault(node);
    public virtual TResult VisitRequestBody(RequestBodyNode node) => VisitDefault(node);
    public virtual TResult VisitResponseType(ResponseTypeNode node) => VisitDefault(node);
}
