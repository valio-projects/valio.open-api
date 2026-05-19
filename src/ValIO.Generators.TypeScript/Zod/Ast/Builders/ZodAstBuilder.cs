using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.Interfaces;
using ValIO.Core.IR;
using ValIO.Core.IR.Constraints;
using ValIO.Core.IR.Enums;
using ValIO.Core.IR.Types;
using ValIO.Generators.TypeScript.Zod.Ast.Enums;
using ValIO.Generators.TypeScript.Zod.Ast.Nodes;

namespace ValIO.Generators.TypeScript.Zod.Ast.Builders;

/// <summary>
/// Построитель AST для Zod специфичных узлов
/// Использует IR для построения AST узлов
/// </summary>
public class ZodAstBuilder
{
    private readonly INamingConverter _namingConverter;
    private readonly Func<IrType, TypeNode> _typeBuilder;
    private readonly bool _useBigInt;
    
    /// <summary>
    /// Конструктор
    /// </summary>
    /// <param name="namingConverter">Конвертер имен</param>
    /// <param name="typeBuilder">Функция для построения TypeNode из IrType</param>
    /// <param name="useBigInt">Использовать bigint для int64. По умолчанию false — number.</param>
    public ZodAstBuilder(
        INamingConverter namingConverter,
        Func<IrType, TypeNode> typeBuilder,
        bool useBigInt = false)
    {
        _namingConverter = namingConverter ?? throw new ArgumentNullException(nameof(namingConverter));
        _typeBuilder = typeBuilder ?? throw new ArgumentNullException(nameof(typeBuilder));
        _useBigInt = useBigInt;
    }
    
    /// <summary>
    /// Строит ZodSchemaNode из IrModel с учётом oneOf, allOf, anyOf
    /// </summary>
    /// <param name="model">IR модель</param>
    /// <returns>AST узел Zod схемы (object, union или intersection)</returns>
    public ZodSchemaNode BuildZodSchemaFromIrModel(IrModel model)
    {
        if (model.Composition != null && model.Composition.Types.Count > 0)
        {
            switch (model.Composition.Kind)
            {
                case IrCompositionKind.OneOf:
                case IrCompositionKind.AnyOf:
                    var unionVariants = model.Composition.Types
                        .Select(t => BuildZodSchemaFromIrType(t, model.Name))
                        .ToImmutableList();
                    return new ZodUnionNode(unionVariants);
                case IrCompositionKind.AllOf:
                    var allOfSchemas = model.Composition.Types
                        .Select(t => BuildZodSchemaFromIrType(t, model.Name))
                        .ToList();
                    if (model.Properties.Count > 0)
                    {
                        var ownPropsSchema = BuildZodObject(model);
                        allOfSchemas.Add(ownPropsSchema);
                    }
                    if (allOfSchemas.Count == 1)
                        return allOfSchemas[0];
                    return new ZodIntersectionNode(allOfSchemas);
            }
        }

        return BuildZodObject(model);
    }

    /// <summary>
    /// Строит ZodObjectNode из IrModel (только properties, без composition)
    /// </summary>
    /// <param name="model">IR модель</param>
    /// <returns>AST узел Zod object схемы</returns>
    public ZodObjectNode BuildZodObject(IrModel model)
    {
        var constraintsName = $"{model.Name}Constraints";
        var properties = model.Properties
            .Select(p => BuildZodProperty(p, model.Name, constraintsName))
            .ToImmutableList();
        return new ZodObjectNode(properties);
    }

    /// <summary>
    /// Строит ZodObjectNode из списка IrParameter (для route/query params)
    /// </summary>
    public ZodObjectNode BuildZodObjectFromParameters(IReadOnlyList<IrParameter> parameters)
    {
        var properties = parameters
            .Select(p =>
            {
                var unwrap = TryUnwrapIrNullableRoot(p.Type, out var inner);
                var schemaIr = unwrap ? inner! : p.Type;
                return new ZodPropertyNode(
                    name: new IdentifierNode(p.Name),
                    schema: BuildZodSchemaFromIrType(schemaIr),
                    isRequired: p.IsRequired,
                    isNullable: unwrap,
                    isOptional: !p.IsRequired);
            })
            .ToImmutableList();
        return new ZodObjectNode(properties);
    }
    
    /// <summary>
    /// Строит ZodPropertyNode из IrProperty
    /// </summary>
    /// <param name="property">IR свойство</param>
    /// <param name="currentModelName">Имя текущей модели (для constraints ref и self-reference)</param>
    /// <param name="constraintsObjectName">Имя const объекта с метаданными валидации (например MyModelConstraints), null если не генерируем</param>
    /// <returns>AST узел Zod property</returns>
    public ZodPropertyNode BuildZodProperty(IrProperty property, string? currentModelName = null, string? constraintsObjectName = null)
    {
        var name = new IdentifierNode(property.Name);
        var unwrapNullable = TryUnwrapIrNullableRoot(property.Type, out var innerIr);
        var schemaIr = unwrapNullable ? innerIr! : property.Type;
        var schema = BuildZodSchemaFromIrType(schemaIr, currentModelName, property, constraintsObjectName);
        
        return new ZodPropertyNode(
            name: name,
            schema: schema,
            isRequired: property.IsRequired,
            isNullable: unwrapNullable,
            isOptional: !property.IsRequired);
    }
    
    /// <summary>
    /// Строит ZodSchemaNode из IrType
    /// </summary>
    /// <param name="irType">IR тип</param>
    /// <param name="currentModelName">Имя текущей модели</param>
    /// <param name="property">Контекст свойства (для constraints), null для параметров</param>
    /// <param name="constraintsObjectName">Имя const объекта (MyModelConstraints)</param>
    public ZodSchemaNode BuildZodSchemaFromIrType(
        ValIO.Core.IR.Types.IrType irType,
        string? currentModelName = null,
        IrProperty? property = null,
        string? constraintsObjectName = null)
    {
        return irType switch
        {
            IrPrimitiveType p => BuildZodPrimitive(
                p,
                property?.Constraints,
                property != null && !string.IsNullOrEmpty(constraintsObjectName)
                    ? $"{constraintsObjectName}.{property.Name}"
                    : null),
            IrModelReference r => BuildZodModelReference(r, currentModelName),
            IrArrayType arr => BuildZodArray(arr, currentModelName),
            IrDictionaryType dict => BuildZodDictionary(dict),
            IrUnionType union => BuildZodUnion(union, currentModelName, property, constraintsObjectName),
            IrIntersectionType intersection => BuildZodIntersection(intersection, currentModelName, property, constraintsObjectName),
            IrNullableType nullable => BuildZodNullable(nullable, currentModelName, property, constraintsObjectName),
            IrOptionalType optional => BuildZodOptional(optional, currentModelName, property, constraintsObjectName),
            _ => BuildZodAny()
        };
    }
    
    /// <summary>
    /// Строит ZodEnumNode из IrEnum
    /// </summary>
    /// <param name="enumModel">IR enum</param>
    /// <returns>AST узел Zod enum схемы</returns>
    public ZodEnumNode BuildZodEnum(IrEnum enumModel)
    {
        var values = enumModel.Values.Select(v =>
        {
            var name = new IdentifierNode(v.Name);
            var value = v.Value switch
            {
                string str => new LiteralExpressionNode(str, LiteralKind.String),
                int i => new LiteralExpressionNode(i, LiteralKind.Number),
                long l => new LiteralExpressionNode(l, LiteralKind.Number),
                short s => new LiteralExpressionNode(s, LiteralKind.Number),
                byte b => new LiteralExpressionNode(b, LiteralKind.Number),
                _ => new LiteralExpressionNode(v.Value?.ToString() ?? "", LiteralKind.String)
            };
            return new ZodEnumValueNode(name, value);
        }).ToImmutableList();
        
        return new ZodEnumNode(values);
    }
    
    /// <summary>
    /// Строит ZodUnionNode для oneOf/anyOf
    /// </summary>
    public ZodUnionNode BuildZodUnion(IReadOnlyList<ZodSchemaNode> variants)
    {
        return new ZodUnionNode(variants);
    }
    
    /// <summary>
    /// Строит ZodIntersectionNode для allOf
    /// </summary>
    public ZodIntersectionNode BuildZodIntersection(IReadOnlyList<ZodSchemaNode> schemas)
    {
        return new ZodIntersectionNode(schemas);
    }
    
    // Приватные вспомогательные методы для IR типов
    
    /// <summary>
    /// Строит Zod схему из IrPrimitiveType.
    /// Использует ZodExpressionBuilder для AST цепочки вызовов (uuid, datetime, min, max и т.д.).
    /// </summary>
    private ZodSchemaNode BuildZodPrimitive(
        IrPrimitiveType primitiveType,
        IrPropertyConstraints? constraints = null,
        string? constraintsRef = null)
    {
        var kind = primitiveType.PrimitiveKind;
        // Binary/ByteArray/File сериализуются runtime'ом как Blob/File.
        // Используем z.instanceof(Blob|File) — это правильно типизируется
        // (vs. z.any(), который выводится как `any | undefined` и ломает
        // required‑поля).
        if (kind is IrPrimitiveKind.Binary or IrPrimitiveKind.ByteArray)
            return BuildZodInstanceOf("Blob");
        if (kind is IrPrimitiveKind.File)
            return BuildZodInstanceOf("File");

        return ZodExpressionBuilder.BuildPrimitiveExpression(kind, _useBigInt, constraints, constraintsRef);
    }

    private static ZodExpressionSchemaNode BuildZodInstanceOf(string typeName)
    {
        return new ZodExpressionSchemaNode(new CallExpressionNode(
            new MemberAccessExpressionNode(
                new IdentifierExpressionNode(new IdentifierNode("z")),
                new IdentifierNode("instanceof")),
            ImmutableList.Create<ExpressionNode>(new IdentifierExpressionNode(new IdentifierNode(typeName)))));
    }
    
    /// <summary>
    /// Строит Zod схему из IrModelReference.
    /// При self-reference (modelReference.ModelName == currentModelName) использует z.lazy().
    /// </summary>
    private ZodSchemaNode BuildZodModelReference(IrModelReference modelReference, string? currentModelName = null)
    {
        var schemaName = $"{modelReference.ModelName}Schema";
        var isLazy = !string.IsNullOrEmpty(currentModelName)
            && string.Equals(modelReference.ModelName, currentModelName, StringComparison.OrdinalIgnoreCase);
        return new ZodPrimitiveSchemaNode(ZodSchemaKind.Any, schemaReference: schemaName, isLazy: isLazy);
    }
    
    /// <summary>
    /// Строит Zod схему из IrArrayType
    /// </summary>
    private ZodSchemaNode BuildZodArray(IrArrayType arrayType, string? currentModelName = null)
    {
        var elementSchema = BuildZodSchemaFromIrType(arrayType.ElementType, currentModelName, null, null);
        return new ZodArrayNode(elementSchema);
    }
    
    /// <summary>
    /// Строит Zod схему из IrDictionaryType
    /// </summary>
    private ZodSchemaNode BuildZodDictionary(IrDictionaryType dictionaryType)
    {
        _ = BuildZodSchemaFromIrType(dictionaryType.ValueType, null, null, null);
        return BuildZodAny();
    }
    
    /// <summary>
    /// Строит Zod схему из IrUnionType
    /// </summary>
    private ZodSchemaNode BuildZodUnion(
        IrUnionType unionType,
        string? currentModelName = null,
        IrProperty? property = null,
        string? constraintsObjectName = null)
    {
        var variants = unionType.Types
            .Select(t => BuildZodSchemaFromIrType(t, currentModelName, property, constraintsObjectName))
            .ToImmutableList();
        return new ZodUnionNode(variants);
    }
    
    /// <summary>
    /// Строит Zod схему из IrIntersectionType
    /// </summary>
    private ZodSchemaNode BuildZodIntersection(
        IrIntersectionType intersectionType,
        string? currentModelName = null,
        IrProperty? property = null,
        string? constraintsObjectName = null)
    {
        var schemas = intersectionType.Types
            .Select(t => BuildZodSchemaFromIrType(t, currentModelName, property, constraintsObjectName))
            .ToImmutableList();
        return new ZodIntersectionNode(schemas);
    }
    
    /// <summary>
    /// Строит Zod схему из IrNullableType
    /// </summary>
    private ZodSchemaNode BuildZodNullable(
        IrNullableType nullableType,
        string? currentModelName = null,
        IrProperty? property = null,
        string? constraintsObjectName = null)
    {
        var innerSchema = BuildZodSchemaFromIrType(nullableType.InnerType, currentModelName, property, constraintsObjectName);
        return new ZodEffectsNode(innerSchema, ZodEffectKind.Nullable);
    }
    
    /// <summary>
    /// Строит Zod схему из IrOptionalType
    /// </summary>
    private ZodSchemaNode BuildZodOptional(
        IrOptionalType optionalType,
        string? currentModelName = null,
        IrProperty? property = null,
        string? constraintsObjectName = null)
    {
        var innerSchema = BuildZodSchemaFromIrType(optionalType.InnerType, currentModelName, property, constraintsObjectName);
        return new ZodEffectsNode(innerSchema, ZodEffectKind.Optional);
    }
    
    private ZodSchemaNode BuildZodAny()
    {
        return new ZodPrimitiveSchemaNode(ZodSchemaKind.Any);
    }
    
    private static bool TryUnwrapIrNullableRoot(IrType type, out IrType? inner)
    {
        if (type is IrNullableType n)
        {
            inner = n.InnerType;
            return true;
        }
        
        inner = null;
        return false;
    }
}
