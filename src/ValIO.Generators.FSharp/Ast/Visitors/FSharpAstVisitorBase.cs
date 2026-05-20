using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Http;
using ValIO.Core.Ast.Nodes.Statements;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.FSharp.Ast.Nodes;

namespace ValIO.Generators.FSharp.Ast.Visitors;

/// <summary>
/// Base for F#-specific visitors: dispatches via <see cref="AstNode.Accept{TResult}"/>
/// and leaves the concrete visit methods abstract.
/// </summary>
public abstract class FSharpAstVisitorBase<TResult> : BaseAstVisitor<TResult>, IFSharpAstVisitor<TResult>
{
    public override TResult Visit(AstNode node) => node.Accept(this);

    public override TResult VisitBlankLineStatement(BlankLineStatementNode node) => VisitDefault(node);

    // F#-specific nodes.
    public abstract TResult VisitFSharpFile(FSharpFileNode node);
    public abstract TResult VisitFSharpRecord(FSharpRecordNode node);
    public abstract TResult VisitFSharpDU(FSharpDuNode node);
    public abstract TResult VisitFSharpFunction(FSharpFunctionNode node);

    // IAstVisitor — shared.
    public abstract TResult VisitEnumDeclaration(EnumDeclarationNode node);
    public abstract TResult VisitInterfaceDeclaration(InterfaceDeclarationNode node);

    // HTTP nodes — F# plugin does not consume them directly; default to VisitDefault.
    public virtual TResult VisitHttpMethod(HttpMethodNode node) => VisitDefault(node);
    public virtual TResult VisitRoute(RouteNode node) => VisitDefault(node);
    public virtual TResult VisitPathParameter(PathParameterNode node) => VisitDefault(node);
    public virtual TResult VisitQueryParameter(QueryParameterNode node) => VisitDefault(node);
    public virtual TResult VisitHeaderParameter(HeaderParameterNode node) => VisitDefault(node);
    public virtual TResult VisitRequestBody(RequestBodyNode node) => VisitDefault(node);
    public virtual TResult VisitResponseType(ResponseTypeNode node) => VisitDefault(node);
}
