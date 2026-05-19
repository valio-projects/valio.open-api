using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Java.Ast.Visitors;

namespace ValIO.Generators.Java.Ast.Nodes;

/// <summary>
/// Java 16+ record declaration: <c>public record User(long id, String name) { ... }</c>.
/// Параметры записи хранятся как <see cref="ParameterDeclarationNode"/>; типы — обычные <see cref="ValIO.Core.Ast.Nodes.TypeNode"/>.
/// </summary>
public sealed class JavaRecordDeclarationNode : TypeDeclarationNode
{
    public IReadOnlyList<ParameterDeclarationNode> Components { get; }
    public IReadOnlyList<string> Implements { get; }

    public JavaRecordDeclarationNode(
        IdentifierNode name,
        IReadOnlyList<ParameterDeclarationNode>? components = null,
        IReadOnlyList<string>? implements = null,
        IReadOnlyList<DeclarationNode>? members = null,
        CommentNode? documentation = null,
        IReadOnlyList<AttributeNode>? attributes = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(name, AccessModifier.Public, baseTypes: null, members: members, documentation: documentation, attributes: attributes, metadata: metadata)
    {
        Components = components?.ToImmutableList() ?? ImmutableList<ParameterDeclarationNode>.Empty;
        Implements = implements?.ToImmutableList() ?? ImmutableList<string>.Empty;
    }

    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var c = new List<AstNode> { Name };
            if (Documentation != null) c.Add(Documentation);
            c.AddRange(Attributes);
            c.AddRange(Components);
            c.AddRange(Members);
            return c;
        }
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is IJavaAstVisitor<TResult> javaVisitor)
            return javaVisitor.VisitJavaRecord(this);
        throw new NotSupportedException($"Visitor must implement IJavaAstVisitor<TResult> to visit {nameof(JavaRecordDeclarationNode)}.");
    }

    public override AstNode Clone() => new JavaRecordDeclarationNode(
        (IdentifierNode)Name.Clone(),
        Components.Select(p => (ParameterDeclarationNode)p.Clone()).ToImmutableList(),
        Implements.ToImmutableList(),
        Members.Select(m => (DeclarationNode)m.Clone()).ToImmutableList(),
        Documentation != null ? (CommentNode)Documentation.Clone() : null,
        Attributes.Select(a => (AttributeNode)a.Clone()).ToImmutableList(),
        Metadata.ToImmutableDictionary());
}
