using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.Interfaces;
using ValIO.Core.IR;
using ValIO.Core.IR.Enums;
using ValIO.Core.IR.Types;
using ValIO.Generators.Dart.Options;

namespace ValIO.Generators.Dart;

/// <summary>
/// Maps IR types to Dart AST type nodes.
/// Nullability is expressed via <see cref="NullableTypeNode"/> which the
/// renderer emits as <c>T?</c>.
/// </summary>
public sealed class DartIrTypeMapper : IIrTypeMapper
{
    private readonly DartGenerationOptions _options;

    public DartIrTypeMapper(DartGenerationOptions? options = null)
    {
        _options = options ?? new DartGenerationOptions();
    }

    public TypeNode MapType(IrType irType) => irType switch
    {
        IrPrimitiveType p     => MapPrimitive(p),
        IrArrayType arr       => new GenericTypeNode(
            new IdentifierNode("List"),
            new TypeNode[] { MapType(arr.ElementType) }),
        IrDictionaryType dict => new GenericTypeNode(
            new IdentifierNode("Map"),
            new TypeNode[] { MapType(dict.KeyType), MapType(dict.ValueType) }),
        IrNullableType n      => new NullableTypeNode(MapType(n.InnerType)),
        IrOptionalType o      => MapType(o.InnerType),
        IrModelReference r    => new SimpleTypeNode(new IdentifierNode(r.ModelName)),
        IrUnionType        => AnyType(),
        IrIntersectionType => AnyType(),
        _                  => AnyType(),
    };

    private SimpleTypeNode MapPrimitive(IrPrimitiveType p) => p.PrimitiveKind switch
    {
        IrPrimitiveKind.String    => new SimpleTypeNode(new IdentifierNode("String"),    isPrimitive: true),
        IrPrimitiveKind.Integer   => new SimpleTypeNode(new IdentifierNode("int"),       isPrimitive: true),
        IrPrimitiveKind.Long      => new SimpleTypeNode(new IdentifierNode("int"),       isPrimitive: true),
        IrPrimitiveKind.Number    => new SimpleTypeNode(new IdentifierNode("double"),    isPrimitive: true),
        IrPrimitiveKind.Boolean   => new SimpleTypeNode(new IdentifierNode("bool"),      isPrimitive: true),
        IrPrimitiveKind.Date      => new SimpleTypeNode(new IdentifierNode("DateTime"),  isPrimitive: true),
        IrPrimitiveKind.DateTime  => new SimpleTypeNode(new IdentifierNode("DateTime"),  isPrimitive: true),
        IrPrimitiveKind.Time      => new SimpleTypeNode(new IdentifierNode("DateTime"),  isPrimitive: true),
        IrPrimitiveKind.Uuid      => new SimpleTypeNode(new IdentifierNode("String"),    isPrimitive: true),
        IrPrimitiveKind.Binary    => new SimpleTypeNode(new IdentifierNode("Uint8List"), isPrimitive: true),
        IrPrimitiveKind.ByteArray => new SimpleTypeNode(new IdentifierNode("Uint8List"), isPrimitive: true),
        IrPrimitiveKind.File      => new SimpleTypeNode(new IdentifierNode("Uint8List"), isPrimitive: true),
        IrPrimitiveKind.Any       => AnyType(),
        IrPrimitiveKind.Void      => new SimpleTypeNode(new IdentifierNode("void"),      isPrimitive: true),
        _                         => AnyType(),
    };

    private SimpleTypeNode AnyType() => _options.AnyMapping switch
    {
        DartOptionIds.AnyMapping.String => new SimpleTypeNode(new IdentifierNode("String"), isPrimitive: true),
        DartOptionIds.AnyMapping.Object => new SimpleTypeNode(new IdentifierNode("Object"), isPrimitive: true),
        _                               => new SimpleTypeNode(new IdentifierNode("dynamic"), isPrimitive: true),
    };

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
