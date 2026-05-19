using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Kotlin.Ast.Nodes;

namespace ValIO.Generators.Kotlin.Ast.Visitors;

/// <summary>
/// Visitor for Kotlin-specific AST nodes.
/// </summary>
public interface IKotlinAstVisitor<TResult> : IAstVisitor<TResult>
{
    TResult VisitKotlinFile(KotlinFileNode node);
    TResult VisitKotlinImport(KotlinImportNode node);
    TResult VisitKotlinAnnotation(KotlinAnnotationNode node);
    TResult VisitKotlinDataClass(KotlinDataClassNode node);
    TResult VisitKotlinClassDeclaration(KotlinClassDeclarationNode node);
    TResult VisitKotlinProperty(KotlinPropertyNode node);
    TResult VisitKotlinEnumDeclaration(KotlinEnumDeclarationNode node);
    TResult VisitKotlinEnumMember(KotlinEnumMemberNode node);
    TResult VisitKotlinFunctionDeclaration(KotlinFunctionDeclarationNode node);
    TResult VisitKotlinParameter(KotlinParameterNode node);
}
