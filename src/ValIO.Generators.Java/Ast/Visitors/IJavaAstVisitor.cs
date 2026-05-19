using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Java.Ast.Nodes;

namespace ValIO.Generators.Java.Ast.Visitors;

/// <summary>
/// Visitor для Java-специфичных AST-узлов. Наследуется от <see cref="IAstVisitor{TResult}"/>.
/// </summary>
public interface IJavaAstVisitor<TResult> : IAstVisitor<TResult>
{
    TResult VisitJavaSourceFile(JavaSourceFileNode node);
    TResult VisitJavaImport(JavaImportNode node);
    TResult VisitJavaRecord(JavaRecordDeclarationNode node);
    TResult VisitJavaClass(JavaClassDeclarationNode node);
}
