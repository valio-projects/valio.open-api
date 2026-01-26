using ValIO.Core.Ast.Enums;
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
    
    /// <summary>
    /// Переопределяем VisitExpression для обработки Zod-специфичных выражений
    /// </summary>
    public override TResult VisitExpression(ExpressionNode node)
    {
        return node.Kind switch
        {
            AstNodeKind.ZodSchema => VisitZodSchema((ZodSchemaNode)node),
            AstNodeKind.ZodObject => VisitZodObject((ZodObjectNode)node),
            AstNodeKind.ZodArray => VisitZodArray((ZodArrayNode)node),
            AstNodeKind.ZodUnion => VisitZodUnion((ZodUnionNode)node),
            AstNodeKind.ZodIntersection => VisitZodIntersection((ZodIntersectionNode)node),
            AstNodeKind.ZodEnum => VisitZodEnum((ZodEnumNode)node),
            AstNodeKind.ZodEnumValue => VisitZodEnumValue((ZodEnumValueNode)node),
            AstNodeKind.ZodProperty => VisitZodProperty((ZodPropertyNode)node),
            AstNodeKind.ZodEffects => VisitZodEffects((ZodEffectsNode)node),
            _ => base.VisitExpression(node)
        };
    }
    
    // Реализация методов IAstVisitor - делегируем в VisitDefault для неизвестных узлов
    
    public override TResult VisitEnumDeclaration(EnumDeclarationNode node) => VisitDefault(node);
    public override TResult VisitInterfaceDeclaration(InterfaceDeclarationNode node) => VisitDefault(node);
    public override TResult VisitHttpMethod(HttpMethodNode node) => VisitDefault(node);
    public override TResult VisitRoute(RouteNode node) => VisitDefault(node);
    public override TResult VisitPathParameter(PathParameterNode node) => VisitDefault(node);
    public override TResult VisitQueryParameter(QueryParameterNode node) => VisitDefault(node);
    public override TResult VisitHeaderParameter(HeaderParameterNode node) => VisitDefault(node);
    public override TResult VisitRequestBody(RequestBodyNode node) => VisitDefault(node);
    public override TResult VisitResponseType(ResponseTypeNode node) => VisitDefault(node);
    
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
}
