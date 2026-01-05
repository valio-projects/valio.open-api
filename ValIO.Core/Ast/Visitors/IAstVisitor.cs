using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.Ast.Nodes.Http;
using ValIO.Core.Ast.Nodes.Statements;
using ValIO.Core.Ast.Nodes.Types;

namespace ValIO.Core.Ast.Visitors;

/// <summary>
/// Интерфейс посетителя AST (Visitor Pattern)
/// Объединяет все интерфейсы визиторов для поддержки всех типов узлов
/// Используется узлами AST в методе Accept для диспетчеризации
/// </summary>
/// <remarks>
/// Наследует все базовые методы из IBaseAstVisitor.
/// Добавляет только методы для диспетчеризации и специфичные узлы.
/// </remarks>
public interface IAstVisitor<TResult> : IBaseAstVisitor<TResult>
{
    /// <summary>
    /// Посещает объявление (диспетчеризация по типу)
    /// </summary>
    TResult VisitDeclaration(DeclarationNode node);
    
    /// <summary>
    /// Посещает тип (диспетчеризация по типу)
    /// </summary>
    TResult VisitType(TypeNode node);
    
    /// <summary>
    /// Посещает выражение (диспетчеризация по типу)
    /// </summary>
    TResult VisitExpression(ExpressionNode node);
    
    /// <summary>
    /// Посещает statement (диспетчеризация по типу)
    /// </summary>
    TResult VisitStatement(StatementNode node);
    
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
    
    // TypeScript специфичные узлы
    TResult VisitSourceFile(SourceFileNode node);
    TResult VisitTypeAliasDeclaration(TypeAliasDeclarationNode node);
    TResult VisitFunctionDeclaration(FunctionDeclarationNode node);
    TResult VisitVariableDeclaration(VariableDeclarationNode node);
    TResult VisitObjectLiteralExpression(ObjectLiteralExpressionNode node);
    TResult VisitPropertyAssignment(PropertyAssignmentNode node);
    TResult VisitTemplateLiteralExpression(TemplateLiteralExpressionNode node);
    TResult VisitGenericParameter(GenericParameterNode node);
    TResult VisitPropertySignature(PropertySignatureNode node);
    
    // HTTP специфичные узлы
    TResult VisitHttpMethod(HttpMethodNode node);
    TResult VisitRoute(RouteNode node);
    TResult VisitPathParameter(PathParameterNode node);
    TResult VisitQueryParameter(QueryParameterNode node);
    TResult VisitHeaderParameter(HeaderParameterNode node);
    TResult VisitRequestBody(RequestBodyNode node);
    TResult VisitResponseType(ResponseTypeNode node);
}
