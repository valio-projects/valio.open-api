using System.Collections.Immutable;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.TypeScript.Zod.Ast.Enums;
using ValIO.Generators.TypeScript.Zod.Ast.Visitors;

namespace ValIO.Generators.TypeScript.Zod.Ast.Nodes;

/// <summary>
/// Узел Zod object схемы
/// </summary>
public sealed class ZodObjectNode : ZodSchemaNode
{
    /// <summary>
    /// Свойства объекта
    /// </summary>
    public IReadOnlyList<ZodPropertyNode> Properties { get; }
    
    
    public override IReadOnlyList<AstNode> Children => Properties.Cast<AstNode>().ToImmutableList();
    
    /// <summary>
    /// Конструктор
    /// </summary>
    public ZodObjectNode(
        IReadOnlyList<ZodPropertyNode> properties,
        TypeNode? type = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(ZodSchemaKind.Object, type, metadata)
    {
        Properties = properties?.ToImmutableList() ?? throw new ArgumentNullException(nameof(properties));
    }
    
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is IZodAstVisitor<TResult> zodVisitor)
        {
            return zodVisitor.VisitZodObject(this);
        }
        return base.Accept(visitor);
    }
    
    public override AstNode Clone()
    {
        return new ZodObjectNode(
            Properties.Select(p => (ZodPropertyNode)p.Clone()).ToImmutableList(),
            Type != null ? (TypeNode)Type.Clone() : null,
            Metadata.ToImmutableDictionary());
    }
}
