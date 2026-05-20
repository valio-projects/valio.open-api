using System.Collections.Immutable;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Ruby.Ast.Visitors;

namespace ValIO.Generators.Ruby.Ast.Nodes;

/// <summary>
/// A single Ruby source file (.rb) with <c>require</c> directives and top-level
/// class declarations. The file is rendered with a <c># frozen_string_literal: true</c>
/// pragma at the very top.
/// </summary>
public sealed class RubyFileNode : AstNode
{
    /// <summary>List of <c>require</c> paths (e.g. <c>"faraday"</c>, <c>"json"</c>).</summary>
    public IReadOnlyList<string> Requires { get; }

    /// <summary>Top-level class declarations.</summary>
    public IReadOnlyList<RubyClassNode> Classes { get; }

    /// <summary>Optional leading file-level documentation comment.</summary>
    public CommentNode? FileDoc { get; }

    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var children = new List<AstNode>();
            if (FileDoc != null) children.Add(FileDoc);
            children.AddRange(Classes);
            return children;
        }
    }

    public RubyFileNode(
        IReadOnlyList<RubyClassNode>? classes = null,
        IReadOnlyList<string>? requires = null,
        CommentNode? fileDoc = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(metadata)
    {
        Requires = requires?.ToImmutableList() ?? ImmutableList<string>.Empty;
        Classes = classes?.ToImmutableList() ?? ImmutableList<RubyClassNode>.Empty;
        FileDoc = fileDoc;
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is IRubyAstVisitor<TResult> rubyVisitor)
            return rubyVisitor.VisitRubyFile(this);
        throw new NotSupportedException("Visitor must implement IRubyAstVisitor<TResult> to visit RubyFileNode");
    }

    public override AstNode Clone() =>
        new RubyFileNode(
            Classes.Select(c => (RubyClassNode)c.Clone()).ToImmutableList(),
            Requires.ToImmutableList(),
            FileDoc != null ? (CommentNode)FileDoc.Clone() : null,
            Metadata.ToImmutableDictionary());
}
