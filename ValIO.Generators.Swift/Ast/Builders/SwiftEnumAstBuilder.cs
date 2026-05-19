using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Interfaces;
using ValIO.Core.IR;
using ValIO.Generators.Swift.Ast.Nodes;

namespace ValIO.Generators.Swift.Ast.Builders;

/// <summary>
/// Builds a Swift <c>public enum</c> conforming to <c>Codable, Sendable</c>
/// (string- or Int-backed) for one <see cref="IrEnum"/>.
/// </summary>
public sealed class SwiftEnumAstBuilder
{
    private readonly INamingConverter _namingConverter;

    public SwiftEnumAstBuilder(INamingConverter namingConverter)
    {
        _namingConverter = namingConverter;
    }

    public SwiftFileNode BuildFile(IrEnum irEnum)
    {
        var isString = irEnum.BaseType == IrEnumBaseType.String;
        var members = new List<SwiftEnumMemberNode>(irEnum.Values.Count);

        foreach (var v in irEnum.Values)
        {
            var caseName = SwiftReservedNames.Avoid(_namingConverter.ToCamelCase(v.Name));
            string? rawLiteral = null;
            if (isString)
            {
                var raw = v.Value?.ToString() ?? "";
                rawLiteral = $"\"{EscapeStringLiteral(raw)}\"";
            }
            else if (v.Value is not null)
            {
                rawLiteral = v.Value.ToString();
            }

            members.Add(new SwiftEnumMemberNode(
                caseName,
                rawLiteral,
                documentation: !string.IsNullOrWhiteSpace(v.Description)
                    ? new CommentNode(v.Description!, CommentKind.SingleLine)
                    : null));
        }

        var rawType = isString ? "String" : "Int";

        var enumDecl = new SwiftEnumDeclarationNode(
            new IdentifierNode(_namingConverter.ToPascalCase(irEnum.Name)),
            members,
            conformances: ["Codable", "Sendable"],
            rawType: rawType,
            accessModifier: "public",
            attributes: null,
            documentation: !string.IsNullOrWhiteSpace(irEnum.Description)
                ? new CommentNode(irEnum.Description, CommentKind.MultiLine)
                : null);

        var imports = new List<SwiftImportNode>
        {
            new SwiftImportNode("Foundation"),
        };

        return new SwiftFileNode(declarations: [enumDecl], imports: imports);
    }

    private static string EscapeStringLiteral(string s) =>
        s.Replace("\\", "\\\\", StringComparison.Ordinal)
         .Replace("\"", "\\\"", StringComparison.Ordinal)
         .Replace("\n", "\\n", StringComparison.Ordinal)
         .Replace("\r", "\\r", StringComparison.Ordinal);
}
