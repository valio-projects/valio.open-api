using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.IR;
using ValIO.Core.IR.Types;
using ValIO.Core.IR.Utilities;
using ValIO.Generators.Cpp.Ast.Nodes;
using ValIO.Generators.Cpp.Services;

namespace ValIO.Generators.Cpp.Ast.Builders.Std;

/// <summary>
/// Строитель ЧИСТЫХ моделей std-профиля: header-only struct без единого следа JSON —
/// только std-типы + ссылки на другие модели/enum'ы. Сериализация живёт в
/// StdModelJsonAstBuilder (model/codec separation). allOf: одиночный $ref-базис →
/// наследование (общий IrInheritancePlan), иначе flatten. Рекурсивные свойства —
/// пропуск + warning (паритет с UE; shared_ptr — backlog).
/// </summary>
public sealed class StdModelAstBuilder
{
    private readonly StdCppTypeMapper _typeMapper;
    private readonly StdNamingConvention _naming;

    public StdModelAstBuilder(StdCppTypeMapper typeMapper, StdNamingConvention naming)
    {
        _typeMapper = typeMapper ?? throw new ArgumentNullException(nameof(typeMapper));
        _naming = naming ?? throw new ArgumentNullException(nameof(naming));
    }

    public CppHeaderFileNode Build(
        IrModel model,
        IReadOnlySet<string> enumNames,
        IReadOnlyDictionary<string, IrModel> modelLookup,
        string rootNamespace,
        ICollection<string>? warnings = null)
    {
        var structName = _naming.StructName(model.Name);
        var plan = IrInheritancePlan.Resolve(model, modelLookup);

        var fields = new List<FieldDeclarationNode>();
        var skipped = new List<string>();
        foreach (var property in plan.EffectiveProperties)
        {
            if (StdTypeReferenceCollector.ReferencesModel(property.Type, model.Name, enumNames))
            {
                skipped.Add(property.Name);
                warnings?.Add($"Model {structName}: skipping recursive property '{property.OriginalName}' (std::shared_ptr support is backlog).");
                continue;
            }
            fields.Add(BuildField(property, model, enumNames, plan));
        }

        var structNode = new StructDeclarationNode(
            new IdentifierNode(structName),
            baseTypes: plan.UseInheritance
                ? [new SimpleTypeNode(new IdentifierNode(_naming.StructName(plan.BaseModelName!)))]
                : null,
            members: fields,
            documentation: BuildDocumentation(model.Description));

        var includes = CollectIncludes(model, plan, enumNames, skipped);

        return new CppHeaderFileNode(
            fileName: _naming.ModelFileName(model.OriginalName),
            includes: includes,
            declarations: [new NamespaceDeclarationNode(new IdentifierNode(rootNamespace), [structNode])]);
    }

    private FieldDeclarationNode BuildField(IrProperty property, IrModel model, IReadOnlySet<string> enumNames, IrInheritancePlan plan)
    {
        var isWrapped = property.Type is IrNullableType or IrOptionalType;
        var mapped = _typeMapper.MapType(property.Type);
        // optional/nullable → std::optional; required без обёртки при !IsRequired — тоже optional
        if (!property.IsRequired && !isWrapped)
            mapped = new GenericTypeNode(new IdentifierNode("std::optional"), [mapped]);

        var isOptional = !property.IsRequired || isWrapped;
        var defaultValueStr = isOptional ? null : _typeMapper.GetDefaultValue(property.Type);
        var defaultValue = defaultValueStr != null
            ? new IdentifierExpressionNode(new IdentifierNode(defaultValueStr))
            : null;

        return new FieldDeclarationNode(
            new IdentifierNode(_naming.PropertyName(property.Name)),
            mapped,
            AccessModifier.Public,
            defaultValue: defaultValue,
            documentation: BuildDocumentation(property.Description));
    }

    /// <summary>
    /// std-инклуды по факту использования + ссылки на модели/enum'ы (пути — по OriginalName).
    /// Порядок: системные (сорт.), затем проектные (сорт.).
    /// </summary>
    private List<CppIncludeDirectiveNode> CollectIncludes(IrModel model, IrInheritancePlan plan, IReadOnlySet<string> enumNames, List<string> skipped)
    {
        var stdIncludes = new SortedSet<string>(StringComparer.Ordinal);
        var referencedModels = new HashSet<string>();
        var referencedEnums = new HashSet<string>();
        var orig = StdTypeReferenceCollector.BuildOriginalNameMap(model, plan.EffectiveProperties);

        if (plan.UseInheritance)
            referencedModels.Add(plan.BaseModelName!);

        foreach (var property in plan.EffectiveProperties)
        {
            if (skipped.Contains(property.Name) && StdTypeReferenceCollector.ReferencesModel(property.Type, model.Name, enumNames))
                continue;
            StdTypeReferenceCollector.Collect(property.Type, referencedModels, referencedEnums, enumNames);
            foreach (var include in StdCppTypeMapper.StdIncludesFor(property.Type))
                stdIncludes.Add(include);
            // std::optional для !IsRequired добавляет сам билдер (обёртка не видна StdIncludesFor).
            if (!property.IsRequired)
                stdIncludes.Add("<optional>");
        }

        var includes = new List<CppIncludeDirectiveNode>();
        foreach (var stdInclude in stdIncludes)
            includes.Add(new CppIncludeDirectiveNode(stdInclude.Trim('<', '>'), isSystemInclude: true));

        foreach (var refModel in referencedModels.Where(m => m != model.Name).OrderBy(x => x, StringComparer.Ordinal))
            includes.Add(new CppIncludeDirectiveNode(_naming.ModelHeaderPath(orig.GetValueOrDefault(refModel, refModel))));

        foreach (var refEnum in referencedEnums.OrderBy(x => x, StringComparer.Ordinal))
            includes.Add(new CppIncludeDirectiveNode(_naming.EnumHeaderPath(orig.GetValueOrDefault(refEnum, refEnum))));

        return includes;
    }

    private static CommentNode? BuildDocumentation(string? description)
        => !string.IsNullOrWhiteSpace(description)
            ? new CommentNode(description, CommentKind.MultiLine)
            : null;
}
