using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;

namespace ValIO.Generators.TypeScript.Angular.Ast.Nodes;

/// <summary>
/// TypeScript class declaration tailored for Angular @Injectable services.
/// Carries the @Injectable decorator (via <see cref="DeclarationNode.Attributes"/>),
/// an Angular-style constructor with parameter properties, and method members.
/// Rendered by <see cref="Renderers.AngularServiceRenderer"/>.
/// </summary>
public sealed class AngularServiceClassNode : DeclarationNode
{
    /// <summary>
    /// Whether the class is exported (Angular services usually are).
    /// </summary>
    public bool IsExported { get; }

    /// <summary>
    /// Constructor parameters — each becomes a "private readonly" parameter property
    /// (Angular DI convention: <c>constructor(private readonly http: HttpClient) {}</c>).
    /// </summary>
    public IReadOnlyList<ParameterDeclarationNode> ConstructorParameters { get; }

    /// <summary>
    /// Methods of the class.
    /// </summary>
    public IReadOnlyList<MethodDeclarationNode> Methods { get; }

    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var children = new List<AstNode> { Name };
            if (Documentation != null)
                children.Add(Documentation);
            children.AddRange(Attributes);
            children.AddRange(ConstructorParameters);
            children.AddRange(Methods);
            return children;
        }
    }

    public AngularServiceClassNode(
        IdentifierNode name,
        IReadOnlyList<AttributeNode>? attributes = null,
        IReadOnlyList<ParameterDeclarationNode>? constructorParameters = null,
        IReadOnlyList<MethodDeclarationNode>? methods = null,
        bool isExported = true,
        CommentNode? documentation = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(name, documentation, attributes, metadata)
    {
        IsExported = isExported;
        ConstructorParameters = constructorParameters?.ToImmutableList() ?? ImmutableList<ParameterDeclarationNode>.Empty;
        Methods = methods?.ToImmutableList() ?? ImmutableList<MethodDeclarationNode>.Empty;
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        // Bespoke node; rendered directly by AngularServiceRenderer rather than
        // through the TypeScript visitor. SourceFile rendering handles dispatch.
        throw new NotSupportedException(
            "AngularServiceClassNode is rendered by AngularServiceRenderer, not visitor dispatch.");
    }

    public override AstNode Clone()
    {
        return new AngularServiceClassNode(
            (IdentifierNode)Name.Clone(),
            Attributes.Select(a => (AttributeNode)a.Clone()).ToImmutableList(),
            ConstructorParameters.Select(p => (ParameterDeclarationNode)p.Clone()).ToImmutableList(),
            Methods.Select(m => (MethodDeclarationNode)m.Clone()).ToImmutableList(),
            IsExported,
            Documentation != null ? (CommentNode)Documentation.Clone() : null,
            Metadata.ToImmutableDictionary());
    }
}
