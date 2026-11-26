using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Python.Ast.Nodes;

namespace ValIO.Generators.Python.Ast.Visitors;

/// <summary>
/// Интерфейс посетителя Python-специфичных AST узлов.
/// </summary>
public interface IPythonAstVisitor<TResult> : IAstVisitor<TResult>
{
    TResult VisitPythonModule(PythonModuleNode node);
    TResult VisitPythonImport(PythonImportNode node);
    TResult VisitPythonClassDeclaration(PythonClassDeclarationNode node);
    TResult VisitPythonAttributeAssignment(PythonAttributeAssignmentNode node);
    TResult VisitPythonFunctionDeclaration(PythonFunctionDeclarationNode node);

    /// <summary>Visits a PythonDictionaryExpressionNode syntax node.</summary>
    /// <param name="node">The Python syntax to visit.</param>
    /// <returns>The visitor-specific result.</returns>
    TResult VisitPythonDictionaryExpression(PythonDictionaryExpressionNode node) => DefaultVisit(node);

    /// <summary>Visits a PythonKeywordUnpackNode syntax node.</summary>
    /// <param name="node">The Python syntax to visit.</param>
    /// <returns>The visitor-specific result.</returns>
    TResult VisitPythonKeywordUnpack(PythonKeywordUnpackNode node) => DefaultVisit(node);
    TResult VisitPythonParameter(PythonParameterNode node);
    TResult VisitPythonEnumDeclaration(PythonEnumDeclarationNode node);
    TResult VisitPythonEnumMember(PythonEnumMemberNode node);
}
