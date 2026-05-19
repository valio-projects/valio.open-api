using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Visitors;

namespace ValIO.Core.Ast.Nodes;

/// <summary>
/// Узел комментария
/// </summary>
public sealed class CommentNode : AstNode
{
    /// <summary>
    /// Текст комментария
    /// </summary>
    public string Text { get; }
    
    /// <summary>
    /// Тип комментария
    /// </summary>
    public CommentKind CommentKind { get; }
    
    
    public override IReadOnlyList<AstNode> Children => Array.Empty<AstNode>();
    
    /// <summary>
    /// Конструктор
    /// </summary>
    public CommentNode(string text, CommentKind kind = CommentKind.SingleLine, IReadOnlyDictionary<string, object>? metadata = null)
        : base(metadata)
    {
        Text = text ?? throw new ArgumentNullException(nameof(text));
        CommentKind = kind;
    }
    
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        return visitor.VisitComment(this);
    }
    
    public override AstNode Clone()
    {
        return new CommentNode(Text, CommentKind, Metadata.ToImmutableDictionary());
    }
    
    public override string ToString()
    {
        var textPreview = Text.Length > 50 ? Text.Substring(0, 50) + "..." : Text;
        return $"Comment({CommentKind}, \"{textPreview.Replace("\n", "\\n").Replace("\r", "\\r")}\")";
    }
}
