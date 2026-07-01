using System.Collections.Immutable;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;

namespace ValIO.Core.Ast.Nodes.Types;

/// <summary>
/// An inline object <b>type literal</b> in type position, e.g. <c>{ a?: T; b: U }</c>.
/// </summary>
/// <remarks>
/// This is the structural counterpart of a named <see cref="InterfaceDeclarationNode"/>: it describes an
/// anonymous object shape directly inside another type — a parameter type, an
/// <see cref="IntersectionTypeNode"/> member, another member's type, and so on — without introducing a named
/// declaration. It is deliberately distinct from <c>ObjectLiteralExpressionNode</c>, which is an
/// <i>expression</i> (a runtime value such as <c>{ a: 1 }</c>) and not a type. Members are ordered and are
/// rendered as <c>{ name?: Type; … }</c> by the language renderer; an empty member list renders as <c>{}</c>.
/// </remarks>
/// <seealso cref="TypeLiteralMember"/>
public sealed class TypeLiteralNode : TypeNode
{
    /// <summary>The ordered members (property signatures) of the object type literal.</summary>
    /// <value>An immutable, possibly-empty list of <see cref="TypeLiteralMember"/>.</value>
    public IReadOnlyList<TypeLiteralMember> Members { get; }

    /// <inheritdoc/>
    /// <remarks>Exposes each member's <see cref="TypeLiteralMember.Type"/> so generic AST traversal descends into them.</remarks>
    public override IReadOnlyList<AstNode> Children => Members.Select(m => (AstNode)m.Type).ToImmutableList();

    /// <summary>Initializes a new <see cref="TypeLiteralNode"/>.</summary>
    /// <param name="members">The object type members; may be empty for the <c>{}</c> type.</param>
    /// <param name="isNullable">Whether the type is nullable (renders a trailing <c>| null</c>).</param>
    /// <param name="isOptional">Whether the type is optional (renders a trailing <c>?</c>).</param>
    /// <param name="metadata">Optional, language-agnostic metadata bag.</param>
    /// <exception cref="ArgumentNullException"><paramref name="members"/> is <see langword="null"/>.</exception>
    public TypeLiteralNode(
        IReadOnlyList<TypeLiteralMember> members,
        bool isNullable = false,
        bool isOptional = false,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(isNullable, isOptional, metadata)
    {
        Members = (members ?? throw new ArgumentNullException(nameof(members))).ToImmutableList();
    }

    /// <inheritdoc/>
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor) => visitor.VisitTypeLiteral(this);

    /// <inheritdoc/>
    public override AstNode Clone() => new TypeLiteralNode(
        Members.Select(m => m.Clone()).ToImmutableList(),
        IsNullable,
        IsOptional,
        Metadata.ToImmutableDictionary());

    /// <inheritdoc/>
    public override string ToString()
    {
        var nullableStr = IsNullable ? " | null" : string.Empty;
        var optionalStr = IsOptional ? "?" : string.Empty;
        return Members.Count == 0
            ? $"{{}}{optionalStr}{nullableStr}"
            : $"{{ {string.Join("; ", Members)} }}{optionalStr}{nullableStr}";
    }
}

/// <summary>
/// A single member of a <see cref="TypeLiteralNode"/>: a named — optionally <c>?</c>-optional and optionally
/// <c>readonly</c> — property signature such as <c>name?: Type</c>.
/// </summary>
/// <remarks>
/// Intentionally a lightweight value holder rather than an <see cref="AstNode"/>: a type-literal member has no
/// independent dispatch target (it is only ever rendered as part of its owning <see cref="TypeLiteralNode"/>),
/// so it carries no <c>Accept</c>/visitor method. Its <see cref="Type"/> is a real node and participates in
/// traversal via <see cref="TypeLiteralNode.Children"/>.
/// </remarks>
public sealed class TypeLiteralMember
{
    /// <summary>The member name (the property key).</summary>
    public string Name { get; }

    /// <summary>The member's declared type.</summary>
    public TypeNode Type { get; }

    /// <summary>Whether the member is optional (renders a <c>?</c> after <see cref="Name"/>).</summary>
    public bool IsOptional { get; }

    /// <summary>Whether the member is read-only (renders a <c>readonly</c> modifier before <see cref="Name"/>).</summary>
    public bool IsReadonly { get; }

    /// <summary>Initializes a new <see cref="TypeLiteralMember"/>.</summary>
    /// <param name="name">The member name.</param>
    /// <param name="type">The member's type.</param>
    /// <param name="isOptional">Whether the member is optional.</param>
    /// <param name="isReadonly">Whether the member is read-only.</param>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> or <paramref name="type"/> is <see langword="null"/>.</exception>
    public TypeLiteralMember(string name, TypeNode type, bool isOptional = false, bool isReadonly = false)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Type = type ?? throw new ArgumentNullException(nameof(type));
        IsOptional = isOptional;
        IsReadonly = isReadonly;
    }

    /// <summary>Creates a deep copy of this member, cloning its <see cref="Type"/>.</summary>
    /// <returns>A new, structurally-equal <see cref="TypeLiteralMember"/>.</returns>
    public TypeLiteralMember Clone() => new(Name, (TypeNode)Type.Clone(), IsOptional, IsReadonly);

    /// <inheritdoc/>
    public override string ToString()
    {
        var readonlyStr = IsReadonly ? "readonly " : string.Empty;
        var optionalStr = IsOptional ? "?" : string.Empty;
        return $"{readonlyStr}{Name}{optionalStr}: {Type}";
    }
}
