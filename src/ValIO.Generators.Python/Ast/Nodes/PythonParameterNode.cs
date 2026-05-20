using System.Collections.Immutable;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Python.Ast.Visitors;

namespace ValIO.Generators.Python.Ast.Nodes;

/// <summary>
/// Параметр Python-функции / метода.
/// </summary>
public sealed class PythonParameterNode : AstNode
{
    public IdentifierNode Name { get; }
    public TypeNode? Annotation { get; }
    public string? DefaultValueLiteral { get; }

    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var children = new List<AstNode> { Name };
            if (Annotation != null) children.Add(Annotation);
            return children;
        }
    }

    public PythonParameterNode(
        IdentifierNode name,
        TypeNode? annotation = null,
        string? defaultValueLiteral = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(metadata)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Annotation = annotation;
        DefaultValueLiteral = defaultValueLiteral;
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is IPythonAstVisitor<TResult> pyVisitor)
            return pyVisitor.VisitPythonParameter(this);
        throw new NotSupportedException("Visitor must implement IPythonAstVisitor<TResult> to visit PythonParameterNode");
    }

    public override AstNode Clone() =>
        new PythonParameterNode(
            (IdentifierNode)Name.Clone(),
            Annotation != null ? (TypeNode)Annotation.Clone() : null,
            DefaultValueLiteral,
            Metadata.ToImmutableDictionary());
}
