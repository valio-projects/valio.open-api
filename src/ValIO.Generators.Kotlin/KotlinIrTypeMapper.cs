using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.Interfaces;
using ValIO.Core.IR;
using ValIO.Core.IR.Enums;
using ValIO.Core.IR.Types;

namespace ValIO.Generators.Kotlin;

/// <summary>
/// Maps IR types to Kotlin AST type nodes. Nullability is expressed via
/// <see cref="NullableTypeNode"/> which the renderer emits as <c>T?</c>.
/// </summary>
public sealed class KotlinIrTypeMapper : IIrTypeMapper
{
    public TypeNode MapType(IrType irType) => irType switch
    {
        IrPrimitiveType p     => MapPrimitive(p),
        IrArrayType arr       => new GenericTypeNode(new IdentifierNode("List"), [MapType(arr.ElementType)]),
        IrDictionaryType dict => new GenericTypeNode(new IdentifierNode("Map"), [MapType(dict.KeyType), MapType(dict.ValueType)]),
        IrNullableType n      => new NullableTypeNode(MapType(n.InnerType)),
        IrOptionalType o      => MapType(o.InnerType),
        IrModelReference r    => new SimpleTypeNode(new IdentifierNode(r.ModelName)),
        // Kotlin has no native union / intersection types — flag as Any (TODO).
        IrUnionType        => new SimpleTypeNode(new IdentifierNode("Any"), isPrimitive: true),
        IrIntersectionType => new SimpleTypeNode(new IdentifierNode("Any"), isPrimitive: true),
        _                  => new SimpleTypeNode(new IdentifierNode("Any"), isPrimitive: true)
    };

    private static SimpleTypeNode MapPrimitive(IrPrimitiveType p) => p.PrimitiveKind switch
    {
        IrPrimitiveKind.String    => new SimpleTypeNode(new IdentifierNode("String"),  isPrimitive: true),
        IrPrimitiveKind.Integer   => new SimpleTypeNode(new IdentifierNode("Int"),     isPrimitive: true),
        IrPrimitiveKind.Long      => new SimpleTypeNode(new IdentifierNode("Long"),    isPrimitive: true),
        IrPrimitiveKind.Number    => new SimpleTypeNode(new IdentifierNode("Double"),  isPrimitive: true),
        IrPrimitiveKind.Boolean   => new SimpleTypeNode(new IdentifierNode("Boolean"), isPrimitive: true),
        IrPrimitiveKind.Date      => new SimpleTypeNode(new IdentifierNode("LocalDate"), isPrimitive: true),
        IrPrimitiveKind.DateTime  => new SimpleTypeNode(new IdentifierNode("Instant"),   isPrimitive: true),
        IrPrimitiveKind.Time      => new SimpleTypeNode(new IdentifierNode("LocalTime"), isPrimitive: true),
        IrPrimitiveKind.Uuid      => new SimpleTypeNode(new IdentifierNode("UUID"),    isPrimitive: true),
        IrPrimitiveKind.Binary    => new SimpleTypeNode(new IdentifierNode("ByteArray"), isPrimitive: true),
        IrPrimitiveKind.ByteArray => new SimpleTypeNode(new IdentifierNode("ByteArray"), isPrimitive: true),
        IrPrimitiveKind.File      => new SimpleTypeNode(new IdentifierNode("File"),    isPrimitive: true),
        IrPrimitiveKind.Any       => new SimpleTypeNode(new IdentifierNode("Any"),     isPrimitive: true),
        IrPrimitiveKind.Void      => new SimpleTypeNode(new IdentifierNode("Unit"),    isPrimitive: true),
        _                         => new SimpleTypeNode(new IdentifierNode("Any"),     isPrimitive: true)
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
