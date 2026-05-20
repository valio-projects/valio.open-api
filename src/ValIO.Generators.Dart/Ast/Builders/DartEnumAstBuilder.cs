using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.Interfaces;
using ValIO.Core.IR;
using ValIO.Generators.Dart.Ast.Nodes;

namespace ValIO.Generators.Dart.Ast.Builders;

/// <summary>
/// Builds a Dart enum for one <see cref="IrEnum"/>.
/// </summary>
public sealed class DartEnumAstBuilder
{
    private readonly INamingConverter _namingConverter;

    public DartEnumAstBuilder(INamingConverter namingConverter)
    {
        _namingConverter = namingConverter;
    }

    public DartFileNode BuildFile(IrEnum enumModel)
    {
        var enumName = _namingConverter.ToPascalCase(enumModel.Name);
        var isStringBacked = enumModel.BaseType == IrEnumBaseType.String;
        var backing = isStringBacked
            ? (TypeNode)new SimpleTypeNode(new IdentifierNode("String"), isPrimitive: true)
            : new SimpleTypeNode(new IdentifierNode("int"), isPrimitive: true);

        var members = new List<DartEnumMemberNode>(enumModel.Values.Count);
        foreach (var v in enumModel.Values)
        {
            var memberName = _namingConverter.ToCamelCase(v.Name);
            var safe = DartReservedNames.Avoid(memberName);
            var lit = v.Value switch
            {
                string s => $"'{s.Replace("'", "\\'", StringComparison.Ordinal)}'",
                bool b => b ? "true" : "false",
                null => "null",
                _ => v.Value!.ToString()!,
            };
            members.Add(new DartEnumMemberNode(new IdentifierNode(safe), lit));
        }

        var en = new DartEnumDeclarationNode(
            new IdentifierNode(enumName),
            members,
            backingType: backing,
            documentation: !string.IsNullOrWhiteSpace(enumModel.Description)
                ? new CommentNode(enumModel.Description!, CommentKind.MultiLine)
                : null);

        return new DartFileNode(declarations: new[] { en });
    }
}
