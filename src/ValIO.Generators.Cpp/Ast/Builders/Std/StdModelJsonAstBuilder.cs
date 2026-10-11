using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.Ast.Nodes.Statements;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.IR;
using ValIO.Core.IR.Types;
using ValIO.Core.IR.Utilities;
using ValIO.Generators.Cpp.Ast.Nodes;
using ValIO.Generators.Cpp.Models;
using ValIO.Generators.Cpp.Services;

namespace ValIO.Generators.Cpp.Ast.Builders.Std;

/// <summary>
/// Builds ADL JSON codecs that preserve required keys, nullable values, and optional presence.
/// </summary>
public sealed class StdModelJsonAstBuilder
{
    /// <summary>Names model fields and serializer files.</summary>
    private readonly StdNamingConvention _naming;
    /// <summary>Maps the types read by generated JSON codecs.</summary>
    private readonly StdCppTypeMapper _typeMapper;

    /// <summary>Creates a codec builder sharing the model's naming and type mapping.</summary>
    /// <param name="typeMapper">The standalone type mapper.</param>
    /// <param name="naming">The generated naming convention.</param>
    /// <exception cref="ArgumentNullException">A required service is missing.</exception>
    public StdModelJsonAstBuilder(StdCppTypeMapper typeMapper, StdNamingConvention naming)
    {
        _typeMapper = typeMapper ?? throw new ArgumentNullException(nameof(typeMapper));
        _naming = naming ?? throw new ArgumentNullException(nameof(naming));
    }

    /// <summary>Builds declarations and definitions for one model's JSON conversion.</summary>
    /// <param name="model">The model contract.</param>
    /// <param name="enumNames">The document's enum names.</param>
    /// <param name="modelLookup">The models available for inheritance resolution.</param>
    /// <param name="rootNamespace">The generated namespace.</param>
    /// <param name="options">The configured JSON include paths.</param>
    /// <param name="warnings">The optional generation diagnostic sink.</param>
    /// <returns>The serializer header and source ASTs.</returns>
    /// <exception cref="NotSupportedException">The model requires unsupported recursive ownership or composition codecs.</exception>
    public (CppHeaderFileNode Header, CppSourceFileNode Source) Build(
        IrModel model,
        IReadOnlySet<string> enumNames,
        IReadOnlyDictionary<string, IrModel> modelLookup,
        string rootNamespace,
        CppGenerationOptions options,
        ICollection<string>? warnings = null)
    {
        var structName = _naming.StructName(model.Name);
        CppTypeReferenceCollector.EnsureSupportedComposition(model, modelLookup);
        var plan = IrInheritancePlan.Resolve(model, modelLookup);
        CppTypeReferenceCollector.EnsureNonRecursive(model, plan.EffectiveProperties, enumNames);
        var properties = plan.EffectiveProperties;

        var header = new CppHeaderFileNode(
            fileName: _naming.SerializerHeaderPath(model.OriginalName),
            includes:
            [
                new CppIncludeDirectiveNode(options.JsonFwdInclude, isSystemInclude: true),
                new CppIncludeDirectiveNode(_naming.ModelHeaderPath(model.OriginalName)),
            ],
            declarations:
            [
                new NamespaceDeclarationNode(new IdentifierNode(rootNamespace),
                [
                    BuildFunctionDeclaration(structName, isToJson: true),
                    BuildFunctionDeclaration(structName, isToJson: false),
                ]),
            ]);

        var source = new CppSourceFileNode(
            fileName: _naming.SerializerSourcePath(model.OriginalName),
            includes: CollectSourceIncludes(model, plan, properties, enumNames, options),
            sections:
            [
                new CppSourceSection(members:
                [
                    new NamespaceDeclarationNode(new IdentifierNode(rootNamespace),
                    [
                        BuildFunctionDefinition(structName, isToJson: true, body: BuildToJsonBody(plan, properties)),
                        BuildFunctionDefinition(structName, isToJson: false, body: BuildFromJsonBody(plan, properties)),
                    ]),
                ]),
            ]);

        return (header, source);
    }

    /// <summary>Declares one ADL conversion function.</summary>
    /// <param name="structName">The generated model name.</param>
    /// <param name="isToJson">Whether the function serializes rather than parses.</param>
    /// <returns>The function declaration.</returns>
    private CppFreeFunctionNode BuildFunctionDeclaration(string structName, bool isToJson)
        => new(
            new IdentifierNode(isToJson ? "to_json" : "from_json"),
            new RawTypeNode("void"),
            BuildParameters(structName, isToJson),
            isDeclarationOnly: true);

    /// <summary>Defines one ADL conversion function.</summary>
    /// <param name="structName">The generated model name.</param>
    /// <param name="isToJson">Whether the function serializes rather than parses.</param>
    /// <param name="body">The conversion statements.</param>
    /// <returns>The function definition.</returns>
    private CppFreeFunctionNode BuildFunctionDefinition(string structName, bool isToJson, BlockNode body)
        => new(
            new IdentifierNode(isToJson ? "to_json" : "from_json"),
            new RawTypeNode("void"),
            BuildParameters(structName, isToJson),
            body: body);

    /// <summary>Builds the JSON and model reference parameters for ADL conversions.</summary>
    /// <param name="structName">The generated model name.</param>
    /// <param name="isToJson">Whether the model reference must be const.</param>
    /// <returns>The two conversion parameters.</returns>
    private static CppParameterNode[] BuildParameters(string structName, bool isToJson)
        =>
        [
            new CppParameterNode(
                new IdentifierNode("j"),
                new RawTypeNode(isToJson ? "nlohmann::json" : "const nlohmann::json"),
                isReference: true),
            new CppParameterNode(
                new IdentifierNode("value"),
                new RawTypeNode(isToJson ? $"const {structName}&" : $"{structName}&")),
        ];

    /// <summary>Serializes a complete object, omitting only absent optional keys.</summary>
    /// <param name="plan">The resolved inheritance strategy.</param>
    /// <param name="properties">The fields declared by this model.</param>
    /// <returns>The serialization body.</returns>
    private BlockNode BuildToJsonBody(IrInheritancePlan plan, IReadOnlyList<IrProperty> properties)
    {
        var statements = new List<StatementNode>
        {
            new ExpressionStatementNode(new BinaryExpressionNode(
                StdExpressions.Ident("j"), BinaryOperator.Assign,
                new CallExpressionNode(StdExpressions.Ident("nlohmann::json::object")), isAssignment: true)),
            new ExpressionStatementNode(new CastExpressionNode(new RawTypeNode("void"), StdExpressions.Ident("value"))),
        };

        if (plan.UseInheritance)
        {
            statements.Add(new ExpressionStatementNode(new CallExpressionNode(
                StdExpressions.Ident("to_json"),
                [
                    StdExpressions.Ident("j"),
                    new CastExpressionNode(
                        new RawTypeNode($"const {_naming.StructName(plan.BaseModelName!)}&"),
                        StdExpressions.Ident("value")),
                ])));
        }

        foreach (var property in properties)
        {
            var field = _naming.PropertyName(property.Name);
            var wire = property.OriginalName;
            var fieldAccess = new MemberAccessExpressionNode(StdExpressions.Ident("value"), new IdentifierNode(field));

            if (StdPropertyTypes.IsOptional(property))
            {
                statements.Add(new IfStatementNode(
                    fieldAccess,
                    new BlockNode(
                    [
                        new ExpressionStatementNode(new BinaryExpressionNode(
                            JsonKey(wire),
                            BinaryOperator.Assign,
                            new UnaryExpressionNode(UnaryOperator.Dereference, fieldAccess),
                            isAssignment: true)),
                    ])));
            }
            else
            {
                statements.Add(new ExpressionStatementNode(new BinaryExpressionNode(
                    JsonKey(wire),
                    BinaryOperator.Assign,
                    fieldAccess,
                    isAssignment: true)));
            }
        }

        return new BlockNode(statements);
    }

    /// <summary>Reads required keys and replaces optional state even when reusing a model instance.</summary>
    /// <param name="plan">The resolved inheritance strategy.</param>
    /// <param name="properties">The fields declared by this model.</param>
    /// <returns>The deserialization body.</returns>
    private BlockNode BuildFromJsonBody(IrInheritancePlan plan, IReadOnlyList<IrProperty> properties)
    {
        var statements = new List<StatementNode>
        {
            new ExpressionStatementNode(new CastExpressionNode(new RawTypeNode("void"),
                new CallExpressionNode(new MemberAccessExpressionNode(StdExpressions.Ident("j"), new IdentifierNode("get_ref")),
                    [], genericTypeName: "const nlohmann::json::object_t&"))),
            new ExpressionStatementNode(new CastExpressionNode(new RawTypeNode("void"), StdExpressions.Ident("value"))),
        };

        if (plan.UseInheritance)
        {
            statements.Add(new ExpressionStatementNode(new CallExpressionNode(
                StdExpressions.Ident("from_json"),
                [
                    StdExpressions.Ident("j"),
                    new CastExpressionNode(
                        new RawTypeNode($"{_naming.StructName(plan.BaseModelName!)}&"),
                        StdExpressions.Ident("value")),
                ])));
        }

        foreach (var property in properties)
        {
            var field = _naming.PropertyName(property.Name);
            var wire = property.OriginalName;
            var fieldAccess = new MemberAccessExpressionNode(StdExpressions.Ident("value"), new IdentifierNode(field));

            if (StdPropertyTypes.IsOptional(property))
            {
                var innerRendered = StdTypeText.Render(_typeMapper.MapType(StdPropertyTypes.ValueType(property)));
                statements.Add(new IfStatementNode(
                    BuildContainsCall(wire),
                    new BlockNode(
                    [
                        new ExpressionStatementNode(new CallExpressionNode(
                            new MemberAccessExpressionNode(fieldAccess, new IdentifierNode("emplace")),
                            [
                            new CallExpressionNode(
                                new MemberAccessExpressionNode(BuildAtCall(wire), new IdentifierNode("get")),
                                [],
                                genericTypeName: innerRendered),
                            ])),
                    ]),
                    new BlockNode(
                    [
                        new ExpressionStatementNode(new CallExpressionNode(
                            new MemberAccessExpressionNode(fieldAccess, new IdentifierNode("reset")))),
                    ])));
            }
            else
            {
                statements.Add(new ExpressionStatementNode(BuildGetToCall(wire, fieldAccess)));
            }
        }

        return new BlockNode(statements);
    }

    /// <summary>Builds assignment access for an original JSON key.</summary>
    /// <param name="wire">The original schema property name.</param>
    /// <returns>The JSON subscript expression.</returns>
    private static IndexAccessExpressionNode JsonKey(string wire)
        => new(
            StdExpressions.Ident("j"),
            new LiteralExpressionNode(wire, LiteralKind.String));

    /// <summary>Builds checked access that rejects missing required keys.</summary>
    /// <param name="wire">The original schema property name.</param>
    /// <returns>The checked JSON access expression.</returns>
    private static CallExpressionNode BuildAtCall(string wire)
        => new(
            new MemberAccessExpressionNode(StdExpressions.Ident("j"), new IdentifierNode("at")),
            [new LiteralExpressionNode(wire, LiteralKind.String)]);

    /// <summary>Builds the presence check for an optional property.</summary>
    /// <param name="wire">The original schema property name.</param>
    /// <returns>The JSON key presence expression.</returns>
    private static CallExpressionNode BuildContainsCall(string wire)
        => new(
            new MemberAccessExpressionNode(StdExpressions.Ident("j"), new IdentifierNode("contains")),
            [new LiteralExpressionNode(wire, LiteralKind.String)]);

    /// <summary>Builds a checked JSON conversion into a required field.</summary>
    /// <param name="wire">The original schema property name.</param>
    /// <param name="target">The destination model field.</param>
    /// <returns>The field conversion expression.</returns>
    private static CallExpressionNode BuildGetToCall(string wire, ExpressionNode target)
        => new(
            new MemberAccessExpressionNode(BuildAtCall(wire), new IdentifierNode("get_to")),
            [target]);

    /// <summary>
    /// Collects JSON runtime support and ADL declarations for all referenced model and enum codecs.
    /// </summary>
    /// <param name="model">The current model.</param>
    /// <param name="plan">The resolved inheritance strategy.</param>
    /// <param name="properties">The generated model fields.</param>
    /// <param name="enumNames">The document's enum names.</param>
    /// <param name="options">The configured JSON include paths.</param>
    /// <returns>The ordered serializer source includes.</returns>
    private List<CppIncludeDirectiveNode> CollectSourceIncludes(
        IrModel model,
        IrInheritancePlan plan,
        IReadOnlyList<IrProperty> properties,
        IReadOnlySet<string> enumNames,
        CppGenerationOptions options)
    {
        var referencedModels = new HashSet<string>();
        var referencedEnums = new HashSet<string>();
        var orig = CppTypeReferenceCollector.BuildOriginalNameMap(model, plan.EffectiveProperties);

        if (plan.UseInheritance)
            referencedModels.Add(plan.BaseModelName!);

        foreach (var property in properties)
            CppTypeReferenceCollector.Collect(property.Type, referencedModels, referencedEnums, enumNames);

        var includes = new List<CppIncludeDirectiveNode>
        {
            new(_naming.SerializerHeaderPath(model.OriginalName)),
            new(options.JsonInclude, isSystemInclude: true),
            new("Runtime/ValIOJsonTypes.h"),
        };

        foreach (var refModel in referencedModels.Where(m => m != model.Name).OrderBy(x => x, StringComparer.Ordinal))
            includes.Add(new CppIncludeDirectiveNode(_naming.SerializerHeaderPath(orig.GetValueOrDefault(refModel, refModel))));

        foreach (var refEnum in referencedEnums.OrderBy(x => x, StringComparer.Ordinal))
            includes.Add(new CppIncludeDirectiveNode(_naming.SerializerHeaderPath(orig.GetValueOrDefault(refEnum, refEnum))));

        return includes;
    }
	}
