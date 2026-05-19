using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Java.Ast.Visitors;

namespace ValIO.Generators.Java.Ast.Nodes;

/// <summary>
/// Java class declaration. Поддерживает <c>final</c>, <c>abstract</c>, поля, конструктор и методы.
/// </summary>
public sealed class JavaClassDeclarationNode : TypeDeclarationNode
{
    public bool IsFinal { get; }
    public bool IsAbstract { get; }
    public IReadOnlyList<string> Implements { get; }
    public string? Extends { get; }

    /// <summary>
    /// Поля класса (final поля). Конструктор должен присваивать их.
    /// </summary>
    public IReadOnlyList<PropertyDeclarationNode> Fields { get; }

    /// <summary>
    /// Конструктор класса (опциональный). Тело и параметры берутся из <see cref="MethodDeclarationNode"/>.
    /// Имя конструктора должно совпадать с именем класса (рендерер игнорирует <see cref="MethodDeclarationNode.Name"/>).
    /// </summary>
    public MethodDeclarationNode? Constructor { get; }

    public JavaClassDeclarationNode(
        IdentifierNode name,
        bool isFinal = true,
        bool isAbstract = false,
        string? extends = null,
        IReadOnlyList<string>? implements = null,
        IReadOnlyList<PropertyDeclarationNode>? fields = null,
        MethodDeclarationNode? constructor = null,
        IReadOnlyList<DeclarationNode>? members = null,
        CommentNode? documentation = null,
        IReadOnlyList<AttributeNode>? attributes = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(name, AccessModifier.Public, baseTypes: null, members: members, documentation: documentation, attributes: attributes, metadata: metadata)
    {
        IsFinal = isFinal;
        IsAbstract = isAbstract;
        Extends = extends;
        Implements = implements?.ToImmutableList() ?? ImmutableList<string>.Empty;
        Fields = fields?.ToImmutableList() ?? ImmutableList<PropertyDeclarationNode>.Empty;
        Constructor = constructor;
    }

    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var c = new List<AstNode> { Name };
            if (Documentation != null) c.Add(Documentation);
            c.AddRange(Attributes);
            c.AddRange(Fields);
            if (Constructor != null) c.Add(Constructor);
            c.AddRange(Members);
            return c;
        }
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is IJavaAstVisitor<TResult> javaVisitor)
            return javaVisitor.VisitJavaClass(this);
        throw new NotSupportedException($"Visitor must implement IJavaAstVisitor<TResult> to visit {nameof(JavaClassDeclarationNode)}.");
    }

    public override AstNode Clone() => new JavaClassDeclarationNode(
        (IdentifierNode)Name.Clone(),
        IsFinal,
        IsAbstract,
        Extends,
        Implements.ToImmutableList(),
        Fields.Select(f => (PropertyDeclarationNode)f.Clone()).ToImmutableList(),
        Constructor != null ? (MethodDeclarationNode)Constructor.Clone() : null,
        Members.Select(m => (DeclarationNode)m.Clone()).ToImmutableList(),
        Documentation != null ? (CommentNode)Documentation.Clone() : null,
        Attributes.Select(a => (AttributeNode)a.Clone()).ToImmutableList(),
        Metadata.ToImmutableDictionary());
}
