using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Http;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.CSharp.Ast.Nodes;

namespace ValIO.Generators.CSharp.Ast.Visitors;

/// <summary>
/// Базовый класс для посетителя C# AST узлов
/// </summary>
public abstract class CSharpAstVisitorBase<TResult> : BaseAstVisitor<TResult>, ICSharpAstVisitor<TResult>
{
    /// <summary>
    /// Переопределяем Visit для поддержки IAstVisitor
    /// </summary>
    public override TResult Visit(AstNode node)
    {
        return node.Accept(this);
    }
    
    // Абстрактные методы только для C# узлов
    public abstract TResult VisitCompilationUnit(CompilationUnitNode node);
    public abstract TResult VisitNamespaceDeclaration(NamespaceDeclarationNode node);
    public abstract TResult VisitUsingDirective(UsingDirectiveNode node);
    public abstract TResult VisitClassDeclaration(ClassDeclarationNode node);
    public abstract TResult VisitRecordDeclaration(RecordDeclarationNode node);
    public abstract TResult VisitStructDeclaration(StructDeclarationNode node);
    public abstract TResult VisitFieldDeclaration(FieldDeclarationNode node);
    public abstract TResult VisitConstructorDeclaration(ConstructorDeclarationNode node);
    
    // Enum и Interface (общие для C# и TypeScript, определены в IAstVisitor)
    // Реализуем их здесь, так как они используются в C#
    public abstract TResult VisitEnumDeclaration(EnumDeclarationNode node);
    public abstract TResult VisitInterfaceDeclaration(InterfaceDeclarationNode node);
    
    // HTTP специфичные узлы (из IAstVisitor)
    public abstract TResult VisitHttpMethod(HttpMethodNode node);
    public abstract TResult VisitRoute(RouteNode node);
    public abstract TResult VisitPathParameter(PathParameterNode node);
    public abstract TResult VisitQueryParameter(QueryParameterNode node);
    public abstract TResult VisitHeaderParameter(HeaderParameterNode node);
    public abstract TResult VisitRequestBody(RequestBodyNode node);
    public abstract TResult VisitResponseType(ResponseTypeNode node);
}
