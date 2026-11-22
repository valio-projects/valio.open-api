using ValIO.Core.Interfaces;
using ValIO.Core.IR.Types;

namespace ValIO.Core.IR.Builders;

/// <summary>
/// Чистое (без побочных эффектов) преобразование IR, добавляющее настраиваемые
/// prefix/suffix к именам моделей, enum'ов и операций.
///
/// Применяется ОДИН раз сразу после извлечения моделей/enum'ов/клиентов и ДО построения
/// графа зависимостей. Поскольку все генераторы читают имена исключительно из IR
/// (<c>IrModel.Name</c>, <c>IrEnum.Name</c>, <c>IrOperation.MethodName</c>,
/// <c>IrModelReference.ModelName</c>), переименование на уровне IR синхронно отражается
/// на определениях, ссылках, схема-константах, импортах-идентификаторах и графе зависимостей
/// во всех языках/транспортах.
///
/// Исходные имена сохраняются в <c>Original*</c>-полях — их используют генераторы для имён
/// файлов и путей импорта, которые меняться не должны.
/// </summary>
public static class IrNameAffixer
{
    /// <summary>
    /// Применяет affix к моделям, enum'ам и операциям клиентов. Возвращает новые коллекции
    /// (входные не мутируются). Ссылки <see cref="IrModelReference"/> переписываются согласно
    /// картам переименования: ссылка на модель получает model-affix, ссылка на enum — enum-affix.
    /// </summary>
    public static (IReadOnlyList<IrModel> Models, IReadOnlyList<IrEnum> Enums, IReadOnlyList<IrClient> Clients) Apply(
        IReadOnlyList<IrModel> models,
        IReadOnlyList<IrEnum> enums,
        IReadOnlyList<IrClient> clients,
        NameAffixOptions options,
        INamingConverter namingConverter)
    {
        ArgumentNullException.ThrowIfNull(models);
        ArgumentNullException.ThrowIfNull(enums);
        ArgumentNullException.ThrowIfNull(clients);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(namingConverter);

        if (options.IsNoOp)
            return (models, enums, clients);

        // Карта: исходное (до affix) имя -> имя с affix. Пространства имён моделей и enum'ов
        // не пересекаются (разные схемы), поэтому одной карты достаточно — каждая запись несёт
        // правильный affix своего вида.
        var rename = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var m in models)
            rename[m.Name] = Affix(namingConverter, options.ModelPrefix, m.Name, options.ModelSuffix);
        foreach (var e in enums)
            rename[e.Name] = Affix(namingConverter, options.EnumPrefix, e.Name, options.EnumSuffix);

        var newModels = new List<IrModel>(models.Count);
        foreach (var m in models)
        {
            newModels.Add(new IrModel(m)
            {
                Name = rename[m.Name],
                OriginalName = m.Name,
                Properties = m.Properties
                    .Select(p => new IrProperty(p) { Type = RewriteType(p.Type, rename) })
                    .ToList(),
                Composition = RewriteComposition(m.Composition, rename),
            });
        }

        var newEnums = new List<IrEnum>(enums.Count);
        foreach (var e in enums)
        {
            newEnums.Add(new IrEnum(e)
            {
                Name = rename[e.Name],
                OriginalName = e.Name,
            });
        }

        var newClients = new List<IrClient>(clients.Count);
        foreach (var c in clients)
        {
            // Имя клиента (группы/тега) не входит в model/method/enum-affix и не меняется.
            newClients.Add(new IrClient(c)
            {
                Operations = c.Operations
                    .Select(op => RewriteOperation(op, options, namingConverter, rename))
                    .ToList(),
            });
        }

        return (newModels, newEnums, newClients);
    }

    /// <summary>Renames an operation and all model references, including alternate response representations.</summary>
    /// <param name="op">The original operation.</param>
    /// <param name="options">The configured naming affixes.</param>
    /// <param name="namingConverter">The identifier naming policy.</param>
    /// <param name="rename">The map from original type names to affixed names.</param>
    /// <returns>The operation with renamed references and original wire metadata.</returns>
    private static IrOperation RewriteOperation(
        IrOperation op,
        NameAffixOptions options,
        INamingConverter namingConverter,
        IReadOnlyDictionary<string, string> rename)
    {
        return new IrOperation(op)
        {
            MethodName = Affix(namingConverter, options.MethodPrefix, op.MethodName, options.MethodSuffix),
            OriginalMethodName = op.MethodName,
            PathParameters = op.PathParameters.Select(p => RewriteParameter(p, rename)).ToList(),
            QueryParameters = op.QueryParameters.Select(p => RewriteParameter(p, rename)).ToList(),
            HeaderParameters = op.HeaderParameters.Select(p => RewriteParameter(p, rename)).ToList(),
            RequestBody = op.RequestBody is null
                ? null
                : new IrRequestBody(op.RequestBody) { Type = RewriteType(op.RequestBody.Type, rename) },
            Responses = op.Responses
                .Select(r => new IrResponse(r)
                {
                    Type = r.Type is null ? null : RewriteType(r.Type, rename),
                    MediaTypes = r.MediaTypes.Select(media => media.Type is null
                        ? media
                        : new IrResponseMediaType(media) { Type = RewriteType(media.Type, rename) }).ToArray(),
                })
                .ToList(),
        };
    }

    private static IrParameter RewriteParameter(IrParameter p, IReadOnlyDictionary<string, string> rename)
        => new IrParameter(p) { Type = RewriteType(p.Type, rename) };

    private static IrComposition? RewriteComposition(IrComposition? comp, IReadOnlyDictionary<string, string> rename)
    {
        if (comp is null)
            return null;

        return new IrComposition(comp)
        {
            Types = comp.Types.Select(t => RewriteType(t, rename)).ToList(),
            DiscriminatorMapping = comp.DiscriminatorMapping?.ToDictionary(
                kv => kv.Key,
                kv => rename.GetValueOrDefault(kv.Value, kv.Value)),
        };
    }

    private static IrType RewriteType(IrType type, IReadOnlyDictionary<string, string> rename) => type switch
    {
        IrModelReference r => new IrModelReference(r)
        {
            ModelName = rename.GetValueOrDefault(r.ModelName, r.ModelName),
            OriginalModelName = r.ModelName,
        },
        IrArrayType a => new IrArrayType(a) { ElementType = RewriteType(a.ElementType, rename) },
        IrDictionaryType d => new IrDictionaryType(d)
        {
            KeyType = RewriteType(d.KeyType, rename),
            ValueType = RewriteType(d.ValueType, rename),
        },
        IrNullableType n => new IrNullableType(n) { InnerType = RewriteType(n.InnerType, rename) },
        IrOptionalType o => new IrOptionalType(o) { InnerType = RewriteType(o.InnerType, rename) },
        IrUnionType u => new IrUnionType(u) { Types = u.Types.Select(t => RewriteType(t, rename)).ToList() },
        IrIntersectionType i => new IrIntersectionType(i) { Types = i.Types.Select(t => RewriteType(t, rename)).ToList() },
        _ => type, // примитивы и прочее — без изменений
    };

    /// <summary>
    /// Склеивает prefix + name + suffix и санитизирует результат как идентификатор.
    /// При пустых prefix/suffix возвращает исходное (уже санитизированное) имя без изменений.
    /// </summary>
    private static string Affix(INamingConverter namingConverter, string prefix, string name, string suffix)
    {
        if (prefix.Length == 0 && suffix.Length == 0)
            return name;
        return namingConverter.SanitizeIdentifier(prefix + name + suffix);
    }
}
