using System.Collections.Immutable;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Dart.Ast.Visitors;

namespace ValIO.Generators.Dart.Ast.Nodes;

/// <summary>
/// A Dart function/method parameter. Supports the three Dart parameter
/// styles: positional, optional positional, named (with or without
/// <c>required</c>).
/// </summary>
public sealed class DartParameterNode : AstNode
{
    public IdentifierNode Name { get; }
    public TypeNode Type { get; }
    public bool IsRequired { get; }
    public bool IsNamed { get; }
    public string? DefaultValueLiteral { get; }

    public override IReadOnlyList<AstNode> Children => new AstNode[] { Name, Type };

    public DartParameterNode(
        IdentifierNode name,
        TypeNode type,
        bool isRequired = true,
        bool isNamed = false,
        string? defaultValueLiteral = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(metadata)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Type = type ?? throw new ArgumentNullException(nameof(type));
        IsRequired = isRequired;
        IsNamed = isNamed;
        DefaultValueLiteral = defaultValueLiteral;
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is IDartAstVisitor<TResult> dartVisitor)
            return dartVisitor.VisitDartParameter(this);
        throw new NotSupportedException("Visitor must implement IDartAstVisitor<TResult> to visit DartParameterNode");
    }

    public override AstNode Clone() =>
        new DartParameterNode(
            (IdentifierNode)Name.Clone(),
            (TypeNode)Type.Clone(),
            IsRequired,
            IsNamed,
            DefaultValueLiteral,
            Metadata.ToImmutableDictionary());
}
