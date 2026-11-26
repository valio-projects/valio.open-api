using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.Interfaces;
using ValIO.Core.IR;
using ValIO.Generators.Rust.Ast.Nodes;

namespace ValIO.Generators.Rust.Ast.Builders;

/// <summary>
/// Builds a Rust <c>pub struct</c> deriving <c>Debug, Clone, Serialize, Deserialize</c>
/// for one <see cref="IrModel"/>. Optional properties become <c>Option&lt;T&gt;</c>.
/// </summary>
public sealed class RustModelAstBuilder(INamingConverter namingConverter, RustIrTypeMapper typeMapper)
{

    /// <summary>Builds a Rust model file with the imports required by its fields.</summary>
    /// <param name="model">The effective model, including inherited fields.</param>
    /// <param name="context">The document's declarations and stable module names.</param>
    public RustFileNode BuildFile(IrModel model, IrDocumentContext context)
    {
        var fields = new List<RustFieldNode>(model.Properties.Count);
        var types = model.Properties.Select(property => property.Type);
        IReadOnlyList<ImportNode> uses = [RustImports.Serde(), .. RustImports.Models(context, namingConverter, types, model.Name)];

        foreach (var prop in model.Properties)
        {
            var snakeName = RustReservedNames.Avoid(namingConverter.ToSnakeCase(prop.Name));
            var mappedType = typeMapper.MapType(prop.Type);
            var isOptional = typeMapper.IsOptionalInLanguage(prop);
            var finalType = isOptional ? EnsureOption(mappedType) : mappedType;

            // serde rename only if the snake-cased identifier diverges from the wire name.
            var serdeRename = !string.Equals(snakeName, prop.OriginalName, StringComparison.Ordinal)
                ? prop.OriginalName
                : null;

            fields.Add(new RustFieldNode(
                new IdentifierNode(snakeName),
                finalType,
                serdeRename: serdeRename,
                documentation: !string.IsNullOrWhiteSpace(prop.Description)
                    ? new CommentNode(prop.Description!, CommentKind.MultiLine)
                    : null));
        }

        var structNode = new RustStructNode(
            new IdentifierNode(namingConverter.ToPascalCase(model.Name)),
            fields,
            derives: null, // default Debug, Clone, Serialize, Deserialize
            documentation: !string.IsNullOrWhiteSpace(model.Description)
                ? new CommentNode(model.Description, CommentKind.MultiLine)
                : null);

        return new RustFileNode(declarations: [structNode], uses: uses);
    }

    private static TypeNode EnsureOption(TypeNode type) =>
        type is GenericTypeNode g && g.Name.Name == "Option"
            ? type
            : new GenericTypeNode(new IdentifierNode("Option"), ImmutableList.Create<TypeNode>(type));

}
