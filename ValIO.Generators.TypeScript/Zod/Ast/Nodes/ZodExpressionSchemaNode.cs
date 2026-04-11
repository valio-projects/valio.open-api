using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.TypeScript.Zod.Ast.Enums;
using ValIO.Generators.TypeScript.Zod.Ast.Visitors;

namespace ValIO.Generators.TypeScript.Zod.Ast.Nodes;

/// <summary>
/// Узел Zod схемы в виде выражения (цепочка вызовов: z.string().uuid().min(1) и т.д.).
/// Используется вместо сырых строк — AST полностью описывает структуру, renderer только отрисовывает.
/// </summary>
public sealed class ZodExpressionSchemaNode : ZodSchemaNode
{
    /// <summary>
    /// Корневое выражение цепочки вызовов (например: CallExpression с target = z.string().uuid().min(...))
    /// </summary>
    public ExpressionNode Expression { get; }

    public override AstNodeKind Kind => AstNodeKind.ZodSchema;

    public override IReadOnlyList<AstNode> Children => new[] { Expression };

    /// <summary>
    /// Конструктор
    /// </summary>
    /// <param name="expression">Выражение Zod цепочки (z.string().uuid().min(...) и т.д.)</param>
    public ZodExpressionSchemaNode(ExpressionNode expression)
        : base(ZodSchemaKind.Expression, null, null)
    {
        Expression = expression ?? throw new ArgumentNullException(nameof(expression));
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is IZodAstVisitor<TResult> zodVisitor)
        {
            return zodVisitor.VisitZodSchema(this);
        }
        return visitor.VisitExpression(this);
    }

    public override AstNode Clone()
    {
        return new ZodExpressionSchemaNode((ExpressionNode)Expression.Clone());
    }
}
