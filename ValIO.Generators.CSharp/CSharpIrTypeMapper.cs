using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.IR;
using ValIO.Core.IR.Enums;
using ValIO.Core.IR.Types;
using ValIO.Core.Interfaces;

namespace ValIO.Generators.CSharp;

/// <summary>
/// Maps IR types to C#-specific AST type nodes.
/// </summary>
public sealed class CSharpIrTypeMapper : IIrTypeMapper
{
    public TypeNode MapType(IrType irType) => irType switch
    {
        IrPrimitiveType p      => MapPrimitive(p),
        IrArrayType arr        => new GenericTypeNode(new IdentifierNode("List"), [MapType(arr.ElementType)]),
        IrDictionaryType dict  => new GenericTypeNode(new IdentifierNode("Dictionary"), [MapType(dict.KeyType), MapType(dict.ValueType)]),
        IrNullableType n       => new NullableTypeNode(MapType(n.InnerType)),
        IrOptionalType o       => MapType(o.InnerType),
        IrModelReference r     => new SimpleTypeNode(new IdentifierNode(r.ModelName)),
        IrUnionType _          => new SimpleTypeNode(new IdentifierNode("object")),
        IrIntersectionType _   => new SimpleTypeNode(new IdentifierNode("object")),
        _                      => new SimpleTypeNode(new IdentifierNode("object"))
    };

    private static SimpleTypeNode MapPrimitive(IrPrimitiveType p) => p.PrimitiveKind switch
    {
        IrPrimitiveKind.String   => new SimpleTypeNode(new IdentifierNode("string"), isPrimitive: true),
        IrPrimitiveKind.Integer  => new SimpleTypeNode(new IdentifierNode("int"), isPrimitive: true),
        IrPrimitiveKind.Long     => new SimpleTypeNode(new IdentifierNode("long"), isPrimitive: true),
        IrPrimitiveKind.Number   => new SimpleTypeNode(new IdentifierNode("double"), isPrimitive: true),
        IrPrimitiveKind.Boolean  => new SimpleTypeNode(new IdentifierNode("bool"), isPrimitive: true),
        IrPrimitiveKind.Date     => new SimpleTypeNode(new IdentifierNode("DateTimeOffset"), isPrimitive: true),
        IrPrimitiveKind.DateTime => new SimpleTypeNode(new IdentifierNode("DateTimeOffset"), isPrimitive: true),
        IrPrimitiveKind.Time     => new SimpleTypeNode(new IdentifierNode("TimeSpan"), isPrimitive: true),
        IrPrimitiveKind.Uuid     => new SimpleTypeNode(new IdentifierNode("Guid"), isPrimitive: true),
        IrPrimitiveKind.Binary   => new SimpleTypeNode(new IdentifierNode("Stream"), isPrimitive: true),
        IrPrimitiveKind.ByteArray => new SimpleTypeNode(new IdentifierNode("byte[]"), isPrimitive: true),
        IrPrimitiveKind.File     => new SimpleTypeNode(new IdentifierNode("Stream"), isPrimitive: true),
        IrPrimitiveKind.Any      => new SimpleTypeNode(new IdentifierNode("object")),
        IrPrimitiveKind.Void     => new SimpleTypeNode(new IdentifierNode("void"), isPrimitive: true),
        _                        => new SimpleTypeNode(new IdentifierNode("object"))
    };

    public bool IsOptionalInLanguage(IrProperty property) =>
        property.Type is IrOptionalType || !property.IsRequired;

    public string? GetImportTarget(IrType irType) => null; // C# uses namespaces, not imports
}
