using System.Collections.Immutable;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Kotlin.Ast.Visitors;

namespace ValIO.Generators.Kotlin.Ast.Nodes;

/// <summary>
/// A single Kotlin source file (.kt) with package, imports, and top-level declarations.
/// </summary>
public sealed class KotlinFileNode : AstNode
{
    /// <summary>Package declaration (e.g. <c>com.example.generated.models</c>).</summary>
    public string Package { get; }

    /// <summary>Imports.</summary>
    public IReadOnlyList<KotlinImportNode> Imports { get; }

    /// <summary>Top-level declarations (data classes, enums, regular classes).</summary>
    public IReadOnlyList<DeclarationNode> Declarations { get; }

    /// <summary>File-level KDoc.</summary>
    public CommentNode? FileDoc { get; }

    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var children = new List<AstNode>();
            if (FileDoc != null) children.Add(FileDoc);
            children.AddRange(Imports);
            children.AddRange(Declarations);
            return children;
        }
    }

    public KotlinFileNode(
        string package_,
        IReadOnlyList<DeclarationNode>? declarations = null,
        IReadOnlyList<KotlinImportNode>? imports = null,
        CommentNode? fileDoc = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(metadata)
    {
        Package = package_ ?? throw new ArgumentNullException(nameof(package_));
        Imports = imports?.ToImmutableList() ?? ImmutableList<KotlinImportNode>.Empty;
        Declarations = declarations?.ToImmutableList() ?? ImmutableList<DeclarationNode>.Empty;
        FileDoc = fileDoc;
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is IKotlinAstVisitor<TResult> ktVisitor)
            return ktVisitor.VisitKotlinFile(this);
        throw new NotSupportedException("Visitor must implement IKotlinAstVisitor<TResult> to visit KotlinFileNode");
    }

    public override AstNode Clone() =>
        new KotlinFileNode(
            Package,
            Declarations.Select(d => (DeclarationNode)d.Clone()).ToImmutableList(),
            Imports.Select(i => (KotlinImportNode)i.Clone()).ToImmutableList(),
            FileDoc != null ? (CommentNode)FileDoc.Clone() : null,
            Metadata.ToImmutableDictionary());
}
