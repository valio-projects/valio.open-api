using System.Collections.Immutable;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Kotlin.Ast.Visitors;

namespace ValIO.Generators.Kotlin.Ast.Nodes;

/// <summary>
/// Kotlin import statement:
///   <c>import com.example.Foo</c>
///   <c>import com.example.Foo as Bar</c>
///   <c>import com.example.*</c>
/// </summary>
public sealed class KotlinImportNode : AstNode
{
    /// <summary>Fully qualified path (e.g. <c>com.example.User</c>).</summary>
    public string FqName { get; }

    /// <summary>Optional alias (<c>as Bar</c>).</summary>
    public string? Alias { get; }

    public override IReadOnlyList<AstNode> Children => Array.Empty<AstNode>();

    public KotlinImportNode(
        string fqName,
        string? alias = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(metadata)
    {
        if (string.IsNullOrWhiteSpace(fqName))
            throw new ArgumentException("Import path cannot be null or whitespace", nameof(fqName));
        FqName = fqName;
        Alias = alias;
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is IKotlinAstVisitor<TResult> ktVisitor)
            return ktVisitor.VisitKotlinImport(this);
        throw new NotSupportedException("Visitor must implement IKotlinAstVisitor<TResult> to visit KotlinImportNode");
    }

    public override AstNode Clone() =>
        new KotlinImportNode(FqName, Alias, Metadata.ToImmutableDictionary());
}
