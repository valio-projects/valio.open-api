using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;

namespace ValIO.Generators.CSharp.Ast.Visitors;

/// <summary>
/// Интерфейс посетителя для C# специфичных AST узлов
/// </summary>
public interface ICSharpAstVisitor<TResult> : IBaseAstVisitor<TResult>
{
    // C# специфичные узлы
    TResult VisitCompilationUnit(CompilationUnitNode node);
    TResult VisitNamespaceDeclaration(NamespaceDeclarationNode node);
    TResult VisitUsingDirective(UsingDirectiveNode node);
    TResult VisitClassDeclaration(ClassDeclarationNode node);
    TResult VisitRecordDeclaration(RecordDeclarationNode node);
    TResult VisitStructDeclaration(StructDeclarationNode node);
    TResult VisitInterfaceDeclaration(InterfaceDeclarationNode node);
    TResult VisitEnumDeclaration(EnumDeclarationNode node);
    TResult VisitFieldDeclaration(FieldDeclarationNode node);
    TResult VisitConstructorDeclaration(ConstructorDeclarationNode node);
}
