using System.Collections.Immutable;
using ValIO.Core.Ast.Visitors;

namespace ValIO.Core.Ast.Nodes.Types;

/// <summary>
/// Тип как raw-строка (для сложных TypeScript-выражений: Parameters&lt;typeof fn&gt;[1] и т.д.)
/// </summary>
public sealed class RawTypeNode : TypeNode
{
    /// <summary>
    /// Строковое представление типа (рендерится как есть)
    /// </summary>
    public string RawTypeString { get; }


    public override IReadOnlyList<AstNode> Children => ImmutableArray<AstNode>.Empty;

    public RawTypeNode(
        string rawTypeString,
        bool isNullable = false,
        bool isOptional = false,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(isNullable, isOptional, metadata)
    {
        RawTypeString = rawTypeString ?? throw new ArgumentNullException(nameof(rawTypeString));
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        return visitor.VisitRawType(this);
    }

    public override AstNode Clone()
    {
        return new RawTypeNode(RawTypeString, IsNullable, IsOptional, Metadata.ToImmutableDictionary());
    }
}
