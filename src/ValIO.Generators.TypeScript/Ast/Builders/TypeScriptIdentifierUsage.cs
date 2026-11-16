using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;

namespace ValIO.Generators.TypeScript.Ast.Builders;

/// <summary>Finds identifier expressions in generated TypeScript before emitting their declarations or imports.</summary>
internal static class TypeScriptIdentifierUsage
{
    /// <summary>Checks whether an AST subtree contains a reference to the identifier in an expression.</summary>
    /// <param name="node">The subtree to inspect.</param>
    /// <param name="identifier">The exact TypeScript identifier name.</param>
    /// <returns>Whether an identifier expression uses that name.</returns>
    public static bool References(AstNode node, string identifier) =>
        node is IdentifierExpressionNode expression && expression.Identifier.Name == identifier
        || node.Children.Any(child => References(child, identifier));
}
