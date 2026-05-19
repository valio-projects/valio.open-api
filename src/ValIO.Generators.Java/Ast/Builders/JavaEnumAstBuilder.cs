using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.Interfaces;
using ValIO.Core.IR;
using ValIO.Generators.Java.Ast.Nodes;

namespace ValIO.Generators.Java.Ast.Builders;

/// <summary>
/// Строит <see cref="JavaSourceFileNode"/> с обычным или backed enum-декларацией.
/// </summary>
public sealed class JavaEnumAstBuilder
{
    private readonly INamingConverter _namingConverter;

    public JavaEnumAstBuilder(INamingConverter namingConverter)
    {
        _namingConverter = namingConverter ?? throw new ArgumentNullException(nameof(namingConverter));
    }

    public JavaSourceFileNode BuildSourceFile(IrEnum irEnum, string rootPackage)
    {
        var pkg = NormalizePackage($"{rootPackage}.models");

        // Always emit backed enums: string enums use the original token, integer enums use the int value.
        // This matches the spec's "@JsonValue" example and gives consumers a stable wire representation.
        bool hasBacking = irEnum.Values.Count > 0;
        var members = new List<DeclarationNode>();

        foreach (var v in irEnum.Values)
        {
            var caseName = ToScreamingSnakeCase(_namingConverter.ToPascalCase(v.Name));
            ExpressionNode value;
            if (irEnum.BaseType == IrEnumBaseType.Integer)
            {
                var raw = v.Value?.ToString() ?? "0";
                value = new LiteralExpressionNode(raw, LiteralKind.Number);
            }
            else
            {
                value = new LiteralExpressionNode(v.Value?.ToString() ?? string.Empty, LiteralKind.String);
            }
            var doc = !string.IsNullOrWhiteSpace(v.Description)
                ? new CommentNode(v.Description!, CommentKind.MultiLine)
                : null;
            members.Add(new EnumMemberDeclarationNode(
                name: new IdentifierNode(caseName),
                value: value,
                documentation: doc));
        }

        var underlying = irEnum.BaseType == IrEnumBaseType.Integer
            ? new SimpleTypeNode(new IdentifierNode("int"), isPrimitive: true)
            : new SimpleTypeNode(new IdentifierNode("String"), isPrimitive: false);

        var enumDoc = !string.IsNullOrWhiteSpace(irEnum.Description)
            ? new CommentNode(irEnum.Description!, CommentKind.MultiLine)
            : null;

        var enumDecl = new EnumDeclarationNode(
            name: new IdentifierNode(irEnum.Name),
            accessModifier: AccessModifier.Public,
            underlyingType: hasBacking ? underlying : null,
            members: members,
            documentation: enumDoc);

        return new JavaSourceFileNode(
            package: pkg,
            imports: ImmutableList<JavaImportNode>.Empty,
            declarations: new DeclarationNode[] { enumDecl }.ToImmutableList());
    }

    private static string NormalizePackage(string pkg) => pkg.Trim('.').Replace('/', '.');

    /// <summary>Convert a PascalCase or camelCase token to SCREAMING_SNAKE_CASE for Java enum constants.</summary>
    internal static string ToScreamingSnakeCase(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        var sb = new System.Text.StringBuilder(input.Length + 4);
        for (int i = 0; i < input.Length; i++)
        {
            var c = input[i];
            if (i > 0 && char.IsUpper(c) && (char.IsLower(input[i - 1]) || (i + 1 < input.Length && char.IsLower(input[i + 1]))))
                sb.Append('_');
            sb.Append(char.ToUpperInvariant(c));
        }
        return sb.ToString();
    }
}
