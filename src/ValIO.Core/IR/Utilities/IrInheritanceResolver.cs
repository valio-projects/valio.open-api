using ValIO.Core.IR.Types;

namespace ValIO.Core.IR.Utilities;

/// <summary>
/// Общий (для всех генераторов) резолвер наследования <c>allOf</c>: вычисляет исходные имена свойств,
/// объявленных базовыми классами модели (<see cref="IrComposition"/>, Kind = AllOf), транзитивно по всей
/// цепочке наследования.
///
/// <para>
/// Зачем это нужно и почему единой точкой. <see cref="IrModel.Properties"/> по контракту содержит только
/// СОБСТВЕННЫЕ свойства (inline-члены allOf), а базовые классы лежат в <see cref="IrComposition.Types"/>
/// (это позволяет 9 из 12 генераторов просто «расплющивать» модель). Но собственный inline-член allOf может
/// ПЕРЕОБЪЯВИТЬ свойство базы — типичный паттерн дискриминированных объединений: каждый подтип переобъявляет
/// поле-дискриминатор (напр. <c>kind</c>), нередко делая его <c>nullable</c>/optional. Генераторы, которые
/// реально наследуют базу через <c>extends</c>/базовый класс (TypeScript, C#, C++), не должны переобъявлять
/// такие свойства: иначе получается несовместимое перекрытие базы — «incorrectly extends interface» в TS
/// (TS2430), скрытие/дубль члена в C#/C++. Генераторы, которые НЕ наследуют, а расплющивают
/// (Java, Kotlin, Python, PHP, Swift, Rust, Ruby, F#, Dart), это множество не используют — их вывод не
/// меняется. Логика вычисления наследуемых имён едина здесь, а применяется в каждом inheritance-генераторе.
/// </para>
/// </summary>
public static class IrInheritanceResolver
{
    private static readonly IReadOnlySet<string> Empty = new HashSet<string>();

    /// <summary>
    /// Возвращает <see cref="IrProperty.OriginalName"/> всех свойств, наследуемых <paramref name="model"/>
    /// от базовых классов его <see cref="IrComposition"/> (Kind = AllOf), транзитивно. Пустое множество —
    /// если наследования нет.
    /// </summary>
    /// <param name="model">Модель, для которой считаем наследуемые свойства.</param>
    /// <param name="modelsByName">Справочник моделей документа по <see cref="IrModel.Name"/>.</param>
    public static IReadOnlySet<string> GetInheritedPropertyNames(
        IrModel model,
        IReadOnlyDictionary<string, IrModel> modelsByName)
    {
        if (model.Composition is not { Kind: IrCompositionKind.AllOf })
            return Empty;

        var names = new HashSet<string>(StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        CollectFromBases(model, modelsByName, names, visited);
        return names;
    }

    /// <summary>
    /// Отфильтровывает из <paramref name="ownProperties"/> те, чьи имена наследуются от базовых классов —
    /// удобный шорткат для inheritance-генераторов при формировании членов, объявляемых рядом с
    /// <c>extends</c>/базовым классом.
    /// </summary>
    public static IReadOnlyList<IrProperty> ExcludeInheritedProperties(
        IrModel model,
        IReadOnlyList<IrProperty> ownProperties,
        IReadOnlyDictionary<string, IrModel> modelsByName)
    {
        var inherited = GetInheritedPropertyNames(model, modelsByName);
        if (inherited.Count == 0)
            return ownProperties;

        var result = new List<IrProperty>(ownProperties.Count);
        foreach (var property in ownProperties)
        {
            if (!inherited.Contains(property.OriginalName))
                result.Add(property);
        }

        return result;
    }

    private static void CollectFromBases(
        IrModel model,
        IReadOnlyDictionary<string, IrModel> modelsByName,
        HashSet<string> names,
        HashSet<string> visited)
    {
        if (model.Composition is not { Kind: IrCompositionKind.AllOf } composition)
            return;

        foreach (var type in composition.Types)
        {
            if (type is not IrModelReference reference)
                continue;
            if (!visited.Add(reference.ModelName))
                continue;
            if (!modelsByName.TryGetValue(reference.ModelName, out var baseModel))
                continue;

            foreach (var property in baseModel.Properties)
                names.Add(property.OriginalName);

            // База сама может наследовать — идём вглубь.
            CollectFromBases(baseModel, modelsByName, names, visited);
        }
    }
}
