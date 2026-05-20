using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Interfaces;
using ValIO.Core.IR;
using ValIO.Generators.Python.Ast.Nodes;

namespace ValIO.Generators.Python.Ast.Builders;

/// <summary>
/// Строит Python <c>StrEnum</c> / <c>IntEnum</c> модуль из IR-enum.
/// </summary>
public sealed class PythonEnumAstBuilder
{
    private readonly INamingConverter _namingConverter;

    public PythonEnumAstBuilder(INamingConverter namingConverter)
    {
        _namingConverter = namingConverter;
    }

    public PythonModuleNode BuildModule(IrEnum irEnum)
    {
        var isString = irEnum.BaseType == IrEnumBaseType.String;
        var baseClass = isString ? PythonEnumBase.StrEnum : PythonEnumBase.IntEnum;

        var members = new List<PythonEnumMemberNode>(irEnum.Values.Count);
        foreach (var v in irEnum.Values)
        {
            var memberName = ToScreamingSnakeCase(v.Name);
            var literal = RenderEnumLiteral(v.Value, isString);
            members.Add(new PythonEnumMemberNode(
                memberName,
                literal,
                documentation: !string.IsNullOrWhiteSpace(v.Description)
                    ? new CommentNode(v.Description!, CommentKind.SingleLine)
                    : null));
        }

        var enumDecl = new PythonEnumDeclarationNode(
            new IdentifierNode(irEnum.Name),
            baseClass,
            members,
            documentation: !string.IsNullOrWhiteSpace(irEnum.Description)
                ? new CommentNode(irEnum.Description, CommentKind.MultiLine)
                : null);

        var imports = new List<PythonImportNode>
        {
            new PythonImportNode("enum", new[] { isString ? "StrEnum" : "IntEnum" })
        };

        return new PythonModuleNode(
            declarations: new DeclarationNode[] { enumDecl },
            imports: imports);
    }

    private string ToScreamingSnakeCase(string name)
    {
        var snake = _namingConverter.ToSnakeCase(name);
        return string.IsNullOrEmpty(snake) ? "_" : snake.ToUpperInvariant();
    }

    private static string RenderEnumLiteral(object value, bool isString)
    {
        if (isString)
        {
            var s = value?.ToString() ?? "";
            return $"\"{EscapeStringLiteral(s)}\"";
        }
        return value?.ToString() ?? "0";
    }

    private static string EscapeStringLiteral(string s) =>
        s.Replace("\\", "\\\\", StringComparison.Ordinal)
         .Replace("\"", "\\\"", StringComparison.Ordinal)
         .Replace("\n", "\\n", StringComparison.Ordinal)
         .Replace("\r", "\\r", StringComparison.Ordinal);
}
