using System.Collections.Immutable;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Kotlin.Ast.Visitors;

namespace ValIO.Generators.Kotlin.Ast.Nodes;

/// <summary>
/// Function parameter: <c>name: Type</c> or <c>name: Type = default</c>.
/// </summary>
public sealed class KotlinParameterNode : AstNode
{
    public IdentifierNode Name { get; }
    public TypeNode Type { get; }
    public string? DefaultValueLiteral { get; }

    public override IReadOnlyList<AstNode> Children => new AstNode[] { Name, Type };

    public KotlinParameterNode(
        IdentifierNode name,
        TypeNode type,
        string? defaultValueLiteral = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(metadata)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Type = type ?? throw new ArgumentNullException(nameof(type));
        DefaultValueLiteral = defaultValueLiteral;
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is IKotlinAstVisitor<TResult> ktVisitor)
            return ktVisitor.VisitKotlinParameter(this);
        throw new NotSupportedException("Visitor must implement IKotlinAstVisitor<TResult> to visit KotlinParameterNode");
    }

    public override AstNode Clone() =>
        new KotlinParameterNode(
            (IdentifierNode)Name.Clone(),
            (TypeNode)Type.Clone(),
            DefaultValueLiteral,
            Metadata.ToImmutableDictionary());
}
