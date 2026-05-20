using System.Collections.Immutable;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.Interfaces;
using ValIO.Core.IR;
using ValIO.Core.IR.Enums;
using ValIO.Core.IR.Types;

namespace ValIO.Generators.FSharp;

/// <summary>
/// Maps IR types to F# AST type nodes. Nullability is expressed via
/// <see cref="NullableTypeNode"/> which the renderer emits as <c>T option</c>.
/// Lists are modeled as a <see cref="GenericTypeNode"/> named <c>list</c>
/// (rendered post-fix as <c>T list</c>) and dictionaries as <c>Map&lt;K,V&gt;</c>.
/// </summary>
public sealed class FSharpIrTypeMapper : IIrTypeMapper
{
    public TypeNode MapType(IrType irType) => irType switch
    {
        IrPrimitiveType p     => MapPrimitive(p),
        IrArrayType arr       => new GenericTypeNode(
            new IdentifierNode("list"),
            ImmutableList.Create(MapType(arr.ElementType))),
        IrDictionaryType dict => new GenericTypeNode(
            new IdentifierNode("Map"),
            ImmutableList.Create(MapType(dict.KeyType), MapType(dict.ValueType))),
        IrNullableType n      => new NullableTypeNode(MapType(n.InnerType)),
        IrOptionalType o      => new NullableTypeNode(MapType(o.InnerType)),
        IrModelReference r    => new SimpleTypeNode(new IdentifierNode(r.ModelName)),
        // F# has no native union/intersection — fall back to obj.
        IrUnionType        => ObjType(),
        IrIntersectionType => ObjType(),
        _                  => ObjType(),
    };

    private static SimpleTypeNode MapPrimitive(IrPrimitiveType p) => p.PrimitiveKind switch
    {
        IrPrimitiveKind.String    => new SimpleTypeNode(new IdentifierNode("string"),          isPrimitive: true),
        IrPrimitiveKind.Integer   => new SimpleTypeNode(new IdentifierNode("int"),             isPrimitive: true),
        IrPrimitiveKind.Long      => new SimpleTypeNode(new IdentifierNode("int64"),           isPrimitive: true),
        IrPrimitiveKind.Number    => new SimpleTypeNode(new IdentifierNode("float"),           isPrimitive: true),
        IrPrimitiveKind.Boolean   => new SimpleTypeNode(new IdentifierNode("bool"),            isPrimitive: true),
        IrPrimitiveKind.Date      => new SimpleTypeNode(new IdentifierNode("System.DateTime"), isPrimitive: true),
        IrPrimitiveKind.DateTime  => new SimpleTypeNode(new IdentifierNode("System.DateTime"), isPrimitive: true),
        IrPrimitiveKind.Time      => new SimpleTypeNode(new IdentifierNode("System.TimeSpan"), isPrimitive: true),
        IrPrimitiveKind.Uuid      => new SimpleTypeNode(new IdentifierNode("string"),          isPrimitive: true),
        IrPrimitiveKind.Binary    => new SimpleTypeNode(new IdentifierNode("byte[]"),          isPrimitive: true),
        IrPrimitiveKind.ByteArray => new SimpleTypeNode(new IdentifierNode("byte[]"),          isPrimitive: true),
        IrPrimitiveKind.File      => new SimpleTypeNode(new IdentifierNode("byte[]"),          isPrimitive: true),
        IrPrimitiveKind.Any       => ObjType(),
        IrPrimitiveKind.Void      => new SimpleTypeNode(new IdentifierNode("unit"),            isPrimitive: true),
        _                         => ObjType(),
    };

    private static SimpleTypeNode ObjType() =>
        new(new IdentifierNode("obj"), isPrimitive: true);

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
