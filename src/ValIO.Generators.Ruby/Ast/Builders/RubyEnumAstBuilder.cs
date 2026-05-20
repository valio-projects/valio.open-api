using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Interfaces;
using ValIO.Core.IR;
using ValIO.Generators.Ruby.Ast.Nodes;

namespace ValIO.Generators.Ruby.Ast.Builders;

/// <summary>
/// Builds a Ruby class containing one frozen-string (or integer) constant per
/// IR enum value. Ruby has no <c>enum</c> keyword; this is the canonical
/// representation in idiomatic Ruby gem code.
/// </summary>
public sealed class RubyEnumAstBuilder
{
    private readonly INamingConverter _namingConverter;

    public RubyEnumAstBuilder(INamingConverter namingConverter)
    {
        _namingConverter = namingConverter;
    }

    public RubyFileNode BuildFile(IrEnum irEnum)
    {
        var isString = irEnum.BaseType == IrEnumBaseType.String;
        var members = new List<DeclarationNode>(irEnum.Values.Count);

        foreach (var v in irEnum.Values)
        {
            var constName = ToScreamingSnakeCase(v.Name);
            var literal = RenderEnumLiteral(v.Value, isString);
            members.Add(new RubyConstantNode(
                new IdentifierNode(constName),
                literal,
                documentation: !string.IsNullOrWhiteSpace(v.Description)
                    ? new CommentNode(v.Description!, CommentKind.SingleLine)
                    : null));
        }

        var className = _namingConverter.ToPascalCase(irEnum.Name);
        var classNode = new RubyClassNode(
            new IdentifierNode(className),
            parentClass: null,
            members: members,
            documentation: !string.IsNullOrWhiteSpace(irEnum.Description)
                ? new CommentNode(irEnum.Description!, CommentKind.MultiLine)
                : null);

        return new RubyFileNode(classes: [classNode]);
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
            return $"'{EscapeSingleQuoted(s)}'";
        }
        return value?.ToString() ?? "0";
    }

    private static string EscapeSingleQuoted(string s) =>
        s.Replace("\\", "\\\\", StringComparison.Ordinal)
         .Replace("'", "\\'", StringComparison.Ordinal);
}
