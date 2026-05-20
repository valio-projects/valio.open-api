using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.Interfaces;
using ValIO.Core.IR;
using ValIO.Generators.Swift.Ast.Nodes;

namespace ValIO.Generators.Swift.Ast.Builders;

/// <summary>
/// Builds a Swift <c>public struct</c> conforming to <c>Codable, Sendable</c>
/// for one <see cref="IrModel"/>. Emits one <see cref="SwiftFileNode"/> per model.
/// </summary>
public sealed class SwiftStructAstBuilder
{
    private readonly INamingConverter _namingConverter;
    private readonly SwiftIrTypeMapper _typeMapper;

    public SwiftStructAstBuilder(INamingConverter namingConverter, SwiftIrTypeMapper typeMapper)
    {
        _namingConverter = namingConverter;
        _typeMapper = typeMapper;
    }

    public SwiftFileNode BuildFile(IrModel model)
    {
        var importScan = new SwiftTypeImportScanner.Result();
        var properties = new List<SwiftPropertyNode>(model.Properties.Count);

        foreach (var prop in model.Properties)
        {
            SwiftTypeImportScanner.Walk(prop.Type, importScan);

            var camelName = _namingConverter.ToCamelCase(prop.Name);
            var safeName = SwiftReservedNames.Avoid(camelName);
            var mappedType = _typeMapper.MapType(prop.Type);

            var isOptional = _typeMapper.IsOptionalInLanguage(prop);
            var finalType = isOptional ? EnsureNullable(mappedType) : mappedType;
            var defaultValue = isOptional ? "nil" : null;

            properties.Add(new SwiftPropertyNode(
                new IdentifierNode(safeName),
                finalType,
                defaultValue,
                isConstant: true,
                accessModifier: "public",
                isStatic: false,
                attributes: null,
                documentation: !string.IsNullOrWhiteSpace(prop.Description)
                    ? new CommentNode(prop.Description!, CommentKind.MultiLine)
                    : null));
        }

        // Foundation is needed when there's a Date/Data field; always add it so
        // generated structs without those still compile cleanly (cheap inclusion).
        importScan.StandardImports.Add("Foundation");

        var imports = importScan.StandardImports
            .Select(name => new SwiftImportNode(name))
            .ToList();

        var structNode = new SwiftStructDeclarationNode(
            new IdentifierNode(_namingConverter.ToPascalCase(model.Name)),
            properties,
            conformances: ["Codable", "Sendable"],
            accessModifier: "public",
            attributes: null,
            documentation: !string.IsNullOrWhiteSpace(model.Description)
                ? new CommentNode(model.Description, CommentKind.MultiLine)
                : null);

        return new SwiftFileNode(declarations: [structNode], imports: imports);
    }

    private static TypeNode EnsureNullable(TypeNode type) =>
        type is NullableTypeNode ? type : new NullableTypeNode(type);
}
