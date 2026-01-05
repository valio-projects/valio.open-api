using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.TypeScript.Zod.Ast.Visitors;

namespace ValIO.Generators.TypeScript.Zod.Ast.Nodes;

/// <summary>
/// Узел значения Zod enum
/// </summary>
public sealed class ZodEnumValueNode : AstNode
{
    /// <summary>
    /// Имя значения
    /// </summary>
    public IdentifierNode Name { get; }
    
    /// <summary>
    /// Значение enum
    /// </summary>
    public LiteralExpressionNode Value { get; }
    
    public override AstNodeKind Kind => AstNodeKind.ZodEnumValue;
    
    public override IReadOnlyList<AstNode> Children => new[] { Name, Value };
    
    /// <summary>
    /// Конструктор
    /// </summary>
    public ZodEnumValueNode(
        IdentifierNode name,
        LiteralExpressionNode value,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(metadata)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Value = value ?? throw new ArgumentNullException(nameof(value));
    }
    
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is IZodAstVisitor<TResult> zodVisitor)
        {
            return zodVisitor.VisitZodEnumValue(this);
        }
        return visitor.VisitDefault(this);
    }
    
    public override AstNode Clone()
    {
        return new ZodEnumValueNode(
            (IdentifierNode)Name.Clone(),
            (LiteralExpressionNode)Value.Clone(),
            Metadata.ToImmutableDictionary());
    }
}
