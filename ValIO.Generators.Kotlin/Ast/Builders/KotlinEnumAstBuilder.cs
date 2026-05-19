using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Interfaces;
using ValIO.Core.IR;
using ValIO.Generators.Kotlin.Ast.Nodes;

namespace ValIO.Generators.Kotlin.Ast.Builders;

/// <summary>
/// Builds a Kotlin <c>enum class</c> with <c>@Serializable</c> + per-member
/// <c>@SerialName</c> for an IR enum.
/// </summary>
public sealed class KotlinEnumAstBuilder
{
    private readonly INamingConverter _namingConverter;

    public KotlinEnumAstBuilder(INamingConverter namingConverter)
    {
        _namingConverter = namingConverter;
    }

    public KotlinFileNode BuildFile(IrEnum irEnum, string modelsPackage)
    {
        var isString = irEnum.BaseType == IrEnumBaseType.String;
        var members = new List<KotlinEnumMemberNode>(irEnum.Values.Count);
        var needsSerialName = false;

        foreach (var v in irEnum.Values)
        {
            var memberName = ToScreamingSnakeCase(v.Name);
            var annotations = new List<KotlinAnnotationNode>();
            if (isString)
            {
                var wireValue = v.Value?.ToString() ?? "";
                annotations.Add(new KotlinAnnotationNode(
                    "SerialName",
                    [$"\"{EscapeStringLiteral(wireValue)}\""]));
                needsSerialName = true;
            }

            members.Add(new KotlinEnumMemberNode(
                memberName,
                annotations,
                documentation: !string.IsNullOrWhiteSpace(v.Description)
                    ? new CommentNode(v.Description!, CommentKind.SingleLine)
                    : null));
        }

        var enumDecl = new KotlinEnumDeclarationNode(
            new IdentifierNode(_namingConverter.ToPascalCase(irEnum.Name)),
            members,
            annotations: [new KotlinAnnotationNode("Serializable")],
            documentation: !string.IsNullOrWhiteSpace(irEnum.Description)
                ? new CommentNode(irEnum.Description, CommentKind.MultiLine)
                : null);

        var imports = new List<KotlinImportNode>
        {
            new KotlinImportNode("kotlinx.serialization.Serializable"),
        };
        if (needsSerialName)
            imports.Add(new KotlinImportNode("kotlinx.serialization.SerialName"));

        return new KotlinFileNode(modelsPackage, declarations: [enumDecl], imports: imports);
    }

    private string ToScreamingSnakeCase(string name)
    {
        var snake = _namingConverter.ToSnakeCase(name);
        return string.IsNullOrEmpty(snake) ? "_" : snake.ToUpperInvariant();
    }

    private static string EscapeStringLiteral(string s) =>
        s.Replace("\\", "\\\\", StringComparison.Ordinal)
         .Replace("\"", "\\\"", StringComparison.Ordinal)
         .Replace("\n", "\\n", StringComparison.Ordinal)
         .Replace("\r", "\\r", StringComparison.Ordinal);
}
