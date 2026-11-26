using System.Collections.Immutable;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.FSharp.Ast.Visitors;

namespace ValIO.Generators.FSharp.Ast.Nodes;

/// <summary>
/// F# function binding:
///   <c>let name (p1: T1) (p2: T2) : R =\n    body</c>
/// Body lines are pre-rendered strings to keep the AST minimal — they live on the
/// AST node itself and only the renderer's Visit* methods emit text.
/// </summary>
public sealed class FSharpFunctionNode : DeclarationNode
{
    /// <summary>Parameter (name + type annotation).</summary>
    public sealed class Parameter : IEquatable<Parameter>
    {
        /// <summary>Initializes the values carried by this model.</summary>
        /// <param name="Name">The parameter name in generated code.</param>
        /// <param name="Type">The syntax node for the parameter's declared type.</param>
        public Parameter(string Name, TypeNode Type)
        {
            this.Name = Name;
            this.Type = Type;
        }

        /// <summary>Gets the parameter name in generated code.</summary>
        /// <value>The parameter name in generated code.</value>
        public string Name { get; init; }

        /// <summary>Gets the syntax node for the parameter's declared type.</summary>
        /// <value>The syntax node for the parameter's declared type.</value>
        public TypeNode Type { get; init; }

        /// <summary>Determines whether another value has the same stored members.</summary>
        /// <param name="other">The instance to compare.</param>
        /// <returns>True when all stored members are equal.</returns>
        public bool Equals(Parameter? other) => other is not null
            && EqualityComparer<string>.Default.Equals(Name, other.Name)
            && EqualityComparer<TypeNode>.Default.Equals(Type, other.Type);

        /// <inheritdoc />
        public override bool Equals(object? obj) => obj is Parameter other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() => HashCode.Combine(Name, Type);

        /// <summary>Compares two values for equality.</summary>
        /// <param name="left">The first value.</param>
        /// <param name="right">The second value.</param>
        /// <returns>True when both are null or carry the same member values.</returns>
        public static bool operator ==(Parameter? left, Parameter? right) => Equals(left, right);

        /// <summary>Compares two values for inequality.</summary>
        /// <param name="left">The first value.</param>
        /// <param name="right">The second value.</param>
        /// <returns>True when the values differ.</returns>
        public static bool operator !=(Parameter? left, Parameter? right) => !Equals(left, right);
    }

    /// <summary>Ordered list of parameters.</summary>
    public IReadOnlyList<Parameter> Parameters { get; }

    /// <summary>Annotated return type. Null = inferred / unit (renderer omits the annotation).</summary>
    public TypeNode? ReturnType { get; }

    /// <summary>Gets the executable syntax of the function.</summary>
    /// <value>The ordered statements in the body.</value>
    public BlockNode Body { get; }

    /// <inheritdoc />
    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var children = new List<AstNode> { Name };
            if (Documentation != null) children.Add(Documentation);
            foreach (var p in Parameters)
                children.Add(p.Type);
            if (ReturnType != null) children.Add(ReturnType);
            children.Add(Body);
            return children;
        }
    }

    /// <summary>Creates a typed F# function declaration.</summary>
    /// <param name="name">The identifier of the function.</param>
    /// <param name="parameters">The curried formal arguments.</param>
    /// <param name="returnType">The optional result annotation.</param>
    /// <param name="body">The executable syntax of the function.</param>
    /// <param name="documentation">The optional documentation.</param>
    /// <param name="metadata">The associated metadata.</param>
    public FSharpFunctionNode(
        IdentifierNode name,
        IReadOnlyList<Parameter>? parameters = null,
        TypeNode? returnType = null,
        BlockNode? body = null,
        CommentNode? documentation = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(name, documentation, attributes: null, metadata)
    {
        Parameters = parameters?.ToImmutableList() ?? ImmutableList<Parameter>.Empty;
        ReturnType = returnType;
        Body = body ?? new BlockNode();
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is IFSharpAstVisitor<TResult> fsVisitor)
            return fsVisitor.VisitFSharpFunction(this);
        throw new NotSupportedException("Visitor must implement IFSharpAstVisitor<TResult> to visit FSharpFunctionNode");
    }

    /// <inheritdoc />
    public override AstNode Clone() =>
        new FSharpFunctionNode(
            (IdentifierNode)Name.Clone(),
            Parameters.Select(p => new Parameter(p.Name, (TypeNode)p.Type.Clone())).ToImmutableList(),
            ReturnType != null ? (TypeNode)ReturnType.Clone() : null,
            (BlockNode)Body.Clone(),
            Documentation != null ? (CommentNode)Documentation.Clone() : null,
            Metadata.ToImmutableDictionary());
}
