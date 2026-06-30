using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.IR;
using ValIO.Core.IR.Enums;
using ValIO.Core.IR.Types;
using ValIO.Core.Interfaces;

namespace ValIO.Generators.TypeScript;

/// <summary>
/// Maps IR types to TypeScript-specific AST type nodes.
/// </summary>
public sealed class TypeScriptIrTypeMapper : IIrTypeMapper
{
    public TypeNode MapType(IrType irType) => irType switch
    {
        IrPrimitiveType p      => MapPrimitive(p),
        IrArrayType arr        => new GenericTypeNode(new IdentifierNode("Array"), [MapType(arr.ElementType)]),
        IrDictionaryType dict  => new GenericTypeNode(new IdentifierNode("Record"), [MapType(dict.KeyType), MapType(dict.ValueType)]),
        IrNullableType n       => new UnionTypeNode([MapType(n.InnerType), new SimpleTypeNode(new IdentifierNode("null"), isPrimitive: true)]),
        IrOptionalType o       => MapType(o.InnerType), // optional handled via IsOptionalInLanguage
        IrModelReference r     => new SimpleTypeNode(new IdentifierNode(r.ModelName)),
        IrUnionType u          => new UnionTypeNode([.. u.Types.Select(MapType)]),
        IrIntersectionType i   => new IntersectionTypeNode([.. i.Types.Select(MapType)]),
        _                      => new SimpleTypeNode(new IdentifierNode("unknown"), isPrimitive: true)
    };

    private static SimpleTypeNode MapPrimitive(IrPrimitiveType p) => p.PrimitiveKind switch
    {
        IrPrimitiveKind.String   => new SimpleTypeNode(new IdentifierNode("string"), isPrimitive: true),
        IrPrimitiveKind.Integer  => new SimpleTypeNode(new IdentifierNode("number"), isPrimitive: true),
        IrPrimitiveKind.Long     => new SimpleTypeNode(new IdentifierNode("bigint"), isPrimitive: true),
        IrPrimitiveKind.Number   => new SimpleTypeNode(new IdentifierNode("number"), isPrimitive: true),
        IrPrimitiveKind.Boolean  => new SimpleTypeNode(new IdentifierNode("boolean"), isPrimitive: true),
        IrPrimitiveKind.Date     => new SimpleTypeNode(new IdentifierNode("string"), isPrimitive: true),
        IrPrimitiveKind.DateTime => new SimpleTypeNode(new IdentifierNode("string"), isPrimitive: true),
        IrPrimitiveKind.Time     => new SimpleTypeNode(new IdentifierNode("string"), isPrimitive: true),
        IrPrimitiveKind.Uuid     => new SimpleTypeNode(new IdentifierNode("string"), isPrimitive: true),
        IrPrimitiveKind.Binary   => new SimpleTypeNode(new IdentifierNode("Blob"), isPrimitive: true),
        IrPrimitiveKind.ByteArray => new SimpleTypeNode(new IdentifierNode("Blob"), isPrimitive: true),
        IrPrimitiveKind.File     => new SimpleTypeNode(new IdentifierNode("File"), isPrimitive: true),
        IrPrimitiveKind.Any      => new SimpleTypeNode(new IdentifierNode("unknown"), isPrimitive: true),
        IrPrimitiveKind.Void     => new SimpleTypeNode(new IdentifierNode("void"), isPrimitive: true),
        IrPrimitiveKind.Null     => new SimpleTypeNode(new IdentifierNode("null"), isPrimitive: true),
        _                        => new SimpleTypeNode(new IdentifierNode("unknown"), isPrimitive: true)
    };

    public bool IsOptionalInLanguage(IrProperty property) =>
        property.Type is IrOptionalType || !property.IsRequired;

    public string? GetImportTarget(IrType irType) =>
        irType is IrModelReference r ? r.ModelName : null;
}
