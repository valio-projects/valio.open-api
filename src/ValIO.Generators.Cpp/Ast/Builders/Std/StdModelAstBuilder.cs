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
/// Builds JSON-independent standalone C++ model headers from the shared inheritance plan.
/// </summary>
public sealed class StdModelAstBuilder
{
    /// <summary>Maps IR values to standalone C++ types.</summary>
    private readonly StdCppTypeMapper _typeMapper;
    /// <summary>Names generated declarations and files.</summary>
    private readonly StdNamingConvention _naming;

    /// <summary>Creates a model builder with consistent naming and type mapping.</summary>
    /// <param name="typeMapper">The standalone type mapper.</param>
    /// <param name="naming">The generated naming convention.</param>
    /// <exception cref="ArgumentNullException">A required service is missing.</exception>
    public StdModelAstBuilder(StdCppTypeMapper typeMapper, StdNamingConvention naming)
    {
        _typeMapper = typeMapper ?? throw new ArgumentNullException(nameof(typeMapper));
        _naming = naming ?? throw new ArgumentNullException(nameof(naming));
    }

    /// <summary>Builds a pure model header or rejects unsupported recursive ownership.</summary>
    /// <param name="model">The model contract.</param>
    /// <param name="enumNames">The document's enum names.</param>
    /// <param name="modelLookup">The inheritance model lookup.</param>
    /// <param name="rootNamespace">The generated namespace.</param>
    /// <param name="warnings">The optional diagnostic sink.</param>
    /// <returns>The generated definition header.</returns>
    /// <exception cref="NotSupportedException">The model requires unsupported recursive ownership or composition codecs.</exception>
    public CppHeaderFileNode Build(
        IrModel model,
        IReadOnlySet<string> enumNames,
        IReadOnlyDictionary<string, IrModel> modelLookup,
        string rootNamespace,
        ICollection<string>? warnings = null)
    {
        var structName = _naming.StructName(model.Name);
        CppTypeReferenceCollector.EnsureSupportedComposition(model, modelLookup);
        var plan = IrInheritancePlan.Resolve(model, modelLookup);
        CppTypeReferenceCollector.EnsureNonRecursive(model, plan.EffectiveProperties, enumNames);

        var fields = new List<FieldDeclarationNode>();
        foreach (var property in plan.EffectiveProperties)
        {
            fields.Add(BuildField(property));
        }

        var structNode = new StructDeclarationNode(
            new IdentifierNode(structName),
            baseTypes: plan.UseInheritance
                ? [new SimpleTypeNode(new IdentifierNode(_naming.StructName(plan.BaseModelName!)))]
                : null,
            members: fields,
            documentation: BuildDocumentation(model.Description));

        var includes = CollectIncludes(model, plan, enumNames);

        return new CppHeaderFileNode(
            fileName: _naming.ModelFileName(model.OriginalName),
            includes: includes,
            declarations: [new NamespaceDeclarationNode(new IdentifierNode(rootNamespace), [structNode])]);
    }

    /// <summary>Builds one field without collapsing missing keys and explicit nulls.</summary>
    /// <param name="property">The property contract.</param>
    /// <returns>The corresponding C++ field declaration.</returns>
    private FieldDeclarationNode BuildField(IrProperty property)
    {
        var mapped = StdPropertyTypes.MapField(property, _typeMapper);
        var isOptional = StdPropertyTypes.IsOptional(property) || property.Type is IrNullableType;
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
    /// Collects explicit standard dependencies and stable model and enum include paths.
    /// </summary>
    /// <param name="model">The model contract.</param>
    /// <param name="plan">The resolved inheritance plan.</param>
    /// <param name="enumNames">The document's enum names.</param>
    /// <returns>The ordered include directives.</returns>
    private List<CppIncludeDirectiveNode> CollectIncludes(IrModel model, IrInheritancePlan plan, IReadOnlySet<string> enumNames)
    {
        var stdIncludes = new SortedSet<string>(StringComparer.Ordinal);
        var referencedModels = new HashSet<string>();
        var referencedEnums = new HashSet<string>();
        var orig = CppTypeReferenceCollector.BuildOriginalNameMap(model, plan.EffectiveProperties);

        if (plan.UseInheritance)
            referencedModels.Add(plan.BaseModelName!);

        foreach (var property in plan.EffectiveProperties)
        {
            CppTypeReferenceCollector.Collect(property.Type, referencedModels, referencedEnums, enumNames);
            foreach (var include in StdCppTypeMapper.StdIncludesFor(property.Type))
                stdIncludes.Add(include);
            // Property presence wrappers are added after mapping the schema value type.
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
