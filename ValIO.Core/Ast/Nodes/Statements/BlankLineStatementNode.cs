using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Visitors;

namespace ValIO.Core.Ast.Nodes.Statements;

/// <summary>
/// Узел пустой строки для форматирования (не рендерится в код, только отступ/перенос).
/// </summary>
public sealed class BlankLineStatementNode : StatementNode
{
    public override AstNodeKind Kind => AstNodeKind.BlankLineStatement;

    public override IReadOnlyList<AstNode> Children => ImmutableArray<AstNode>.Empty;

    public BlankLineStatementNode(IReadOnlyDictionary<string, object>? metadata = null)
        : base(documentation: null, metadata)
    {
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        return visitor.VisitBlankLineStatement(this);
    }

    public override AstNode Clone()
    {
        return new BlankLineStatementNode(Metadata.ToImmutableDictionary());
    }
}
