using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Dart.Ast.Nodes;

namespace ValIO.Generators.Dart.Ast.Visitors;

/// <summary>
/// Visitor for Dart-specific AST nodes.
/// </summary>
public interface IDartAstVisitor<TResult> : IAstVisitor<TResult>
{
    TResult VisitDartFile(DartFileNode node);
    TResult VisitDartImport(DartImportNode node);
    TResult VisitDartClass(DartClassDeclarationNode node);
    TResult VisitDartEnum(DartEnumDeclarationNode node);
    TResult VisitDartEnumMember(DartEnumMemberNode node);
    TResult VisitDartField(DartFieldNode node);
    TResult VisitDartMethod(DartMethodNode node);
    TResult VisitDartParameter(DartParameterNode node);
}
