using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.Ast.Nodes.Statements;
using ValIO.Core.Ast.Nodes.Types;

namespace ValIO.Core.Ast.Visitors;

/// <summary>
/// Базовый абстрактный класс для посетителя AST с default implementations
/// </summary>
public abstract class BaseAstVisitor<TResult> : IBaseAstVisitor<TResult>
{
    /// <summary>
    /// Посещает узел (диспетчеризация по типу)
    /// </summary>
    public virtual TResult Visit(AstNode node)
    {
        return node.Accept(this);
    }
    
    /// <summary>
    /// Посещает объявление
    /// </summary>
    /// <remarks>
    /// EnumDeclaration и InterfaceDeclaration обрабатываются через IAstVisitor.VisitEnumDeclaration и IAstVisitor.VisitInterfaceDeclaration
    /// в языковых visitor'ах, так как BaseAstVisitor не реализует IAstVisitor.
    /// </remarks>
    public virtual TResult VisitDeclaration(DeclarationNode node)
    {
        return node.Kind switch
        {
            AstNodeKind.TypeDeclaration => VisitTypeDeclaration((TypeDeclarationNode)node),
            AstNodeKind.PropertyDeclaration => VisitPropertyDeclaration((PropertyDeclarationNode)node),
            AstNodeKind.MethodDeclaration => VisitMethodDeclaration((MethodDeclarationNode)node),
            AstNodeKind.ParameterDeclaration => VisitParameterDeclaration((ParameterDeclarationNode)node),
            AstNodeKind.EnumMemberDeclaration => VisitEnumMemberDeclaration((EnumMemberDeclarationNode)node),
            // EnumDeclaration и InterfaceDeclaration обрабатываются через IAstVisitor в языковых visitor'ах
            _ => VisitDefault(node)
        };
    }
    
    /// <summary>
    /// Посещает тип
    /// </summary>
    public virtual TResult VisitType(TypeNode node)
    {
        return node.Kind switch
        {
            AstNodeKind.SimpleType => VisitSimpleType((SimpleTypeNode)node),
            AstNodeKind.GenericType => VisitGenericType((GenericTypeNode)node),
            AstNodeKind.NullableType => VisitNullableType((NullableTypeNode)node),
            AstNodeKind.OptionalType => VisitOptionalType((OptionalTypeNode)node),
            AstNodeKind.ArrayType => VisitArrayType((ArrayTypeNode)node),
            AstNodeKind.UnionType => VisitUnionType((UnionTypeNode)node),
            AstNodeKind.IntersectionType => VisitIntersectionType((IntersectionTypeNode)node),
            AstNodeKind.DictionaryType => VisitDictionaryType((DictionaryTypeNode)node),
            _ => VisitDefault(node)
        };
    }
    
    /// <summary>
    /// Посещает выражение
    /// </summary>
    public virtual TResult VisitExpression(ExpressionNode node)
    {
        return node.Kind switch
        {
            AstNodeKind.LiteralExpression => VisitLiteralExpression((LiteralExpressionNode)node),
            AstNodeKind.IdentifierExpression => VisitIdentifierExpression((IdentifierExpressionNode)node),
            AstNodeKind.CallExpression => VisitCallExpression((CallExpressionNode)node),
            AstNodeKind.MemberAccessExpression => VisitMemberAccessExpression((MemberAccessExpressionNode)node),
            AstNodeKind.BinaryExpression => VisitBinaryExpression((BinaryExpressionNode)node),
            AstNodeKind.ConditionalExpression => VisitConditionalExpression((ConditionalExpressionNode)node),
            _ => VisitDefault(node)
        };
    }
    
    /// <summary>
    /// Посещает statement
    /// </summary>
    public virtual TResult VisitStatement(StatementNode node)
    {
        return node.Kind switch
        {
            AstNodeKind.ReturnStatement => VisitReturnStatement((ReturnStatementNode)node),
            AstNodeKind.IfStatement => VisitIfStatement((IfStatementNode)node),
            AstNodeKind.ForEachStatement => VisitForEachStatement((ForEachStatementNode)node),
            AstNodeKind.ExpressionStatement => VisitExpressionStatement((ExpressionStatementNode)node),
            _ => VisitDefault(node)
        };
    }
    
    // Базовые узлы
    public abstract TResult VisitIdentifier(IdentifierNode node);
    public abstract TResult VisitComment(CommentNode node);
    public abstract TResult VisitAttribute(AttributeNode node);
    public abstract TResult VisitBlock(BlockNode node);
    public abstract TResult VisitImport(ImportNode node);
    public abstract TResult VisitExport(ExportNode node);
    
    // Типы
    public abstract TResult VisitSimpleType(SimpleTypeNode node);
    public abstract TResult VisitGenericType(GenericTypeNode node);
    public abstract TResult VisitNullableType(NullableTypeNode node);
    public abstract TResult VisitOptionalType(OptionalTypeNode node);
    public abstract TResult VisitArrayType(ArrayTypeNode node);
    public abstract TResult VisitUnionType(UnionTypeNode node);
    public abstract TResult VisitIntersectionType(IntersectionTypeNode node);
    public abstract TResult VisitDictionaryType(DictionaryTypeNode node);
    
    // Выражения
    public abstract TResult VisitLiteralExpression(LiteralExpressionNode node);
    public abstract TResult VisitIdentifierExpression(IdentifierExpressionNode node);
    public abstract TResult VisitCallExpression(CallExpressionNode node);
    public abstract TResult VisitMemberAccessExpression(MemberAccessExpressionNode node);
    public abstract TResult VisitBinaryExpression(BinaryExpressionNode node);
    public abstract TResult VisitConditionalExpression(ConditionalExpressionNode node);
    
    // Statements
    public abstract TResult VisitReturnStatement(ReturnStatementNode node);
    public abstract TResult VisitIfStatement(IfStatementNode node);
    public abstract TResult VisitForEachStatement(ForEachStatementNode node);
    public abstract TResult VisitExpressionStatement(ExpressionStatementNode node);
    
    // Объявления
    public abstract TResult VisitTypeDeclaration(TypeDeclarationNode node);
    public abstract TResult VisitPropertyDeclaration(PropertyDeclarationNode node);
    public abstract TResult VisitMethodDeclaration(MethodDeclarationNode node);
    public abstract TResult VisitParameterDeclaration(ParameterDeclarationNode node);
    public abstract TResult VisitEnumMemberDeclaration(EnumMemberDeclarationNode node);
    
    /// <summary>
    /// Метод по умолчанию для неизвестных узлов
    /// </summary>
    public virtual TResult VisitDefault(AstNode node)
    {
        throw new NotSupportedException($"Visit for node kind {node.Kind} is not supported");
    }
}
