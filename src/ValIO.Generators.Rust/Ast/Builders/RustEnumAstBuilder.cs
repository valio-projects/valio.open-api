using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Interfaces;
using ValIO.Core.IR;
using ValIO.Generators.Rust.Ast.Nodes;

namespace ValIO.Generators.Rust.Ast.Builders;

/// <summary>
/// Builds a Rust <c>pub enum</c> deriving <c>Debug, Clone, Serialize, Deserialize</c>
/// with <c>#[serde(rename_all = "camelCase")]</c> for one <see cref="IrEnum"/>.
/// </summary>
public sealed class RustEnumAstBuilder
{
    private readonly INamingConverter _namingConverter;

    public RustEnumAstBuilder(INamingConverter namingConverter)
    {
        _namingConverter = namingConverter;
    }

    public RustFileNode BuildFile(IrEnum irEnum)
    {
        var variants = new List<RustEnumVariantNode>(irEnum.Values.Count);

        foreach (var v in irEnum.Values)
        {
            var variantName = RustReservedNames.Avoid(_namingConverter.ToPascalCase(v.Name));
            variants.Add(new RustEnumVariantNode(
                new IdentifierNode(variantName),
                payload: null,
                documentation: !string.IsNullOrWhiteSpace(v.Description)
                    ? new CommentNode(v.Description!, CommentKind.SingleLine)
                    : null));
        }

        var enumNode = new RustEnumNode(
            new IdentifierNode(_namingConverter.ToPascalCase(irEnum.Name)),
            variants,
            derives: null,
            serdeRenameAll: "camelCase",
            documentation: !string.IsNullOrWhiteSpace(irEnum.Description)
                ? new CommentNode(irEnum.Description, CommentKind.MultiLine)
                : null);

        var uses = ImmutableList.Create("serde::{Deserialize, Serialize}");

        return new RustFileNode(declarations: [enumNode], uses: uses);
    }
}
