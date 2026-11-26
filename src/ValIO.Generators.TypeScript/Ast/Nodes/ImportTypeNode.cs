using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.TypeScript.Ast.Visitors;

namespace ValIO.Generators.TypeScript.Ast.Nodes;

/// <summary>References an exported type without adding a top-level binding to the module.</summary>
/// <param name="module">The module specifier.</param>
/// <param name="name">The exported type name.</param>
public sealed class ImportTypeNode(string module, IdentifierNode name) : TypeNode
{
    /// <summary>Gets the module where the type is declared.</summary>
    /// <value>The module specifier.</value>
    public string Module { get; } = module;

    /// <summary>Gets the exported type name.</summary>
    /// <value>The identifier selected from the module.</value>
    public IdentifierNode Name { get; } = name;

    /// <inheritdoc/>
    public override IReadOnlyList<AstNode> Children => [Name];

    /// <inheritdoc/>
    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor) =>
        visitor is ITypeScriptAstVisitor<TResult> typeScript
            ? typeScript.VisitImportType(this)
            : visitor.DefaultVisit(this);

    /// <inheritdoc/>
    public override AstNode Clone() => new ImportTypeNode(Module, (IdentifierNode)Name.Clone());
}
