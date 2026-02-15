using System.Collections.Immutable;
using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.IR;
using ValIO.Core.IR.Types;
using ValIO.Generators.Cpp.Ast.Nodes;
using ValIO.Generators.Cpp.Ast.Nodes.UE;
using ValIO.Generators.Cpp.Ast.Nodes.UE.Specifiers;
using ValIO.Generators.Cpp.Models;
using ValIO.Generators.Cpp.Services;

namespace ValIO.Generators.Cpp.Ast.Builders;

/// <summary>
/// Строитель AST для UE модели (USTRUCT) из IR представления.
/// Генерирует CppHeaderFileNode, содержащий один UStructDeclarationNode.
/// </summary>
public sealed class UEModelAstBuilder
{
    private readonly UETypeMapper _typeMapper;
    private readonly UENamingConvention _naming;
    private readonly CppGenerationOptions _options;

    public UEModelAstBuilder(UETypeMapper typeMapper, UENamingConvention naming, CppGenerationOptions options)
    {
        _typeMapper = typeMapper ?? throw new ArgumentNullException(nameof(typeMapper));
        _naming = naming ?? throw new ArgumentNullException(nameof(naming));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Строит CppHeaderFileNode из IrModel
    /// </summary>
    /// <param name="modelLookup">Словарь моделей по имени (для раскрытия allOf)</param>
    /// <param name="warnings">Коллекция для записи предупреждений (пропуск рекурсивных свойств)</param>
    public CppHeaderFileNode Build(IrModel model, IReadOnlySet<string> enumNames, IReadOnlyDictionary<string, IrModel> modelLookup, ICollection<string>? warnings = null)
    {
        var structName = _naming.StructName(model.Name);
        var fileName = _naming.ModelFileName(model.Name);

        var category = _naming.ModelTypeCategory(_options.BlueprintCategory);
        var effectiveProperties = GetEffectiveProperties(model, modelLookup);
        var fields = new List<UPropertyFieldNode>();
        var skippedProperties = new List<SkippedPropertyComment>();
        foreach (var property in effectiveProperties)
        {
            if (TypeReferencesModel(property.Type, model.Name, enumNames))
            {
                var typeNode = _typeMapper.MapType(property.Type);
                var propName = _naming.PropertyName(property.Name);
                var reason = "Unreal Engine does not support recursive types";
                skippedProperties.Add(new SkippedPropertyComment(propName, typeNode, reason));
                warnings?.Add($"Model {structName}: skipping recursive property '{property.Name}' ({reason}).");
                continue;
            }
            fields.Add(BuildField(property, category));
        }
        var structNode = new UStructDeclarationNode(
            name: new IdentifierNode(structName),
            specifiers: new UStructSpecifiers { BlueprintType = true, Category = category },
            fields: fields,
            skippedProperties: skippedProperties,
            documentation: BuildDocumentation(model.Description));

        var includes = CollectIncludes(model, fileName, enumNames, effectiveProperties);

        return new CppHeaderFileNode(
            fileName: fileName,
            headerComment: BuildCopyrightComment(),
            includes: includes,
            declarations: new AstNode[] { structNode });
    }

    /// <summary>
    /// Возвращает эффективный список свойств модели с раскрытием allOf.
    /// Для UE USTRUCT (без наследования) все свойства из базовых схем allOf объединяются в один список.
    /// </summary>
    private static IReadOnlyList<IrProperty> GetEffectiveProperties(IrModel model, IReadOnlyDictionary<string, IrModel> modelLookup)
    {
        if (model.Composition?.Kind != IrCompositionKind.AllOf || model.Composition.Types.Count == 0)
            return model.Properties;

        var byName = new Dictionary<string, IrProperty>(StringComparer.Ordinal);
        var order = new List<string>();

        foreach (var compType in model.Composition.Types)
        {
            if (compType is IrModelReference modelRef && modelLookup.TryGetValue(modelRef.ModelName, out var baseModel))
            {
                var baseProps = GetEffectiveProperties(baseModel, modelLookup);
                foreach (var p in baseProps)
                {
                    if (!byName.ContainsKey(p.Name))
                        order.Add(p.Name);
                    byName[p.Name] = p;
                }
            }
        }

        foreach (var p in model.Properties)
        {
            if (!byName.ContainsKey(p.Name))
                order.Add(p.Name);
            byName[p.Name] = p;
        }

        return order.Select(n => byName[n]).ToList();
    }

    /// <summary>
    /// Строит UPropertyFieldNode из IrProperty
    /// </summary>
    private UPropertyFieldNode BuildField(IrProperty property, string category)
    {
        var type = _typeMapper.MapType(property.Type);
        var defaultValueStr = _typeMapper.GetDefaultValue(property.Type);
        ExpressionNode? defaultValue = defaultValueStr != null
            ? new LiteralExpressionNode(defaultValueStr, LiteralKind.String)
            : null;

        return new UPropertyFieldNode(
            name: new IdentifierNode(_naming.PropertyName(property.Name)),
            type: type,
            specifiers: UPropertySpecifiers.ModelField(category),
            defaultValue: defaultValue,
            documentation: BuildDocumentation(property.Description));
    }

    /// <summary>
    /// Собирает необходимые #include директивы для модели
    /// </summary>
    private List<CppIncludeDirectiveNode> CollectIncludes(IrModel model, string fileName, IReadOnlySet<string> enumNames, IReadOnlyList<IrProperty> effectiveProperties)
    {
        var includes = new List<CppIncludeDirectiveNode> { new("CoreMinimal.h") };

        var referencedModels = new HashSet<string>();
        var referencedEnums = new HashSet<string>();
        foreach (var property in effectiveProperties)
        {
            if (TypeReferencesModel(property.Type, model.Name, enumNames))
                continue; // Skip recursive — already excluded from fields
            CollectTypeReferences(property.Type, referencedModels, referencedEnums, enumNames);
        }

        foreach (var refModel in referencedModels.Where(m => m != model.Name).OrderBy(x => x))
            includes.Add(new CppIncludeDirectiveNode(_naming.ModelFileName(refModel)));

        foreach (var refEnum in referencedEnums.OrderBy(x => x))
            includes.Add(new CppIncludeDirectiveNode($"../{_naming.EnumIncludePath(refEnum)}"));

        // .generated.h должен быть всегда последним (требование UHT)
        includes.Add(new CppIncludeDirectiveNode(fileName.Replace(".h", _options.GeneratedHeaderSuffix)));

        return includes;
    }

    /// <summary>
    /// Проверяет, содержит ли тип ссылку на указанную модель (прямо или через TArray, TMap и т.д.).
    /// UE не поддерживает рекурсивные типы.
    /// </summary>
    private static bool TypeReferencesModel(IrType type, string modelName, IReadOnlySet<string> enumNames)
    {
        switch (type)
        {
            case IrModelReference modelRef:
                return !enumNames.Contains(modelRef.ModelName) && modelRef.ModelName == modelName;
            case IrArrayType array:
                return TypeReferencesModel(array.ElementType, modelName, enumNames);
            case IrDictionaryType dict:
                return TypeReferencesModel(dict.KeyType, modelName, enumNames)
                    || TypeReferencesModel(dict.ValueType, modelName, enumNames);
            case IrNullableType nullable:
                return TypeReferencesModel(nullable.InnerType, modelName, enumNames);
            case IrOptionalType optional:
                return TypeReferencesModel(optional.InnerType, modelName, enumNames);
            case IrUnionType union:
                return union.Types.Any(t => TypeReferencesModel(t, modelName, enumNames));
            case IrIntersectionType intersection:
                return intersection.Types.Any(t => TypeReferencesModel(t, modelName, enumNames));
            default:
                return false;
        }
    }

    /// <summary>
    /// Рекурсивно собирает ссылки на модели и enum'ы из типа
    /// </summary>
    private static void CollectTypeReferences(IrType type, HashSet<string> models, HashSet<string> enums, IReadOnlySet<string> enumNames)
    {
        switch (type)
        {
            case IrModelReference modelRef:
                if (enumNames.Contains(modelRef.ModelName))
                    enums.Add(modelRef.ModelName);
                else
                    models.Add(modelRef.ModelName);
                break;
            case IrArrayType array:
                CollectTypeReferences(array.ElementType, models, enums, enumNames);
                break;
            case IrDictionaryType dict:
                CollectTypeReferences(dict.KeyType, models, enums, enumNames);
                CollectTypeReferences(dict.ValueType, models, enums, enumNames);
                break;
            case IrNullableType nullable:
                CollectTypeReferences(nullable.InnerType, models, enums, enumNames);
                break;
            case IrOptionalType optional:
                CollectTypeReferences(optional.InnerType, models, enums, enumNames);
                break;
            case IrUnionType union:
                foreach (var t in union.Types) CollectTypeReferences(t, models, enums, enumNames);
                break;
            case IrIntersectionType intersection:
                foreach (var t in intersection.Types) CollectTypeReferences(t, models, enums, enumNames);
                break;
        }
    }

    private CommentNode? BuildDocumentation(string? description)
    {
        return !string.IsNullOrWhiteSpace(description)
            ? new CommentNode(description, CommentKind.MultiLine)
            : null;
    }

    private CommentNode? BuildCopyrightComment()
    {
        return _options.Copyright != null
            ? new CommentNode(_options.Copyright, CommentKind.MultiLine)
            : null;
    }
}
