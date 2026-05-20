using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.Interfaces;
using ValIO.Core.IR;
using ValIO.Core.IR.Enums;
using ValIO.Core.IR.Types;
using ValIO.Generators.Swift.Options;

namespace ValIO.Generators.Swift;

/// <summary>
/// Maps IR types to Swift AST type nodes. Nullability is expressed via
/// <see cref="NullableTypeNode"/> which the renderer emits as <c>T?</c>.
/// </summary>
public sealed class SwiftIrTypeMapper : IIrTypeMapper
{
    private readonly SwiftGenerationOptions _options;

    public SwiftIrTypeMapper(SwiftGenerationOptions? options = null)
    {
        _options = options ?? new SwiftGenerationOptions();
    }

    public TypeNode MapType(IrType irType) => irType switch
    {
        IrPrimitiveType p     => MapPrimitive(p),
        IrArrayType arr       => new ArrayTypeNode(MapType(arr.ElementType)),
        IrDictionaryType dict => new DictionaryTypeNode(MapType(dict.KeyType), MapType(dict.ValueType)),
        IrNullableType n      => new NullableTypeNode(MapType(n.InnerType)),
        IrOptionalType o      => MapType(o.InnerType),
        IrModelReference r    => new SimpleTypeNode(new IdentifierNode(r.ModelName)),
        // Swift has no native union / intersection types — fall back to the configured Any mapping.
        IrUnionType        => AnyType(),
        IrIntersectionType => AnyType(),
        _                  => AnyType(),
    };

    private SimpleTypeNode MapPrimitive(IrPrimitiveType p) => p.PrimitiveKind switch
    {
        IrPrimitiveKind.String    => new SimpleTypeNode(new IdentifierNode("String"), isPrimitive: true),
        IrPrimitiveKind.Integer   => new SimpleTypeNode(new IdentifierNode("Int"),    isPrimitive: true),
        IrPrimitiveKind.Long      => new SimpleTypeNode(new IdentifierNode("Int64"),  isPrimitive: true),
        IrPrimitiveKind.Number    => new SimpleTypeNode(new IdentifierNode("Double"), isPrimitive: true),
        IrPrimitiveKind.Boolean   => new SimpleTypeNode(new IdentifierNode("Bool"),   isPrimitive: true),
        IrPrimitiveKind.Date      => new SimpleTypeNode(new IdentifierNode("Date"),   isPrimitive: true),
        IrPrimitiveKind.DateTime  => new SimpleTypeNode(new IdentifierNode("Date"),   isPrimitive: true),
        IrPrimitiveKind.Time      => new SimpleTypeNode(new IdentifierNode("Date"),   isPrimitive: true),
        IrPrimitiveKind.Uuid      => new SimpleTypeNode(new IdentifierNode("String"), isPrimitive: true),
        IrPrimitiveKind.Binary    => new SimpleTypeNode(new IdentifierNode("Data"),   isPrimitive: true),
        IrPrimitiveKind.ByteArray => new SimpleTypeNode(new IdentifierNode("Data"),   isPrimitive: true),
        IrPrimitiveKind.File      => new SimpleTypeNode(new IdentifierNode("Data"),   isPrimitive: true),
        IrPrimitiveKind.Any       => AnyType(),
        IrPrimitiveKind.Void      => new SimpleTypeNode(new IdentifierNode("Void"),   isPrimitive: true),
        _                         => AnyType(),
    };

    private SimpleTypeNode AnyType() =>
        _options.AnyMapping == SwiftOptionIds.AnyMapping.String
            ? new SimpleTypeNode(new IdentifierNode("String"),     isPrimitive: true)
            : new SimpleTypeNode(new IdentifierNode("AnyCodable"), isPrimitive: true);

    public bool IsOptionalInLanguage(IrProperty property) =>
        property.Type is IrOptionalType || !property.IsRequired;

    /// <summary>
    /// Returns the model/enum name referenced by this IR type (for import collection),
    /// or null when no import is required.
    /// </summary>
    public string? GetImportTarget(IrType irType) => irType switch
    {
        IrModelReference r    => r.ModelName,
        IrArrayType arr       => GetImportTarget(arr.ElementType),
        IrDictionaryType dict => GetImportTarget(dict.ValueType) ?? GetImportTarget(dict.KeyType),
        IrNullableType n      => GetImportTarget(n.InnerType),
        IrOptionalType o      => GetImportTarget(o.InnerType),
        _ => null,
    };
}
