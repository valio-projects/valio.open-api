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
public sealed class RustModelAstBuilder
{
    private readonly INamingConverter _namingConverter;
    private readonly RustIrTypeMapper _typeMapper;

    public RustModelAstBuilder(INamingConverter namingConverter, RustIrTypeMapper typeMapper)
    {
        _namingConverter = namingConverter;
        _typeMapper = typeMapper;
    }

    public RustFileNode BuildFile(IrModel model)
    {
        var fields = new List<RustFieldNode>(model.Properties.Count);
        var uses = new SortedSet<string>(StringComparer.Ordinal)
        {
            "serde::{Deserialize, Serialize}",
        };

        foreach (var prop in model.Properties)
        {
            var snakeName = RustReservedNames.Avoid(_namingConverter.ToSnakeCase(prop.Name));
            var mappedType = _typeMapper.MapType(prop.Type);
            var isOptional = _typeMapper.IsOptionalInLanguage(prop);
            var finalType = isOptional ? EnsureOption(mappedType) : mappedType;

            // Add use directive for chrono types if applicable (scan the rendered tree).
            ScanForUses(finalType, uses);

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
            new IdentifierNode(_namingConverter.ToPascalCase(model.Name)),
            fields,
            derives: null, // default Debug, Clone, Serialize, Deserialize
            documentation: !string.IsNullOrWhiteSpace(model.Description)
                ? new CommentNode(model.Description, CommentKind.MultiLine)
                : null);

        return new RustFileNode(declarations: [structNode], uses: uses.ToImmutableList());
    }

    private static TypeNode EnsureOption(TypeNode type) =>
        type is GenericTypeNode g && g.Name.Name == "Option"
            ? type
            : new GenericTypeNode(new IdentifierNode("Option"), ImmutableList.Create<TypeNode>(type));

    private static void ScanForUses(TypeNode type, SortedSet<string> uses)
    {
        switch (type)
        {
            case SimpleTypeNode s:
                MaybeAddChronoUse(s.Name.Name, uses);
                break;
            case GenericTypeNode g:
                MaybeAddChronoUse(g.Name.Name, uses);
                foreach (var arg in g.TypeArguments)
                    ScanForUses(arg, uses);
                break;
            case ArrayTypeNode a:
                ScanForUses(a.ElementType, uses);
                break;
            case DictionaryTypeNode d:
                ScanForUses(d.KeyType, uses);
                ScanForUses(d.ValueType, uses);
                break;
            case NullableTypeNode n:
                ScanForUses(n.InnerType, uses);
                break;
            case OptionalTypeNode o:
                ScanForUses(o.InnerType, uses);
                break;
        }
    }

    private static void MaybeAddChronoUse(string typeName, SortedSet<string> uses)
    {
        if (typeName.StartsWith("chrono::", StringComparison.Ordinal))
            uses.Add("chrono");
    }
}
