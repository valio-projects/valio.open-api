using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.Ast.Nodes.Http;
using ValIO.Core.Ast.Nodes.Statements;
using ValIO.Core.Ast.Nodes.Types;

namespace ValIO.Core.Ast.Visitors;

/// <summary>
/// Базовый абстрактный класс Visitor для AST (базовая диспетчеризация)
/// Наследуется от BaseAstVisitor и реализует IAstVisitor для обратной совместимости
/// </summary>
public abstract class AstVisitorBase<TResult> : BaseAstVisitor<TResult>, IAstVisitor<TResult>
{
    /// <summary>
    /// Посещает объявление (расширенная версия с поддержкой всех типов объявлений)
    /// </summary>
    public override TResult VisitDeclaration(DeclarationNode node)
    {
        return node.Kind switch
        {
            AstNodeKind.TypeDeclaration => VisitTypeDeclaration((TypeDeclarationNode)node),
            AstNodeKind.PropertyDeclaration => VisitPropertyDeclaration((PropertyDeclarationNode)node),
            AstNodeKind.MethodDeclaration => VisitMethodDeclaration((MethodDeclarationNode)node),
            AstNodeKind.ParameterDeclaration => VisitParameterDeclaration((ParameterDeclarationNode)node),
            AstNodeKind.EnumMemberDeclaration => VisitEnumMemberDeclaration((EnumMemberDeclarationNode)node),
            AstNodeKind.NamespaceDeclaration => VisitNamespaceDeclaration((NamespaceDeclarationNode)node),
            AstNodeKind.ClassDeclaration => VisitClassDeclaration((ClassDeclarationNode)node),
            AstNodeKind.RecordDeclaration => VisitRecordDeclaration((RecordDeclarationNode)node),
            AstNodeKind.StructDeclaration => VisitStructDeclaration((StructDeclarationNode)node),
            AstNodeKind.InterfaceDeclaration => VisitInterfaceDeclaration((InterfaceDeclarationNode)node),
            AstNodeKind.EnumDeclaration => VisitEnumDeclaration((EnumDeclarationNode)node),
            AstNodeKind.FieldDeclaration => VisitFieldDeclaration((FieldDeclarationNode)node),
            AstNodeKind.ConstructorDeclaration => VisitConstructorDeclaration((ConstructorDeclarationNode)node),
            AstNodeKind.TypeAliasDeclaration => VisitTypeAliasDeclaration((TypeAliasDeclarationNode)node),
            AstNodeKind.FunctionDeclaration => VisitFunctionDeclaration((FunctionDeclarationNode)node),
            AstNodeKind.VariableDeclaration => VisitVariableDeclaration((VariableDeclarationNode)node),
            AstNodeKind.PropertySignature => VisitPropertySignature((PropertySignatureNode)node),
            _ => VisitDefault(node)
        };
    }
    
    /// <summary>
    /// Посещает выражение (расширенная версия с поддержкой всех типов выражений)
    /// </summary>
    public override TResult VisitExpression(ExpressionNode node)
    {
        return node.Kind switch
        {
            AstNodeKind.LiteralExpression => VisitLiteralExpression((LiteralExpressionNode)node),
            AstNodeKind.IdentifierExpression => VisitIdentifierExpression((IdentifierExpressionNode)node),
            AstNodeKind.CallExpression => VisitCallExpression((CallExpressionNode)node),
            AstNodeKind.MemberAccessExpression => VisitMemberAccessExpression((MemberAccessExpressionNode)node),
            AstNodeKind.BinaryExpression => VisitBinaryExpression((BinaryExpressionNode)node),
            AstNodeKind.ConditionalExpression => VisitConditionalExpression((ConditionalExpressionNode)node),
            AstNodeKind.ObjectLiteralExpression => VisitObjectLiteralExpression((ObjectLiteralExpressionNode)node),
            AstNodeKind.TemplateLiteralExpression => VisitTemplateLiteralExpression((TemplateLiteralExpressionNode)node),
            _ => VisitDefault(node)
        };
    }
    
    // C# специфичные узлы (default implementations выбрасывают NotSupportedException)
    public virtual TResult VisitCompilationUnit(CompilationUnitNode node)
    {
        throw new NotSupportedException("CompilationUnit is not supported by this visitor. Use ICSharpAstVisitor instead.");
    }
    
    public virtual TResult VisitNamespaceDeclaration(NamespaceDeclarationNode node)
    {
        throw new NotSupportedException("NamespaceDeclaration is not supported by this visitor. Use ICSharpAstVisitor instead.");
    }
    
    public virtual TResult VisitUsingDirective(UsingDirectiveNode node)
    {
        throw new NotSupportedException("UsingDirective is not supported by this visitor. Use ICSharpAstVisitor instead.");
    }
    
    public virtual TResult VisitClassDeclaration(ClassDeclarationNode node)
    {
        throw new NotSupportedException("ClassDeclaration is not supported by this visitor. Use ICSharpAstVisitor instead.");
    }
    
    public virtual TResult VisitRecordDeclaration(RecordDeclarationNode node)
    {
        throw new NotSupportedException("RecordDeclaration is not supported by this visitor. Use ICSharpAstVisitor instead.");
    }
    
    public virtual TResult VisitStructDeclaration(StructDeclarationNode node)
    {
        throw new NotSupportedException("StructDeclaration is not supported by this visitor. Use ICSharpAstVisitor instead.");
    }
    
    public virtual TResult VisitFieldDeclaration(FieldDeclarationNode node)
    {
        throw new NotSupportedException("FieldDeclaration is not supported by this visitor. Use ICSharpAstVisitor instead.");
    }
    
    public virtual TResult VisitConstructorDeclaration(ConstructorDeclarationNode node)
    {
        throw new NotSupportedException("ConstructorDeclaration is not supported by this visitor. Use ICSharpAstVisitor instead.");
    }
    
    // TypeScript специфичные узлы (default implementations выбрасывают NotSupportedException)
    public virtual TResult VisitSourceFile(SourceFileNode node)
    {
        throw new NotSupportedException("SourceFile is not supported by this visitor. Use ITypeScriptAstVisitor instead.");
    }
    
    public virtual TResult VisitTypeAliasDeclaration(TypeAliasDeclarationNode node)
    {
        throw new NotSupportedException("TypeAliasDeclaration is not supported by this visitor. Use ITypeScriptAstVisitor instead.");
    }
    
    public virtual TResult VisitFunctionDeclaration(FunctionDeclarationNode node)
    {
        throw new NotSupportedException("FunctionDeclaration is not supported by this visitor. Use ITypeScriptAstVisitor instead.");
    }
    
    public virtual TResult VisitVariableDeclaration(VariableDeclarationNode node)
    {
        throw new NotSupportedException("VariableDeclaration is not supported by this visitor. Use ITypeScriptAstVisitor instead.");
    }
    
    public virtual TResult VisitObjectLiteralExpression(ObjectLiteralExpressionNode node)
    {
        throw new NotSupportedException("ObjectLiteralExpression is not supported by this visitor. Use ITypeScriptAstVisitor instead.");
    }
    
    public virtual TResult VisitPropertyAssignment(PropertyAssignmentNode node)
    {
        throw new NotSupportedException("PropertyAssignment is not supported by this visitor. Use ITypeScriptAstVisitor instead.");
    }
    
    public virtual TResult VisitTemplateLiteralExpression(TemplateLiteralExpressionNode node)
    {
        throw new NotSupportedException("TemplateLiteralExpression is not supported by this visitor. Use ITypeScriptAstVisitor instead.");
    }
    
    public virtual TResult VisitGenericParameter(GenericParameterNode node)
    {
        throw new NotSupportedException("GenericParameter is not supported by this visitor. Use ITypeScriptAstVisitor instead.");
    }
    
    public virtual TResult VisitPropertySignature(PropertySignatureNode node)
    {
        throw new NotSupportedException("PropertySignature is not supported by this visitor. Use ITypeScriptAstVisitor instead.");
    }
    
    // HTTP специфичные узлы (default implementations выбрасывают NotSupportedException)
    public virtual TResult VisitHttpMethod(HttpMethodNode node)
    {
        throw new NotSupportedException("HttpMethod is not supported by this visitor. Use IHttpAstVisitor instead.");
    }
    
    public virtual TResult VisitRoute(RouteNode node)
    {
        throw new NotSupportedException("Route is not supported by this visitor. Use IHttpAstVisitor instead.");
    }
    
    public virtual TResult VisitPathParameter(PathParameterNode node)
    {
        throw new NotSupportedException("PathParameter is not supported by this visitor. Use IHttpAstVisitor instead.");
    }
    
    public virtual TResult VisitQueryParameter(QueryParameterNode node)
    {
        throw new NotSupportedException("QueryParameter is not supported by this visitor. Use IHttpAstVisitor instead.");
    }
    
    public virtual TResult VisitHeaderParameter(HeaderParameterNode node)
    {
        throw new NotSupportedException("HeaderParameter is not supported by this visitor. Use IHttpAstVisitor instead.");
    }
    
    public virtual TResult VisitRequestBody(RequestBodyNode node)
    {
        throw new NotSupportedException("RequestBody is not supported by this visitor. Use IHttpAstVisitor instead.");
    }
    
    public virtual TResult VisitResponseType(ResponseTypeNode node)
    {
        throw new NotSupportedException("ResponseType is not supported by this visitor. Use IHttpAstVisitor instead.");
    }
    
    /// <summary>
    /// Метод по умолчанию для неизвестных узлов
    /// </summary>
    protected virtual TResult VisitDefault(AstNode node)
    {
        throw new NotSupportedException($"Visit for node kind {node.Kind} is not supported");
    }
}
