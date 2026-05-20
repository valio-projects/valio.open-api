using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Http;
using ValIO.Core.Ast.Nodes.Statements;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Rust.Ast.Nodes;

namespace ValIO.Generators.Rust.Ast.Visitors;

/// <summary>
/// Base for Rust-specific visitors: dispatches via <see cref="AstNode.Accept{TResult}"/>
/// and leaves the concrete visit methods abstract.
/// </summary>
public abstract class RustAstVisitorBase<TResult> : BaseAstVisitor<TResult>, IRustAstVisitor<TResult>
{
    public override TResult Visit(AstNode node) => node.Accept(this);

    public override TResult VisitBlankLineStatement(BlankLineStatementNode node) => VisitDefault(node);

    // Rust-specific nodes.
    public abstract TResult VisitRustFile(RustFileNode node);
    public abstract TResult VisitRustStruct(RustStructNode node);
    public abstract TResult VisitRustEnum(RustEnumNode node);
    public abstract TResult VisitRustFunction(RustFunctionNode node);
    public abstract TResult VisitRustField(RustFieldNode node);
    public abstract TResult VisitRustEnumVariant(RustEnumVariantNode node);

    // IAstVisitor — shared.
    public abstract TResult VisitEnumDeclaration(EnumDeclarationNode node);
    public abstract TResult VisitInterfaceDeclaration(InterfaceDeclarationNode node);

    // HTTP nodes — Rust plugin does not consume them directly; default to VisitDefault.
    public virtual TResult VisitHttpMethod(HttpMethodNode node) => VisitDefault(node);
    public virtual TResult VisitRoute(RouteNode node) => VisitDefault(node);
    public virtual TResult VisitPathParameter(PathParameterNode node) => VisitDefault(node);
    public virtual TResult VisitQueryParameter(QueryParameterNode node) => VisitDefault(node);
    public virtual TResult VisitHeaderParameter(HeaderParameterNode node) => VisitDefault(node);
    public virtual TResult VisitRequestBody(RequestBodyNode node) => VisitDefault(node);
    public virtual TResult VisitResponseType(ResponseTypeNode node) => VisitDefault(node);
}
