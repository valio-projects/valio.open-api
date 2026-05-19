using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.IR;
using ValIO.Core.IR.Enums;
using ValIO.Core.IR.Types;
using ValIO.Core.Interfaces;

namespace ValIO.Generators.Php;

/// <summary>
/// Maps IR types to PHP 8.2-flavoured AST type nodes.
/// </summary>
public sealed class PhpIrTypeMapper : IIrTypeMapper
{
    public TypeNode MapType(IrType irType) => irType switch
    {
        IrPrimitiveType p     => MapPrimitive(p),
        IrArrayType _         => new SimpleTypeNode(new IdentifierNode("array"), isPrimitive: true),
        IrDictionaryType _    => new SimpleTypeNode(new IdentifierNode("array"), isPrimitive: true),
        IrNullableType n      => new NullableTypeNode(MapType(n.InnerType)),
        IrOptionalType o      => MapType(o.InnerType),
        IrModelReference r    => new SimpleTypeNode(new IdentifierNode(r.ModelName)),
        IrUnionType u         => new UnionTypeNode([.. u.Types.Select(MapType)]),
        IrIntersectionType i  => new IntersectionTypeNode([.. i.Types.Select(MapType)]),
        _                     => new SimpleTypeNode(new IdentifierNode("mixed"), isPrimitive: true),
    };

    private static SimpleTypeNode MapPrimitive(IrPrimitiveType p) => p.PrimitiveKind switch
    {
        IrPrimitiveKind.String    => new SimpleTypeNode(new IdentifierNode("string"), isPrimitive: true),
        IrPrimitiveKind.Integer   => new SimpleTypeNode(new IdentifierNode("int"), isPrimitive: true),
        IrPrimitiveKind.Long      => new SimpleTypeNode(new IdentifierNode("int"), isPrimitive: true),
        IrPrimitiveKind.Number    => new SimpleTypeNode(new IdentifierNode("float"), isPrimitive: true),
        IrPrimitiveKind.Boolean   => new SimpleTypeNode(new IdentifierNode("bool"), isPrimitive: true),
        IrPrimitiveKind.Date      => new SimpleTypeNode(new IdentifierNode("\\DateTimeImmutable"), isPrimitive: false),
        IrPrimitiveKind.DateTime  => new SimpleTypeNode(new IdentifierNode("\\DateTimeImmutable"), isPrimitive: false),
        IrPrimitiveKind.Time      => new SimpleTypeNode(new IdentifierNode("\\DateTimeImmutable"), isPrimitive: false),
        IrPrimitiveKind.Uuid      => new SimpleTypeNode(new IdentifierNode("string"), isPrimitive: true),
        IrPrimitiveKind.Binary    => new SimpleTypeNode(new IdentifierNode("\\SplFileInfo"), isPrimitive: false),
        IrPrimitiveKind.ByteArray => new SimpleTypeNode(new IdentifierNode("\\SplFileInfo"), isPrimitive: false),
        IrPrimitiveKind.File      => new SimpleTypeNode(new IdentifierNode("\\SplFileInfo"), isPrimitive: false),
        IrPrimitiveKind.Any       => new SimpleTypeNode(new IdentifierNode("mixed"), isPrimitive: true),
        IrPrimitiveKind.Void      => new SimpleTypeNode(new IdentifierNode("void"), isPrimitive: true),
        _                         => new SimpleTypeNode(new IdentifierNode("mixed"), isPrimitive: true),
    };

    public bool IsOptionalInLanguage(IrProperty property) =>
        property.Type is IrOptionalType || !property.IsRequired;

    public string? GetImportTarget(IrType irType) =>
        irType is IrModelReference r ? r.ModelName : null;

    /// <summary>
    /// Returns a PHPDoc fragment for an array/dict IR type, or null if there is none to add.
    /// </summary>
    public string? GetPhpDocArrayType(IrType irType) => irType switch
    {
        IrArrayType arr        => $"array<{GetPhpDocType(arr.ElementType)}>",
        IrDictionaryType dict  => $"array<{GetPhpDocType(dict.KeyType)}, {GetPhpDocType(dict.ValueType)}>",
        IrNullableType n       => GetPhpDocArrayType(n.InnerType),
        IrOptionalType o       => GetPhpDocArrayType(o.InnerType),
        _ => null,
    };

    private string GetPhpDocType(IrType irType) => irType switch
    {
        IrPrimitiveType p      => GetPhpDocPrimitive(p.PrimitiveKind),
        IrArrayType arr        => $"array<{GetPhpDocType(arr.ElementType)}>",
        IrDictionaryType dict  => $"array<{GetPhpDocType(dict.KeyType)}, {GetPhpDocType(dict.ValueType)}>",
        IrNullableType n       => $"{GetPhpDocType(n.InnerType)}|null",
        IrOptionalType o       => GetPhpDocType(o.InnerType),
        IrModelReference r     => r.ModelName,
        IrUnionType u          => string.Join("|", u.Types.Select(GetPhpDocType)),
        IrIntersectionType i   => string.Join("&", i.Types.Select(GetPhpDocType)),
        _ => "mixed",
    };

    private static string GetPhpDocPrimitive(IrPrimitiveKind kind) => kind switch
    {
        IrPrimitiveKind.String                                          => "string",
        IrPrimitiveKind.Integer or IrPrimitiveKind.Long                 => "int",
        IrPrimitiveKind.Number                                          => "float",
        IrPrimitiveKind.Boolean                                         => "bool",
        IrPrimitiveKind.Date or IrPrimitiveKind.DateTime or IrPrimitiveKind.Time
                                                                        => "\\DateTimeImmutable",
        IrPrimitiveKind.Uuid                                            => "string",
        IrPrimitiveKind.Binary or IrPrimitiveKind.ByteArray or IrPrimitiveKind.File
                                                                        => "\\SplFileInfo",
        IrPrimitiveKind.Any                                             => "mixed",
        IrPrimitiveKind.Void                                            => "void",
        _ => "mixed",
    };
}
