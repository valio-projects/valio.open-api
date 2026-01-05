using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.CSharp.Ast.Nodes;

namespace ValIO.Generators.CSharp.Ast.Visitors;

/// <summary>
/// Базовый класс для посетителя C# AST узлов
/// </summary>
public abstract class CSharpAstVisitorBase<TResult> : BaseAstVisitor<TResult>, ICSharpAstVisitor<TResult>
{
    // Абстрактные методы только для C# узлов
    public abstract TResult VisitCompilationUnit(CompilationUnitNode node);
    public abstract TResult VisitNamespaceDeclaration(NamespaceDeclarationNode node);
    public abstract TResult VisitUsingDirective(UsingDirectiveNode node);
    public abstract TResult VisitClassDeclaration(ClassDeclarationNode node);
    public abstract TResult VisitRecordDeclaration(RecordDeclarationNode node);
    public abstract TResult VisitStructDeclaration(StructDeclarationNode node);
    public abstract TResult VisitInterfaceDeclaration(InterfaceDeclarationNode node);
    public abstract TResult VisitEnumDeclaration(EnumDeclarationNode node);
    public abstract TResult VisitFieldDeclaration(FieldDeclarationNode node);
    public abstract TResult VisitConstructorDeclaration(ConstructorDeclarationNode node);
}
