using System.Collections.Immutable;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.FSharp.Ast.Visitors;

namespace ValIO.Generators.FSharp.Ast.Nodes;

/// <summary>
/// F# discriminated union:
///   <c>type Name = | Case1 of payload | Case2</c>
/// </summary>
public sealed class FSharpDuNode : DeclarationNode
{
    /// <summary>Single DU case (case name + optional payload type).</summary>
    public sealed class Case : IEquatable<Case>
    {
        /// <summary>Initializes the values carried by this model.</summary>
        /// <param name="Name">The variant name in generated code.</param>
        /// <param name="Payload">The syntax node for the optional payload type.</param>
        public Case(string Name, TypeNode? Payload)
        {
            this.Name = Name;
            this.Payload = Payload;
        }

        /// <summary>Gets the variant name in generated code.</summary>
        /// <value>The variant name in generated code.</value>
        public string Name { get; init; }

        /// <summary>Gets the syntax node for the optional payload type.</summary>
        /// <value>The syntax node for the optional payload type.</value>
        public TypeNode? Payload { get; init; }

        /// <summary>Determines whether another value has the same stored members.</summary>
        /// <param name="other">The instance to compare.</param>
        /// <returns>True when all stored members are equal.</returns>
        public bool Equals(Case? other) => other is not null
            && EqualityComparer<string>.Default.Equals(Name, other.Name)
            && EqualityComparer<TypeNode?>.Default.Equals(Payload, other.Payload);

        /// <inheritdoc />
        public override bool Equals(object? obj) => obj is Case other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() => HashCode.Combine(Name, Payload);

        /// <summary>Compares two values for equality.</summary>
        /// <param name="left">The first value.</param>
        /// <param name="right">The second value.</param>
        /// <returns>True when both are null or carry the same member values.</returns>
        public static bool operator ==(Case? left, Case? right) => Equals(left, right);

        /// <summary>Compares two values for inequality.</summary>
        /// <param name="left">The first value.</param>
        /// <param name="right">The second value.</param>
        /// <returns>True when the values differ.</returns>
        public static bool operator !=(Case? left, Case? right) => !Equals(left, right);
    }

    /// <summary>Ordered list of cases.</summary>
    public IReadOnlyList<Case> Cases { get; }

    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var children = new List<AstNode> { Name };
            if (Documentation != null) children.Add(Documentation);
            foreach (var c in Cases)
                if (c.Payload != null) children.Add(c.Payload);
            return children;
        }
    }

    public FSharpDuNode(
        IdentifierNode name,
        IReadOnlyList<Case>? cases = null,
        CommentNode? documentation = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(name, documentation, attributes: null, metadata)
    {
        Cases = cases?.ToImmutableList() ?? ImmutableList<Case>.Empty;
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is IFSharpAstVisitor<TResult> fsVisitor)
            return fsVisitor.VisitFSharpDU(this);
        throw new NotSupportedException("Visitor must implement IFSharpAstVisitor<TResult> to visit FSharpDuNode");
    }

    public override AstNode Clone() =>
        new FSharpDuNode(
            (IdentifierNode)Name.Clone(),
            Cases.Select(c => new Case(
                c.Name,
                c.Payload != null ? (TypeNode)c.Payload.Clone() : null)).ToImmutableList(),
            Documentation != null ? (CommentNode)Documentation.Clone() : null,
            Metadata.ToImmutableDictionary());
}
