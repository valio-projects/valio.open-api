using System.Collections.Immutable;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Dart.Ast.Visitors;

namespace ValIO.Generators.Dart.Ast.Nodes;

/// <summary>
/// Dart import directive: <c>import 'package:foo/bar.dart';</c> or
/// <c>import 'package:foo/bar.dart' as f;</c>.
/// </summary>
public sealed class DartImportNode : AstNode
{
    public string Uri { get; }
    public string? Alias { get; }

    public override IReadOnlyList<AstNode> Children => Array.Empty<AstNode>();

    public DartImportNode(string uri, string? alias = null,
        IReadOnlyDictionary<string, object>? metadata = null) : base(metadata)
    {
        Uri = uri ?? throw new ArgumentNullException(nameof(uri));
        Alias = alias;
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is IDartAstVisitor<TResult> dartVisitor)
            return dartVisitor.VisitDartImport(this);
        throw new NotSupportedException("Visitor must implement IDartAstVisitor<TResult> to visit DartImportNode");
    }

    public override AstNode Clone() =>
        new DartImportNode(Uri, Alias, Metadata.ToImmutableDictionary());
}
