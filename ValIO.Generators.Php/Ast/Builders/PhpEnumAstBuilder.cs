using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.Interfaces;
using ValIO.Core.IR;
using ValIO.Generators.Php.Ast.Nodes;

namespace ValIO.Generators.Php.Ast.Builders;

/// <summary>
/// Builds a PHP 8.1+ backed enum from an <see cref="IrEnum"/>.
/// </summary>
public sealed class PhpEnumAstBuilder
{
    private readonly INamingConverter _namingConverter;

    public PhpEnumAstBuilder(INamingConverter namingConverter)
    {
        _namingConverter = namingConverter ?? throw new ArgumentNullException(nameof(namingConverter));
    }

    public PhpSourceFileNode BuildSourceFile(IrEnum irEnum, string @namespace)
    {
        var ns = NormalizeNamespace($"{@namespace}\\Models");

        var members = new List<DeclarationNode>();
        foreach (var v in irEnum.Values)
        {
            var caseName = _namingConverter.ToPascalCase(v.Name);
            ExpressionNode value;
            if (irEnum.BaseType == IrEnumBaseType.String)
            {
                value = new LiteralExpressionNode(v.Value?.ToString() ?? string.Empty, LiteralKind.String);
            }
            else
            {
                var raw = v.Value?.ToString() ?? "0";
                value = new LiteralExpressionNode(raw, LiteralKind.Number);
            }
            var doc = !string.IsNullOrWhiteSpace(v.Description)
                ? new CommentNode(v.Description!, CommentKind.MultiLine)
                : null;
            members.Add(new EnumMemberDeclarationNode(
                name: new IdentifierNode(caseName),
                value: value,
                documentation: doc));
        }

        var underlying = irEnum.BaseType == IrEnumBaseType.String
            ? new SimpleTypeNode(new IdentifierNode("string"), isPrimitive: true)
            : new SimpleTypeNode(new IdentifierNode("int"), isPrimitive: true);

        var enumDoc = !string.IsNullOrWhiteSpace(irEnum.Description)
            ? new CommentNode(irEnum.Description!, CommentKind.MultiLine)
            : null;

        var enumDecl = new EnumDeclarationNode(
            name: new IdentifierNode(irEnum.Name),
            underlyingType: underlying,
            members: members,
            documentation: enumDoc);

        return new PhpSourceFileNode(
            @namespace: ns,
            uses: ImmutableList<PhpUseStatementNode>.Empty,
            declarations: new DeclarationNode[] { enumDecl }.ToImmutableList());
    }

    private static string NormalizeNamespace(string ns) => ns.Trim('\\').Replace('/', '\\');
}
