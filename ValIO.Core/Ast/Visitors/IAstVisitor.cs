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
public interface IAstVisitor<TResult> : IBaseAstVisitor<TResult>
{
    /// <summary>
    /// Посещает узел (диспетчеризация по типу)
    /// </summary>
    TResult Visit(AstNode node);
    
    /// <summary>
    /// Посещает объявление
    /// </summary>
    TResult VisitDeclaration(DeclarationNode node);
    
    /// <summary>
    /// Посещает тип
    /// </summary>
    TResult VisitType(TypeNode node);
    
    /// <summary>
    /// Посещает выражение
    /// </summary>
    TResult VisitExpression(ExpressionNode node);
    
    /// <summary>
    /// Посещает statement
    /// </summary>
    TResult VisitStatement(StatementNode node);
    
    // Базовые узлы
    TResult VisitIdentifier(IdentifierNode node);
    TResult VisitComment(CommentNode node);
    TResult VisitAttribute(AttributeNode node);
    TResult VisitBlock(BlockNode node);
    TResult VisitImport(ImportNode node);
    TResult VisitExport(ExportNode node);
    
    // Типы
    TResult VisitSimpleType(SimpleTypeNode node);
    TResult VisitGenericType(GenericTypeNode node);
    TResult VisitNullableType(NullableTypeNode node);
    TResult VisitOptionalType(OptionalTypeNode node);
    TResult VisitArrayType(ArrayTypeNode node);
    TResult VisitUnionType(UnionTypeNode node);
    TResult VisitIntersectionType(IntersectionTypeNode node);
    TResult VisitDictionaryType(DictionaryTypeNode node);
    
    // Выражения
    TResult VisitLiteralExpression(LiteralExpressionNode node);
    TResult VisitIdentifierExpression(IdentifierExpressionNode node);
    TResult VisitCallExpression(CallExpressionNode node);
    TResult VisitMemberAccessExpression(MemberAccessExpressionNode node);
    TResult VisitBinaryExpression(BinaryExpressionNode node);
    TResult VisitConditionalExpression(ConditionalExpressionNode node);
    
    // Statements
    TResult VisitReturnStatement(ReturnStatementNode node);
    TResult VisitIfStatement(IfStatementNode node);
    TResult VisitForEachStatement(ForEachStatementNode node);
    TResult VisitExpressionStatement(ExpressionStatementNode node);
    
    // Объявления
    TResult VisitTypeDeclaration(TypeDeclarationNode node);
    TResult VisitPropertyDeclaration(PropertyDeclarationNode node);
    TResult VisitMethodDeclaration(MethodDeclarationNode node);
    TResult VisitParameterDeclaration(ParameterDeclarationNode node);
    TResult VisitEnumMemberDeclaration(EnumMemberDeclarationNode node);
    
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
