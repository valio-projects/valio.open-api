using System.Collections.Immutable;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.FSharp.Ast.Visitors;

namespace ValIO.Generators.FSharp.Ast.Nodes;

/// <summary>
/// F# record type:
///   <c>type Name = {{ Field1: T1; Field2: T2 }}</c>
/// </summary>
public sealed class FSharpRecordNode : DeclarationNode
{
    /// <summary>Record field (label + type).</summary>
    public sealed class Field : IEquatable<Field>
    {
        /// <summary>Initializes the values carried by this model.</summary>
        /// <param name="Name">The field name in the generated record.</param>
        /// <param name="Type">The syntax node for the field's declared type.</param>
        public Field(string Name, TypeNode Type)
        {
            this.Name = Name;
            this.Type = Type;
        }

        /// <summary>Gets the field name in the generated record.</summary>
        /// <value>The field name in the generated record.</value>
        public string Name { get; init; }

        /// <summary>Gets the syntax node for the field's declared type.</summary>
        /// <value>The syntax node for the field's declared type.</value>
        public TypeNode Type { get; init; }

        /// <summary>Determines whether another value has the same stored members.</summary>
        /// <param name="other">The instance to compare.</param>
        /// <returns>True when all stored members are equal.</returns>
        public bool Equals(Field? other) => other is not null
            && EqualityComparer<string>.Default.Equals(Name, other.Name)
            && EqualityComparer<TypeNode>.Default.Equals(Type, other.Type);

        /// <inheritdoc />
        public override bool Equals(object? obj) => obj is Field other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() => HashCode.Combine(Name, Type);

        /// <summary>Compares two values for equality.</summary>
        /// <param name="left">The first value.</param>
        /// <param name="right">The second value.</param>
        /// <returns>True when both are null or carry the same member values.</returns>
        public static bool operator ==(Field? left, Field? right) => Equals(left, right);

        /// <summary>Compares two values for inequality.</summary>
        /// <param name="left">The first value.</param>
        /// <param name="right">The second value.</param>
        /// <returns>True when the values differ.</returns>
        public static bool operator !=(Field? left, Field? right) => !Equals(left, right);
    }

    /// <summary>Ordered list of record fields.</summary>
    public IReadOnlyList<Field> Fields { get; }

    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var children = new List<AstNode> { Name };
            if (Documentation != null) children.Add(Documentation);
            foreach (var f in Fields)
                children.Add(f.Type);
            return children;
        }
    }

    public FSharpRecordNode(
        IdentifierNode name,
        IReadOnlyList<Field>? fields = null,
        CommentNode? documentation = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(name, documentation, attributes: null, metadata)
    {
        Fields = fields?.ToImmutableList() ?? ImmutableList<Field>.Empty;
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is IFSharpAstVisitor<TResult> fsVisitor)
            return fsVisitor.VisitFSharpRecord(this);
        throw new NotSupportedException("Visitor must implement IFSharpAstVisitor<TResult> to visit FSharpRecordNode");
    }

    public override AstNode Clone() =>
        new FSharpRecordNode(
            (IdentifierNode)Name.Clone(),
            Fields.Select(f => new Field(f.Name, (TypeNode)f.Type.Clone())).ToImmutableList(),
            Documentation != null ? (CommentNode)Documentation.Clone() : null,
            Metadata.ToImmutableDictionary());
}
