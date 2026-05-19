using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.Ast.Nodes.Http;
using ValIO.Core.Ast.Nodes.Statements;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.TypeScript.Zod.Ast.Nodes;

namespace ValIO.Generators.TypeScript.Zod.Ast.Visitors;

/// <summary>
/// Базовый класс для посетителя Zod AST узлов
/// </summary>
public abstract class ZodAstVisitorBase<TResult> : BaseAstVisitor<TResult>, IZodAstVisitor<TResult>
{
    /// <summary>
    /// Посещает узел (диспетчеризация по типу)
    /// </summary>
    public override TResult Visit(AstNode node)
    {
        return node.Accept(this);
    }

    public override TResult VisitBlankLineStatement(BlankLineStatementNode node) => VisitDefault(node);

    // Реализация методов IAstVisitor - делегируем в VisitDefault для неизвестных узлов
    public override TResult VisitUnaryExpression(UnaryExpressionNode node) => VisitDefault(node);

    public virtual TResult VisitEnumDeclaration(EnumDeclarationNode node) => VisitDefault(node);
    public virtual TResult VisitInterfaceDeclaration(InterfaceDeclarationNode node) => VisitDefault(node);
    public virtual TResult VisitHttpMethod(HttpMethodNode node) => VisitDefault(node);
    public virtual TResult VisitRoute(RouteNode node) => VisitDefault(node);
    public virtual TResult VisitPathParameter(PathParameterNode node) => VisitDefault(node);
    public virtual TResult VisitQueryParameter(QueryParameterNode node) => VisitDefault(node);
    public virtual TResult VisitHeaderParameter(HeaderParameterNode node) => VisitDefault(node);
    public virtual TResult VisitRequestBody(RequestBodyNode node) => VisitDefault(node);
    public virtual TResult VisitResponseType(ResponseTypeNode node) => VisitDefault(node);
    
    // Абстрактные методы только для Zod узлов
    public abstract TResult VisitZodSchema(ZodSchemaNode node);
    public abstract TResult VisitZodObject(ZodObjectNode node);
    public abstract TResult VisitZodArray(ZodArrayNode node);
    public abstract TResult VisitZodUnion(ZodUnionNode node);
    public abstract TResult VisitZodIntersection(ZodIntersectionNode node);
    public abstract TResult VisitZodEnum(ZodEnumNode node);
    public abstract TResult VisitZodEnumValue(ZodEnumValueNode node);
    public abstract TResult VisitZodProperty(ZodPropertyNode node);
    public abstract TResult VisitZodEffects(ZodEffectsNode node);
    public abstract TResult VisitZodPrimitiveSchema(ZodPrimitiveSchemaNode node);
    public abstract TResult VisitZodExpressionSchema(ZodExpressionSchemaNode node);
}
