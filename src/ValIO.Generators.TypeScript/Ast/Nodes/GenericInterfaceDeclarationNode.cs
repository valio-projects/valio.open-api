using System.Collections.Immutable;
using ValIO.Core.Ast.Nodes;

namespace ValIO.Generators.TypeScript.Ast.Nodes;

/// <summary>Represents a TypeScript interface with type parameters scoped to its declaration.</summary>
public sealed class GenericInterfaceDeclarationNode : InterfaceDeclarationNode
{
    /// <summary>Creates an exported interface with the specified type parameter defaults and members.</summary>
    /// <param name="name">The declared name without type arguments.</param>
    /// <param name="genericParameters">The type parameters in declaration order.</param>
    /// <param name="members">The interface members.</param>
    public GenericInterfaceDeclarationNode(
        IdentifierNode name,
        IReadOnlyList<GenericParameterNode> genericParameters,
        IReadOnlyList<DeclarationNode> members)
        : base(name, members: members)
    {
        GenericParameters = genericParameters.ToImmutableList();
    }

    /// <summary>Gets the parameter declarations visible to each member.</summary>
    /// <value>The immutable sequence of type parameters.</value>
    public IReadOnlyList<GenericParameterNode> GenericParameters { get; }

    /// <inheritdoc />
    public override IReadOnlyList<AstNode> Children => base.Children.Concat(GenericParameters).ToImmutableList();

    /// <inheritdoc />
    public override AstNode Clone() => new GenericInterfaceDeclarationNode(
        (IdentifierNode)Name.Clone(),
        GenericParameters.Select(parameter => (GenericParameterNode)parameter.Clone()).ToImmutableList(),
        Members.Select(member => (DeclarationNode)member.Clone()).ToImmutableList());
}
