using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Http;
using ValIO.Core.Ast.Nodes.Statements;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Python.Ast.Nodes;

namespace ValIO.Generators.Python.Ast.Visitors;

/// <summary>
/// Базовый класс для Python-визиторов: реализует диспетчеризацию через Accept
/// и оставляет конкретные методы абстрактными.
/// </summary>
public abstract class PythonAstVisitorBase<TResult> : BaseAstVisitor<TResult>, IPythonAstVisitor<TResult>
{
    public override TResult Visit(AstNode node) => node.Accept(this);

    public override TResult VisitBlankLineStatement(BlankLineStatementNode node) => VisitDefault(node);

    // Python-узлы
    public abstract TResult VisitPythonModule(PythonModuleNode node);
    public abstract TResult VisitPythonImport(PythonImportNode node);
    public abstract TResult VisitPythonClassDeclaration(PythonClassDeclarationNode node);
    public abstract TResult VisitPythonAttributeAssignment(PythonAttributeAssignmentNode node);
    public abstract TResult VisitPythonFunctionDeclaration(PythonFunctionDeclarationNode node);
    public abstract TResult VisitPythonParameter(PythonParameterNode node);
    public abstract TResult VisitPythonEnumDeclaration(PythonEnumDeclarationNode node);
    public abstract TResult VisitPythonEnumMember(PythonEnumMemberNode node);

    // Из IAstVisitor — общие.
    public abstract TResult VisitEnumDeclaration(EnumDeclarationNode node);
    public abstract TResult VisitInterfaceDeclaration(InterfaceDeclarationNode node);

    // HTTP-узлы (не используются Python-плагином напрямую, но интерфейс требует).
    public virtual TResult VisitHttpMethod(HttpMethodNode node) => VisitDefault(node);
    public virtual TResult VisitRoute(RouteNode node) => VisitDefault(node);
    public virtual TResult VisitPathParameter(PathParameterNode node) => VisitDefault(node);
    public virtual TResult VisitQueryParameter(QueryParameterNode node) => VisitDefault(node);
    public virtual TResult VisitHeaderParameter(HeaderParameterNode node) => VisitDefault(node);
    public virtual TResult VisitRequestBody(RequestBodyNode node) => VisitDefault(node);
    public virtual TResult VisitResponseType(ResponseTypeNode node) => VisitDefault(node);
}
