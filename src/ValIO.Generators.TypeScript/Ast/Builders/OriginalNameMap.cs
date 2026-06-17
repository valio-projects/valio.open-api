using ValIO.Core.IR;
using ValIO.Core.IR.Types;

namespace ValIO.Generators.TypeScript.Ast.Builders;

/// <summary>
/// Сопоставление «имя с affix → исходное имя» для вычисления имён файлов и путей импорта.
///
/// Идентификаторы (типы, функции, схема-константы) в сгенерированном коде используют имена
/// С affix (берутся напрямую из IR). Имена файлов и пути импорта меняться не должны, поэтому
/// для них нужно исходное имя. Все IR-сущности несут <c>Original*</c>-поля; этот сидер
/// разворачивает их в карту, которой пользуются билдеры при построении путей.
///
/// При отсутствии affix исходное имя совпадает с affix-именем, поэтому карта работает как
/// тождество и поведение генерации не меняется.
/// </summary>
internal static class OriginalNameMap
{
    public static void AddType(Dictionary<string, string> map, IrType type)
    {
        switch (type)
        {
            case IrModelReference r:
                map[r.ModelName] = r.OriginalModelName;
                break;
            case IrArrayType a:
                AddType(map, a.ElementType);
                break;
            case IrDictionaryType d:
                AddType(map, d.KeyType);
                AddType(map, d.ValueType);
                break;
            case IrUnionType u:
                foreach (var t in u.Types) AddType(map, t);
                break;
            case IrIntersectionType i:
                foreach (var t in i.Types) AddType(map, t);
                break;
            case IrNullableType n:
                AddType(map, n.InnerType);
                break;
            case IrOptionalType o:
                AddType(map, o.InnerType);
                break;
        }
    }

    public static void AddModel(Dictionary<string, string> map, IrModel model)
    {
        map[model.Name] = model.OriginalName;
        foreach (var p in model.Properties)
            AddType(map, p.Type);
        if (model.Composition != null)
            foreach (var t in model.Composition.Types)
                AddType(map, t);
    }

    public static void AddEnum(Dictionary<string, string> map, IrEnum irEnum)
        => map[irEnum.Name] = irEnum.OriginalName;

    public static void AddOperation(Dictionary<string, string> map, IrOperation op)
    {
        map[op.MethodName] = op.OriginalMethodName;
        foreach (var p in op.PathParameters) AddType(map, p.Type);
        foreach (var p in op.QueryParameters) AddType(map, p.Type);
        foreach (var p in op.HeaderParameters) AddType(map, p.Type);
        if (op.RequestBody != null) AddType(map, op.RequestBody.Type);
        foreach (var r in op.Responses)
            if (r.Type != null) AddType(map, r.Type);
    }

    public static void AddClient(Dictionary<string, string> map, IrClient client)
    {
        foreach (var op in client.Operations)
            AddOperation(map, op);
    }
}
