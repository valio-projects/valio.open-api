using System.Collections.Immutable;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Python.Ast.Visitors;

namespace ValIO.Generators.Python.Ast.Nodes;

/// <summary>
/// Аннотированное присваивание уровня класса/модуля:
///   <c>name: Type = default</c>
///   <c>name: Type</c>
///   <c>name = value</c>
///   <c>name: "ForwardRef"</c>
/// Поддерживает квотированные (forward-ref) аннотации через <see cref="QuoteAnnotation"/>.
/// </summary>
public sealed class PythonAttributeAssignmentNode : DeclarationNode
{
    /// <summary>Аннотация типа (может быть null для безаннотированных присваиваний).</summary>
    public TypeNode? Annotation { get; }

    /// <summary>Литерал значения по умолчанию (raw Python-выражение). Если null — присваивание отсутствует.</summary>
    public string? DefaultValueLiteral { get; }

    /// <summary>Если true — рендерим аннотацию как строковый forward-reference: <c>"X | None"</c>.</summary>
    public bool QuoteAnnotation { get; }

    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var children = new List<AstNode> { Name };
            if (Annotation != null) children.Add(Annotation);
            if (Documentation != null) children.Add(Documentation);
            return children;
        }
    }

    public PythonAttributeAssignmentNode(
        IdentifierNode name,
        TypeNode? annotation = null,
        string? defaultValueLiteral = null,
        bool quoteAnnotation = false,
        CommentNode? documentation = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(name, documentation, attributes: null, metadata)
    {
        Annotation = annotation;
        DefaultValueLiteral = defaultValueLiteral;
        QuoteAnnotation = quoteAnnotation;
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is IPythonAstVisitor<TResult> pyVisitor)
            return pyVisitor.VisitPythonAttributeAssignment(this);
        throw new NotSupportedException("Visitor must implement IPythonAstVisitor<TResult> to visit PythonAttributeAssignmentNode");
    }

    public override AstNode Clone() =>
        new PythonAttributeAssignmentNode(
            (IdentifierNode)Name.Clone(),
            Annotation != null ? (TypeNode)Annotation.Clone() : null,
            DefaultValueLiteral,
            QuoteAnnotation,
            Documentation != null ? (CommentNode)Documentation.Clone() : null,
            Metadata.ToImmutableDictionary());
}
