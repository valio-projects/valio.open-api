using System.Collections.Immutable;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Java.Ast.Visitors;

namespace ValIO.Generators.Java.Ast.Nodes;

/// <summary>
/// Java source-файл: <c>package ...;</c>, imports, declarations. Один top-level type на файл.
/// </summary>
public sealed class JavaSourceFileNode : AstNode
{
    public string? Package { get; }
    public IReadOnlyList<JavaImportNode> Imports { get; }
    public IReadOnlyList<DeclarationNode> Declarations { get; }

    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var c = new List<AstNode>();
            c.AddRange(Imports);
            c.AddRange(Declarations);
            return c;
        }
    }

    public JavaSourceFileNode(
        string? package = null,
        IReadOnlyList<JavaImportNode>? imports = null,
        IReadOnlyList<DeclarationNode>? declarations = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(metadata)
    {
        Package = package;
        Imports = imports?.ToImmutableList() ?? ImmutableList<JavaImportNode>.Empty;
        Declarations = declarations?.ToImmutableList() ?? ImmutableList<DeclarationNode>.Empty;
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is IJavaAstVisitor<TResult> javaVisitor)
            return javaVisitor.VisitJavaSourceFile(this);
        throw new NotSupportedException($"Visitor must implement IJavaAstVisitor<TResult> to visit {nameof(JavaSourceFileNode)}.");
    }

    public override AstNode Clone() => new JavaSourceFileNode(
        Package,
        Imports.Select(i => (JavaImportNode)i.Clone()).ToImmutableList(),
        Declarations.Select(d => (DeclarationNode)d.Clone()).ToImmutableList(),
        Metadata.ToImmutableDictionary());
}
