using System.Globalization;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.IR.Constraints;

namespace ValIO.Generators.TypeScript.Ast.JsDoc;

/// <summary>
/// Helpers for assembling JSDoc comments for generated TypeScript: a standard file header and
/// multi-tag comment blocks. Multi-line text is rendered correctly because
/// <see cref="ValIO.Core.Utilities.CommentGenerator.GenerateJSDocComment"/> splits on newlines.
/// </summary>
public static class TypeScriptDoc
{
    /// <summary>Standard generated-file header: <c>@file</c> / <c>@module</c> / <c>@description</c> / <c>@generated</c> banner.</summary>
    public static CommentNode FileHeader(string fileName, string module, string? description)
    {
        var lines = new List<string>
        {
            $"@file {fileName}",
            $"@module {module}",
        };
        if (!string.IsNullOrWhiteSpace(description))
            lines.Add($"@description {description.Trim()}");
        lines.Add("@generated");
        lines.Add("Do not edit by hand — regenerate via ValIO.");
        return new CommentNode(string.Join("\n", lines), CommentKind.JSDoc);
    }

    /// <summary>
    /// Builds a JSDoc comment from an optional leading description plus a set of <c>@tag value</c> lines.
    /// Tag entries with a <c>null</c> value are skipped; an empty value renders the bare <c>@tag</c>.
    /// Returns <c>null</c> when nothing would be emitted.
    /// </summary>
    public static CommentNode? Block(string? description, params (string Tag, string? Value)[] tags)
        => Block(description, (IEnumerable<(string Tag, string? Value)>)tags);

    public static CommentNode? Block(string? description, IEnumerable<(string Tag, string? Value)> tags)
    {
        var lines = new List<string>();
        if (!string.IsNullOrWhiteSpace(description))
            lines.Add(description.Trim());

        foreach (var (tag, value) in tags)
        {
            if (value == null)
                continue;
            lines.Add(value.Length == 0 ? $"@{tag}" : $"@{tag} {value}");
        }

        return lines.Count == 0 ? null : new CommentNode(string.Join("\n", lines), CommentKind.JSDoc);
    }

    /// <summary>
    /// Constraint / format annotations for a property as JSDoc tag lines (<c>@format</c>, <c>@minLength</c>,
    /// <c>@maxLength</c>, <c>@minimum</c>, <c>@maximum</c>, <c>@exclusiveMinimum</c>, <c>@exclusiveMaximum</c>,
    /// <c>@pattern</c>, <c>@default</c>). Reused by models, params and schema docs.
    /// </summary>
    public static IEnumerable<(string Tag, string? Value)> ConstraintTags(IrPropertyConstraints? c, string? format = null)
    {
        if (!string.IsNullOrWhiteSpace(format))
            yield return ("format", format);

        if (c == null)
            yield break;

        if (c.MinLength.HasValue) yield return ("minLength", c.MinLength.Value.ToString(CultureInfo.InvariantCulture));
        if (c.MaxLength.HasValue) yield return ("maxLength", c.MaxLength.Value.ToString(CultureInfo.InvariantCulture));
        if (c.Min.HasValue) yield return ("minimum", c.Min.Value.ToString(CultureInfo.InvariantCulture));
        if (c.Max.HasValue) yield return ("maximum", c.Max.Value.ToString(CultureInfo.InvariantCulture));
        if (c.ExclusiveMin.HasValue) yield return ("exclusiveMinimum", c.ExclusiveMin.Value.ToString(CultureInfo.InvariantCulture));
        if (c.ExclusiveMax.HasValue) yield return ("exclusiveMaximum", c.ExclusiveMax.Value.ToString(CultureInfo.InvariantCulture));
        if (!string.IsNullOrEmpty(c.Pattern)) yield return ("pattern", c.Pattern);
        if (c.DefaultValue != null) yield return ("default", Convert.ToString(c.DefaultValue, CultureInfo.InvariantCulture));
    }
}
