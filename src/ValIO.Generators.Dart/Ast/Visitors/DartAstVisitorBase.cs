using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.Ast.Nodes.Http;
using ValIO.Core.Ast.Nodes.Statements;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Dart.Ast.Nodes;

namespace ValIO.Generators.Dart.Ast.Visitors;

/// <summary>
/// Base for Dart-specific visitors: dispatches via <see cref="AstNode.Accept{TResult}"/>.
/// Provides default <see cref="BaseAstVisitor{TResult}.VisitDefault"/> implementations for
/// AST nodes the Dart plugin does not consume directly.
/// </summary>
public abstract class DartAstVisitorBase<TResult> : BaseAstVisitor<TResult>, IDartAstVisitor<TResult>
{
    public override TResult Visit(AstNode node) => node.Accept(this);

    // Dart-specific (abstract — concrete renderer provides).
    public abstract TResult VisitDartFile(DartFileNode node);
    public abstract TResult VisitDartImport(DartImportNode node);
    public abstract TResult VisitDartClass(DartClassDeclarationNode node);
    public abstract TResult VisitDartEnum(DartEnumDeclarationNode node);
    public abstract TResult VisitDartEnumMember(DartEnumMemberNode node);
    public abstract TResult VisitDartField(DartFieldNode node);
    public abstract TResult VisitDartMethod(DartMethodNode node);
    public abstract TResult VisitDartParameter(DartParameterNode node);

    // Expressions — Dart plugin doesn't consume them.
    public override TResult VisitLiteralExpression(LiteralExpressionNode node) => VisitDefault(node);
    public override TResult VisitIdentifierExpression(IdentifierExpressionNode node) => VisitDefault(node);
    public override TResult VisitCallExpression(CallExpressionNode node) => VisitDefault(node);
    public override TResult VisitMemberAccessExpression(MemberAccessExpressionNode node) => VisitDefault(node);
    public override TResult VisitUnaryExpression(UnaryExpressionNode node) => VisitDefault(node);
    public override TResult VisitBinaryExpression(BinaryExpressionNode node) => VisitDefault(node);
    public override TResult VisitConditionalExpression(ConditionalExpressionNode node) => VisitDefault(node);
    public override TResult VisitAwaitExpression(AwaitExpressionNode node) => VisitDefault(node);
    public override TResult VisitCastExpression(CastExpressionNode node) => VisitDefault(node);

    // Statements.
    public override TResult VisitBlankLineStatement(BlankLineStatementNode node) => VisitDefault(node);
    public override TResult VisitReturnStatement(ReturnStatementNode node) => VisitDefault(node);
    public override TResult VisitIfStatement(IfStatementNode node) => VisitDefault(node);
    public override TResult VisitForEachStatement(ForEachStatementNode node) => VisitDefault(node);
    public override TResult VisitExpressionStatement(ExpressionStatementNode node) => VisitDefault(node);
    public override TResult VisitVariableDeclarationStatement(VariableDeclarationStatementNode node) => VisitDefault(node);
    public override TResult VisitThrowStatement(ThrowStatementNode node) => VisitDefault(node);
    public override TResult VisitUsingStatement(UsingStatementNode node) => VisitDefault(node);
    public override TResult VisitTryCatchStatement(TryCatchStatementNode node) => VisitDefault(node);
    public override TResult VisitCatchClause(CatchClauseNode node) => VisitDefault(node);
    public override TResult VisitBlock(BlockNode node) => VisitDefault(node);

    // Misc Core declarations.
    public override TResult VisitMethodDeclaration(MethodDeclarationNode node) => VisitDefault(node);
    public override TResult VisitPropertyDeclaration(PropertyDeclarationNode node) => VisitDefault(node);
    public override TResult VisitParameterDeclaration(ParameterDeclarationNode node) => VisitDefault(node);

    // Concrete renderer provides shared declarations.
    public abstract TResult VisitEnumDeclaration(EnumDeclarationNode node);
    public abstract TResult VisitInterfaceDeclaration(InterfaceDeclarationNode node);

    // HTTP nodes — Dart plugin doesn't consume directly.
    public virtual TResult VisitHttpMethod(HttpMethodNode node) => VisitDefault(node);
    public virtual TResult VisitRoute(RouteNode node) => VisitDefault(node);
    public virtual TResult VisitPathParameter(PathParameterNode node) => VisitDefault(node);
    public virtual TResult VisitQueryParameter(QueryParameterNode node) => VisitDefault(node);
    public virtual TResult VisitHeaderParameter(HeaderParameterNode node) => VisitDefault(node);
    public virtual TResult VisitRequestBody(RequestBodyNode node) => VisitDefault(node);
    public virtual TResult VisitResponseType(ResponseTypeNode node) => VisitDefault(node);
}
