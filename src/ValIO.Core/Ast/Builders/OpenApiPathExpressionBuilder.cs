using System.Text.RegularExpressions;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.IR;

namespace ValIO.Core.Ast.Builders;

/// <summary>Separates an OpenAPI path's literal segments from its declared positional substitutions.</summary>
public static partial class OpenApiPathExpressionBuilder
{
    /// <summary>Builds a path expression that each language can print with its own interpolation syntax.</summary>
    /// <param name="operation">The operation that owns the route and its parameters.</param>
    /// <param name="reference">The target-language expression for an argument.</param>
    /// <returns>The path with literal and substitution nodes in source order.</returns>
    public static InterpolatedStringExpressionNode Build(IrOperation operation, Func<IrParameter, ExpressionNode> reference)
    {
        var parts = new List<ExpressionNode>();
        var path = operation.Path;
        var position = 0;
        foreach (Match match in Placeholder().Matches(path))
        {
            var declared = operation.PathParameters.FirstOrDefault(parameter => parameter.OriginalName == match.Groups[1].Value);
            if (declared is null)
                continue;

            if (position < match.Index)
                parts.Add(new LiteralExpressionNode(path[position..match.Index], LiteralKind.String));
            parts.Add(reference(declared));
            position = match.Index + match.Length;
        }

        if (position < path.Length)
            parts.Add(new LiteralExpressionNode(path[position..], LiteralKind.String));
        return new InterpolatedStringExpressionNode(parts);
    }

    /// <summary>Matches an OpenAPI path placeholder without consuming adjacent literal text.</summary>
    /// <returns>The generated placeholder expression.</returns>
    [GeneratedRegex(@"\{([^{}]+)\}", RegexOptions.CultureInvariant)]
    private static partial Regex Placeholder();
}
