using System.Collections.Immutable;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Java.Ast.Visitors;

namespace ValIO.Generators.Java.Ast.Nodes;

/// <summary>
/// Java <c>import</c>-секция, например <c>import java.util.List;</c> или <c>import static foo.Bar.X;</c>.
/// </summary>
public sealed class JavaImportNode : AstNode
{
    public string FullyQualifiedName { get; }
    public bool IsStatic { get; }
    public bool IsWildcard { get; }

    public override IReadOnlyList<AstNode> Children => ImmutableList<AstNode>.Empty;

    public JavaImportNode(
        string fullyQualifiedName,
        bool isStatic = false,
        bool isWildcard = false,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(metadata)
    {
        FullyQualifiedName = fullyQualifiedName?.Trim()
            ?? throw new ArgumentNullException(nameof(fullyQualifiedName));
        IsStatic = isStatic;
        IsWildcard = isWildcard;
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is IJavaAstVisitor<TResult> javaVisitor)
            return javaVisitor.VisitJavaImport(this);
        throw new NotSupportedException($"Visitor must implement IJavaAstVisitor<TResult> to visit {nameof(JavaImportNode)}.");
    }

    public override AstNode Clone() =>
        new JavaImportNode(FullyQualifiedName, IsStatic, IsWildcard, Metadata.ToImmutableDictionary());
}
