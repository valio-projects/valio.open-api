using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.TypeScript.Ast.Visitors;

namespace ValIO.Generators.TypeScript.Ast.Nodes;

/// <summary>Re-exports all members or a selected group from another TypeScript module.</summary>
/// <param name="module">The source module specifier.</param>
/// <param name="symbols">The emitted identifiers, or null for a star re-export.</param>
/// <param name="typeOnly">Whether the named exports exist only at type-checking time.</param>
public sealed class ModuleReExportNode(string module, IReadOnlyList<IdentifierNode>? symbols = null, bool typeOnly = false)
    : DeclarationNode(new IdentifierNode(module))
{
    /// <summary>Gets the selected identifiers.</summary>
    /// <value>Null for a star re-export.</value>
    public IReadOnlyList<IdentifierNode>? Symbols { get; } = symbols?.ToArray();

    /// <summary>Gets whether to emit a type-only export.</summary>
    /// <value>Whether the selected names are types.</value>
    public bool TypeOnly { get; } = typeOnly;

    /// <inheritdoc/>
    public override IReadOnlyList<AstNode> Children => [Name, .. Symbols ?? []];

    /// <inheritdoc/>
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor) =>
        visitor is ITypeScriptAstVisitor<TResult> typeScript
            ? typeScript.VisitModuleReExport(this)
            : visitor.DefaultVisit(this);

    /// <inheritdoc/>
    public override AstNode Clone() => new ModuleReExportNode(Name.Name,
        Symbols?.Select(symbol => (IdentifierNode)symbol.Clone()).ToArray(), TypeOnly);
}
