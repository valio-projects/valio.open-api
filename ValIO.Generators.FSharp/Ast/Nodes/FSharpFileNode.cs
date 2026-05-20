using System.Collections.Immutable;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Visitors;
using ValIO.Generators.FSharp.Ast.Visitors;

namespace ValIO.Generators.FSharp.Ast.Nodes;

/// <summary>
/// A single F# source file (.fs) with a module declaration, opens and top-level declarations.
/// </summary>
public sealed class FSharpFileNode : AstNode
{
    /// <summary>F# module name (the bit after <c>module</c> at the top of the file).</summary>
    public string ModuleName { get; }

    /// <summary>Modules brought into scope via <c>open</c>.</summary>
    public IReadOnlyList<string> Opens { get; }

    /// <summary>Top-level declarations (records, DUs, functions).</summary>
    public IReadOnlyList<DeclarationNode> Declarations { get; }

    public override IReadOnlyList<AstNode> Children => Declarations;

    public FSharpFileNode(
        string moduleName,
        IReadOnlyList<DeclarationNode>? declarations = null,
        IReadOnlyList<string>? opens = null,
        IReadOnlyDictionary<string, object>? metadata = null)
        : base(metadata)
    {
        if (string.IsNullOrWhiteSpace(moduleName))
            throw new ArgumentException("Module name cannot be null or whitespace", nameof(moduleName));
        ModuleName = moduleName;
        Opens = opens?.ToImmutableList() ?? ImmutableList<string>.Empty;
        Declarations = declarations?.ToImmutableList() ?? ImmutableList<DeclarationNode>.Empty;
    }

    public override TResult Accept<TResult>(IAstVisitor<TResult> visitor)
    {
        if (visitor is IFSharpAstVisitor<TResult> fsVisitor)
            return fsVisitor.VisitFSharpFile(this);
        throw new NotSupportedException("Visitor must implement IFSharpAstVisitor<TResult> to visit FSharpFileNode");
    }

    public override AstNode Clone() =>
        new FSharpFileNode(
            ModuleName,
            Declarations.Select(d => (DeclarationNode)d.Clone()).ToImmutableList(),
            Opens.ToImmutableList(),
            Metadata.ToImmutableDictionary());
}
