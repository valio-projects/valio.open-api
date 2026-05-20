using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.Interfaces;
using ValIO.Core.IR;
using ValIO.Core.IR.Enums;
using ValIO.Core.IR.Types;
using ValIO.Generators.Python.Ast.Nodes;

namespace ValIO.Generators.Python.Ast.Builders;

/// <summary>
/// Строит Pydantic v2 BaseModel из IR-модели в виде <see cref="PythonModuleNode"/>.
/// </summary>
public sealed class PydanticModelAstBuilder
{
    private readonly INamingConverter _namingConverter;
    private readonly PythonIrTypeMapper _typeMapper;

    public PydanticModelAstBuilder(INamingConverter namingConverter, PythonIrTypeMapper typeMapper)
    {
        _namingConverter = namingConverter;
        _typeMapper = typeMapper;
    }

    public PythonModuleNode BuildModule(IrModel model, IReadOnlyDictionary<string, string> moduleByModelName)
    {
        var typingImports = new SortedSet<string>(StringComparer.Ordinal);
        var datetimeImports = new SortedSet<string>(StringComparer.Ordinal);
        var uuidNeeded = false;
        var anyNeeded = false;

        // Маппим все свойства через type mapper и собираем зависимости.
        var members = new List<DeclarationNode>();
        var typeCheckingImports = new List<PythonImportNode>();
        var typeCheckingTargets = new SortedDictionary<string, string>(StringComparer.Ordinal);

        members.Add(new PythonAttributeAssignmentNode(
            new IdentifierNode("model_config"),
            annotation: null,
            defaultValueLiteral: "ConfigDict(populate_by_name=True)"));

        foreach (var property in model.Properties)
        {
            var mappedType = _typeMapper.MapType(property.Type);
            var isOptional = _typeMapper.IsOptionalInLanguage(property);

            // Собираем зависимости моделей (forward refs).
            CollectModelReferences(property.Type, typeCheckingTargets, moduleByModelName, model.Name);

            // Учитываем потребности в импортах.
            ScanTypeForImports(property.Type, datetimeImports, ref uuidNeeded, ref anyNeeded);

            var hasForwardRef = HasModelReference(property.Type, typeCheckingTargets);
            TypeNode finalType;
            if (isOptional)
                finalType = WrapWithNone(mappedType);
            else
                finalType = mappedType;

            string? defaultLiteral = isOptional ? "None" : null;

            members.Add(new PythonAttributeAssignmentNode(
                new IdentifierNode(SanitizePropertyName(property.Name)),
                annotation: finalType,
                defaultValueLiteral: defaultLiteral,
                quoteAnnotation: hasForwardRef,
                documentation: !string.IsNullOrWhiteSpace(property.Description)
                    ? new CommentNode(property.Description, CommentKind.SingleLine)
                    : null));
        }

        // Стандартные импорты — pydantic ВСЕГДА.
        var standardImports = new List<PythonImportNode>
        {
            new PythonImportNode("pydantic", new[] { "BaseModel", "ConfigDict" })
        };

        if (datetimeImports.Count > 0)
            standardImports.Insert(0, new PythonImportNode("datetime", datetimeImports.ToList()));
        if (uuidNeeded)
            standardImports.Insert(0, new PythonImportNode("uuid", new[] { "UUID" }));

        // typing импорты (Any).
        if (anyNeeded)
            standardImports.Insert(0, new PythonImportNode("typing", new[] { "Any" }));

        // TYPE_CHECKING импорты для inter-model forward refs.
        if (typeCheckingTargets.Count > 0)
        {
            standardImports.Insert(0, new PythonImportNode("typing", new[] { "TYPE_CHECKING" }));
            foreach (var kvp in typeCheckingTargets)
                typeCheckingImports.Add(new PythonImportNode(kvp.Value, new[] { kvp.Key }));
        }

        var classNode = new PythonClassDeclarationNode(
            new IdentifierNode(model.Name),
            baseClasses: new TypeNode[] { new SimpleTypeNode(new IdentifierNode("BaseModel")) },
            members: members,
            documentation: !string.IsNullOrWhiteSpace(model.Description)
                ? new CommentNode(model.Description, CommentKind.MultiLine)
                : null);

        var trailingStatements = typeCheckingTargets.Count > 0
            ? new[] { $"{model.Name}.model_rebuild()" }
            : Array.Empty<string>();

        return new PythonModuleNode(
            declarations: new DeclarationNode[] { classNode },
            imports: standardImports,
            typeCheckingImports: typeCheckingImports,
            futureImports: new[] { new PythonImportNode("__future__", new[] { "annotations" }) },
            trailingStatements: trailingStatements);
    }

    private string SanitizePropertyName(string name) =>
        _namingConverter.ToSnakeCase(name) is var snake && !string.IsNullOrEmpty(snake)
            ? PythonKeywords.Avoid(snake)
            : "_";

    private static TypeNode WrapWithNone(TypeNode inner)
    {
        if (inner is UnionTypeNode union)
        {
            // Уже union — добавляем None, если ещё не там.
            if (union.Types.Any(t => t is SimpleTypeNode s && s.Name.Name == "None"))
                return union;
            var types = union.Types.Concat(new[] { (TypeNode)new SimpleTypeNode(new IdentifierNode("None"), isPrimitive: true) }).ToList();
            return new UnionTypeNode(types);
        }
        return new UnionTypeNode(new[] { inner, (TypeNode)new SimpleTypeNode(new IdentifierNode("None"), isPrimitive: true) });
    }

    private void CollectModelReferences(
        IrType type,
        SortedDictionary<string, string> targets,
        IReadOnlyDictionary<string, string> moduleByModelName,
        string selfName)
    {
        switch (type)
        {
            case IrModelReference r when r.ModelName != selfName:
                if (moduleByModelName.TryGetValue(r.ModelName, out var module))
                    targets[r.ModelName] = module;
                break;
            case IrArrayType a:
                CollectModelReferences(a.ElementType, targets, moduleByModelName, selfName);
                break;
            case IrDictionaryType d:
                CollectModelReferences(d.KeyType, targets, moduleByModelName, selfName);
                CollectModelReferences(d.ValueType, targets, moduleByModelName, selfName);
                break;
            case IrNullableType n:
                CollectModelReferences(n.InnerType, targets, moduleByModelName, selfName);
                break;
            case IrOptionalType o:
                CollectModelReferences(o.InnerType, targets, moduleByModelName, selfName);
                break;
            case IrUnionType u:
                foreach (var t in u.Types) CollectModelReferences(t, targets, moduleByModelName, selfName);
                break;
            case IrIntersectionType i:
                foreach (var t in i.Types) CollectModelReferences(t, targets, moduleByModelName, selfName);
                break;
        }
    }

    private static bool HasModelReference(IrType type, SortedDictionary<string, string> targets) => type switch
    {
        IrModelReference r => targets.ContainsKey(r.ModelName),
        IrArrayType a => HasModelReference(a.ElementType, targets),
        IrDictionaryType d => HasModelReference(d.ValueType, targets) || HasModelReference(d.KeyType, targets),
        IrNullableType n => HasModelReference(n.InnerType, targets),
        IrOptionalType o => HasModelReference(o.InnerType, targets),
        IrUnionType u => u.Types.Any(t => HasModelReference(t, targets)),
        IrIntersectionType i => i.Types.Any(t => HasModelReference(t, targets)),
        _ => false
    };

    private static void ScanTypeForImports(
        IrType type,
        SortedSet<string> datetimeImports,
        ref bool uuidNeeded,
        ref bool anyNeeded)
    {
        switch (type)
        {
            case IrPrimitiveType p:
                switch (p.PrimitiveKind)
                {
                    case IrPrimitiveKind.Date: datetimeImports.Add("date"); break;
                    case IrPrimitiveKind.DateTime: datetimeImports.Add("datetime"); break;
                    case IrPrimitiveKind.Time: datetimeImports.Add("time"); break;
                    case IrPrimitiveKind.Uuid: uuidNeeded = true; break;
                    case IrPrimitiveKind.Any: anyNeeded = true; break;
                }
                break;
            case IrArrayType a:
                ScanTypeForImports(a.ElementType, datetimeImports, ref uuidNeeded, ref anyNeeded);
                break;
            case IrDictionaryType d:
                ScanTypeForImports(d.KeyType, datetimeImports, ref uuidNeeded, ref anyNeeded);
                ScanTypeForImports(d.ValueType, datetimeImports, ref uuidNeeded, ref anyNeeded);
                break;
            case IrNullableType n:
                ScanTypeForImports(n.InnerType, datetimeImports, ref uuidNeeded, ref anyNeeded);
                break;
            case IrOptionalType o:
                ScanTypeForImports(o.InnerType, datetimeImports, ref uuidNeeded, ref anyNeeded);
                break;
            case IrUnionType u:
                foreach (var t in u.Types) ScanTypeForImports(t, datetimeImports, ref uuidNeeded, ref anyNeeded);
                break;
            case IrIntersectionType i:
                foreach (var t in i.Types) ScanTypeForImports(t, datetimeImports, ref uuidNeeded, ref anyNeeded);
                break;
        }
    }
}

/// <summary>
/// Списки Python keywords / built-ins, которые нельзя использовать как идентификаторы.
/// </summary>
internal static class PythonKeywords
{
    private static readonly HashSet<string> Reserved = new(StringComparer.Ordinal)
    {
        "False","None","True","and","as","assert","async","await","break","class","continue","def","del","elif",
        "else","except","finally","for","from","global","if","import","in","is","lambda","nonlocal","not","or",
        "pass","raise","return","try","while","with","yield","match","case","type",
    };

    public static string Avoid(string name) => Reserved.Contains(name) ? name + "_" : name;
}
