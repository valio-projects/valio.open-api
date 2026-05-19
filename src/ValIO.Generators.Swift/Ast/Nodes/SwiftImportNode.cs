using System.Collections.Immutable;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.Swift.Ast.Visitors;

namespace ValIO.Generators.Swift.Ast.Nodes;

/// <summary>
/// Swift import statement: <c>import Foundation</c>.
/// </summary>
public sealed class SwiftImportNode : AstNode
{
    /// <summary>Module name (e.g. <c>Foundation</c>).</summary>
    public string ModuleName { get; }

    public override IReadOnlyList<AstNode> Children => Array.Empty<AstNode>();

    public SwiftImportNode(
        string moduleName,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(metadata)
    {
        if (string.IsNullOrWhiteSpace(moduleName))
            throw new ArgumentException("Module name cannot be null or whitespace", nameof(moduleName));
        ModuleName = moduleName;
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is ISwiftAstVisitor<TResult> swiftVisitor)
            return swiftVisitor.VisitSwiftImport(this);
        throw new NotSupportedException("Visitor must implement ISwiftAstVisitor<TResult> to visit SwiftImportNode");
    }

    public override AstNode Clone() =>
        new SwiftImportNode(ModuleName, Metadata.ToImmutableDictionary());
}
