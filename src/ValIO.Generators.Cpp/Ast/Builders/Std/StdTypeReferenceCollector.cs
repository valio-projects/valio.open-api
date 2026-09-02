using ValIO.Core.IR;
using ValIO.Core.IR.Types;

namespace ValIO.Generators.Cpp.Ast.Builders.Std;

/// <summary>
/// Сбор ссылок на модели/enum'ы из IrType + карта «affix-имя → OriginalName»
/// (include-пути не должны меняться при affix-преобразованиях).
/// Std-эквивалент приватных хелперов UEModelAstBuilder (UE хранит свои — паритет-замок W1).
/// </summary>
public static class StdTypeReferenceCollector
{
    /// <summary>
    /// Рекурсивно раскладывает ссылки типа по множествам models/enums.
    /// </summary>
    public static void Collect(IrType type, ISet<string> models, ISet<string> enums, IReadOnlySet<string> enumNames)
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
                Collect(array.ElementType, models, enums, enumNames);
                break;
            case IrDictionaryType dict:
                Collect(dict.KeyType, models, enums, enumNames);
                Collect(dict.ValueType, models, enums, enumNames);
                break;
            case IrNullableType nullable:
                Collect(nullable.InnerType, models, enums, enumNames);
                break;
            case IrOptionalType optional:
                Collect(optional.InnerType, models, enums, enumNames);
                break;
            case IrUnionType union:
                foreach (var t in union.Types) Collect(t, models, enums, enumNames);
                break;
            case IrIntersectionType intersection:
                foreach (var t in intersection.Types) Collect(t, models, enums, enumNames);
                break;
        }
    }

    /// <summary>
    /// Карта «affix-имя ссылки → исходное имя» по свойствам и composition модели.
    /// </summary>
    public static Dictionary<string, string> BuildOriginalNameMap(IrModel model, IReadOnlyList<IrProperty> properties)
    {
        var orig = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [model.Name] = model.OriginalName,
        };
        foreach (var property in properties)
            CollectOriginalNames(property.Type, orig);
        if (model.Composition != null)
            foreach (var t in model.Composition.Types)
                CollectOriginalNames(t, orig);
        return orig;
    }

    /// <summary>
    /// Ссылается ли тип (напрямую или через контейнеры) на указанную модель —
    /// проверка рекурсивных свойств.
    /// </summary>
    public static bool ReferencesModel(IrType type, string modelName, IReadOnlySet<string> enumNames)
    {
        switch (type)
        {
            case IrModelReference modelRef:
                return !enumNames.Contains(modelRef.ModelName) && modelRef.ModelName == modelName;
            case IrArrayType array:
                return ReferencesModel(array.ElementType, modelName, enumNames);
            case IrDictionaryType dict:
                return ReferencesModel(dict.KeyType, modelName, enumNames)
                    || ReferencesModel(dict.ValueType, modelName, enumNames);
            case IrNullableType nullable:
                return ReferencesModel(nullable.InnerType, modelName, enumNames);
            case IrOptionalType optional:
                return ReferencesModel(optional.InnerType, modelName, enumNames);
            case IrUnionType union:
                return union.Types.Any(t => ReferencesModel(t, modelName, enumNames));
            case IrIntersectionType intersection:
                return intersection.Types.Any(t => ReferencesModel(t, modelName, enumNames));
            default:
                return false;
        }
    }

    private static void CollectOriginalNames(IrType type, Dictionary<string, string> orig)
    {
        switch (type)
        {
            case IrModelReference modelRef:
                orig[modelRef.ModelName] = modelRef.OriginalModelName;
                break;
            case IrArrayType array:
                CollectOriginalNames(array.ElementType, orig);
                break;
            case IrDictionaryType dict:
                CollectOriginalNames(dict.KeyType, orig);
                CollectOriginalNames(dict.ValueType, orig);
                break;
            case IrNullableType nullable:
                CollectOriginalNames(nullable.InnerType, orig);
                break;
            case IrOptionalType optional:
                CollectOriginalNames(optional.InnerType, orig);
                break;
            case IrUnionType union:
                foreach (var t in union.Types) CollectOriginalNames(t, orig);
                break;
            case IrIntersectionType intersection:
                foreach (var t in intersection.Types) CollectOriginalNames(t, orig);
                break;
        }
    }
}
