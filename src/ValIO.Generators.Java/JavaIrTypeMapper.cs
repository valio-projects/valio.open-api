using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.IR;
using ValIO.Core.IR.Enums;
using ValIO.Core.IR.Types;
using ValIO.Core.Interfaces;

namespace ValIO.Generators.Java;

/// <summary>
/// Maps IR types to Java-21-flavoured AST type nodes.
/// </summary>
public sealed class JavaIrTypeMapper : IIrTypeMapper
{
    /// <summary>
    /// Map IR type to a Java AST type node. By default emits boxed types for primitives so they
    /// can participate in <c>java.util.List&lt;T&gt;</c> / <c>java.util.Map&lt;K,V&gt;</c>; record components
    /// later swap to unboxed primitives for required, non-collection contexts.
    /// </summary>
    public TypeNode MapType(IrType irType) => MapType(irType, preferUnboxed: false);

    /// <summary>
    /// Variant for cases where the caller knows the value is not nullable and would prefer the
    /// unboxed Java primitive (e.g. record component for required int).
    /// </summary>
    public TypeNode MapType(IrType irType, bool preferUnboxed) => irType switch
    {
        IrPrimitiveType p     => MapPrimitive(p, preferUnboxed),
        IrArrayType arr       => new GenericTypeNode(
            new IdentifierNode("List"),
            new[] { MapType(arr.ElementType, preferUnboxed: false) }),
        IrDictionaryType dict => new GenericTypeNode(
            new IdentifierNode("Map"),
            new[]
            {
                MapType(dict.KeyType, preferUnboxed: false),
                MapType(dict.ValueType, preferUnboxed: false),
            }),
        IrNullableType n      => MapType(n.InnerType, preferUnboxed: false),
        IrOptionalType o      => MapType(o.InnerType, preferUnboxed: false),
        IrModelReference r    => new SimpleTypeNode(new IdentifierNode(r.ModelName)),
        IrUnionType u         => new SimpleTypeNode(new IdentifierNode("Object"), isPrimitive: false),
        IrIntersectionType i  => new SimpleTypeNode(new IdentifierNode("Object"), isPrimitive: false),
        _                     => new SimpleTypeNode(new IdentifierNode("Object"), isPrimitive: false),
    };

    private static SimpleTypeNode MapPrimitive(IrPrimitiveType p, bool preferUnboxed)
    {
        return p.PrimitiveKind switch
        {
            IrPrimitiveKind.String    => new SimpleTypeNode(new IdentifierNode("String"), isPrimitive: false),
            IrPrimitiveKind.Integer   => preferUnboxed
                                            ? new SimpleTypeNode(new IdentifierNode("int"), isPrimitive: true)
                                            : new SimpleTypeNode(new IdentifierNode("Integer"), isPrimitive: false),
            IrPrimitiveKind.Long      => preferUnboxed
                                            ? new SimpleTypeNode(new IdentifierNode("long"), isPrimitive: true)
                                            : new SimpleTypeNode(new IdentifierNode("Long"), isPrimitive: false),
            IrPrimitiveKind.Number    => preferUnboxed
                                            ? new SimpleTypeNode(new IdentifierNode("double"), isPrimitive: true)
                                            : new SimpleTypeNode(new IdentifierNode("Double"), isPrimitive: false),
            IrPrimitiveKind.Boolean   => preferUnboxed
                                            ? new SimpleTypeNode(new IdentifierNode("boolean"), isPrimitive: true)
                                            : new SimpleTypeNode(new IdentifierNode("Boolean"), isPrimitive: false),
            IrPrimitiveKind.Date      => new SimpleTypeNode(new IdentifierNode("LocalDate"), isPrimitive: false),
            IrPrimitiveKind.DateTime  => new SimpleTypeNode(new IdentifierNode("OffsetDateTime"), isPrimitive: false),
            IrPrimitiveKind.Time      => new SimpleTypeNode(new IdentifierNode("OffsetTime"), isPrimitive: false),
            IrPrimitiveKind.Uuid      => new SimpleTypeNode(new IdentifierNode("UUID"), isPrimitive: false),
            IrPrimitiveKind.Binary    => new SimpleTypeNode(new IdentifierNode("byte[]"), isPrimitive: true),
            IrPrimitiveKind.ByteArray => new SimpleTypeNode(new IdentifierNode("byte[]"), isPrimitive: true),
            IrPrimitiveKind.File      => new SimpleTypeNode(new IdentifierNode("byte[]"), isPrimitive: true),
            IrPrimitiveKind.Any       => new SimpleTypeNode(new IdentifierNode("Object"), isPrimitive: false),
            IrPrimitiveKind.Void      => new SimpleTypeNode(new IdentifierNode("void"), isPrimitive: true),
            _                         => new SimpleTypeNode(new IdentifierNode("Object"), isPrimitive: false),
        };
    }

    /// <summary>A property is optional in Java when it is wrapped in IrOptionalType or not required.</summary>
    public bool IsOptionalInLanguage(IrProperty property) =>
        property.Type is IrOptionalType || !property.IsRequired;

    /// <summary>
    /// Returns the fully-qualified <c>java.x.Y</c> import target for a type, or <c>null</c> if no
    /// import is required (primitives, model references handled by package layout).
    /// </summary>
    public string? GetImportTarget(IrType irType) => irType switch
    {
        IrPrimitiveType p     => GetPrimitiveImport(p.PrimitiveKind),
        IrArrayType _         => "java.util.List",
        IrDictionaryType _    => "java.util.Map",
        IrNullableType n      => GetImportTarget(n.InnerType),
        IrOptionalType o      => GetImportTarget(o.InnerType),
        IrModelReference _    => null,
        _                     => null,
    };

    /// <summary>
    /// Returns all imports needed for this type (recurses into collections so that, e.g.,
    /// <c>List&lt;OffsetDateTime&gt;</c> imports both <c>List</c> and <c>OffsetDateTime</c>).
    /// </summary>
    public IEnumerable<string> CollectImports(IrType irType)
    {
        switch (irType)
        {
            case IrArrayType arr:
                yield return "java.util.List";
                foreach (var i in CollectImports(arr.ElementType)) yield return i;
                break;
            case IrDictionaryType dict:
                yield return "java.util.Map";
                foreach (var i in CollectImports(dict.KeyType)) yield return i;
                foreach (var i in CollectImports(dict.ValueType)) yield return i;
                break;
            case IrNullableType n:
                foreach (var i in CollectImports(n.InnerType)) yield return i;
                break;
            case IrOptionalType o:
                foreach (var i in CollectImports(o.InnerType)) yield return i;
                break;
            case IrPrimitiveType p:
                var primitive = GetPrimitiveImport(p.PrimitiveKind);
                if (primitive != null) yield return primitive;
                break;
            case IrUnionType u:
                foreach (var t in u.Types)
                    foreach (var i in CollectImports(t)) yield return i;
                break;
            case IrIntersectionType x:
                foreach (var t in x.Types)
                    foreach (var i in CollectImports(t)) yield return i;
                break;
        }
    }

    private static string? GetPrimitiveImport(IrPrimitiveKind kind) => kind switch
    {
        IrPrimitiveKind.Date     => "java.time.LocalDate",
        IrPrimitiveKind.DateTime => "java.time.OffsetDateTime",
        IrPrimitiveKind.Time     => "java.time.OffsetTime",
        IrPrimitiveKind.Uuid     => "java.util.UUID",
        _ => null,
    };
}
