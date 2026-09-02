using ValIO.Core.IR.Types;

namespace ValIO.Core.IR.Utilities;

/// <summary>
/// План отображения allOf-композиции модели в целевой язык:
/// одиночный $ref-базис → наследование (только собственные свойства),
/// иначе — flatten (объединённые свойства всех баз + собственные).
/// Единая реализация для всех генераторов (раньше дублировалась в UEModelAstBuilder).
/// </summary>
public sealed record IrInheritancePlan
{
    /// <summary>
    /// Имя базовой модели (sanitized) для наследования; null — flatten без наследования.
    /// </summary>
    public string? BaseModelName { get; }

    /// <summary>
    /// Свойства, которые должен объявить генератор: при наследовании — только собственные,
    /// при flatten — объединённые (базы первыми, порядок стабилен, дубликаты по имени
    /// разрешаются в пользу последнего объявления).
    /// </summary>
    public IReadOnlyList<IrProperty> EffectiveProperties { get; }

    public bool UseInheritance => BaseModelName != null;

    private IrInheritancePlan(string? baseModelName, IReadOnlyList<IrProperty> effectiveProperties)
    {
        BaseModelName = baseModelName;
        EffectiveProperties = effectiveProperties;
    }

    public static IrInheritancePlan Resolve(IrModel model, IReadOnlyDictionary<string, IrModel> modelLookup)
    {
        if (model.Composition?.Kind != IR.IrCompositionKind.AllOf || model.Composition.Types.Count == 0)
            return new IrInheritancePlan(null, model.Properties);

        // Наследование возможно только при ровно одном IrModelReference-базисе.
        var refBases = model.Composition.Types.OfType<IrModelReference>().ToList();
        if (refBases.Count == 1 && model.Composition.Types.Count == 1 &&
            modelLookup.TryGetValue(refBases[0].ModelName, out var baseModel))
        {
            var ownProperties = IrInheritanceResolver.ExcludeInheritedProperties(model, model.Properties, modelLookup);
            return new IrInheritancePlan(baseModel.Name, ownProperties);
        }

        return new IrInheritancePlan(null, Flatten(model, modelLookup));
    }

    /// <summary>
    /// Объединяет свойства всех $ref-базисов (рекурсивно) с собственными.
    /// </summary>
    private static IReadOnlyList<IrProperty> Flatten(IrModel model, IReadOnlyDictionary<string, IrModel> modelLookup)
    {
        var byName = new Dictionary<string, IrProperty>(StringComparer.Ordinal);
        var order = new List<string>();

        foreach (var compType in model.Composition!.Types)
        {
            if (compType is IrModelReference modelRef && modelLookup.TryGetValue(modelRef.ModelName, out var baseModel))
            {
                foreach (var p in Flatten(baseModel, modelLookup))
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
}
