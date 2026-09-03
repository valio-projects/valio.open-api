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
/// Строитель JSON-кодеков моделей std-профиля: заголовок объявляет ADL to_json/from_json
/// в namespace модели (json_fwd only), исходник определяет их с ПОЛНЫМ nlohmann.
/// Единственное место, знающее wire-имена (OriginalName). Семантика: required → j.at
/// (пропуск ключа = исключение = Error{Parse} в рантайме); optional → contains + !is_null;
/// allOf-наследование → каскад через static_cast к базе; enum-поля — через их кодек (ADL).
/// </summary>
public sealed class StdModelJsonAstBuilder
{
    private readonly StdNamingConvention _naming;
    private readonly StdCppTypeMapper _typeMapper;

    public StdModelJsonAstBuilder(StdCppTypeMapper typeMapper, StdNamingConvention naming)
    {
        _typeMapper = typeMapper ?? throw new ArgumentNullException(nameof(typeMapper));
        _naming = naming ?? throw new ArgumentNullException(nameof(naming));
    }

    public (CppHeaderFileNode Header, CppSourceFileNode Source) Build(
        IrModel model,
        IReadOnlySet<string> enumNames,
        IReadOnlyDictionary<string, IrModel> modelLookup,
        string rootNamespace,
        CppGenerationOptions options,
        ICollection<string>? warnings = null)
    {
        var structName = _naming.StructName(model.Name);
        var plan = IrInheritancePlan.Resolve(model, modelLookup);
        var properties = plan.EffectiveProperties
            .Where(p => !CppTypeReferenceCollector.ReferencesModel(p.Type, model.Name, enumNames))
            .ToList();

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
                        BuildFunctionDefinition(structName, isToJson: true, body: BuildToJsonBody(structName, plan, properties)),
                        BuildFunctionDefinition(structName, isToJson: false, body: BuildFromJsonBody(structName, plan, properties)),
                    ]),
                ]),
            ]);

        return (header, source);
    }

    private CppFreeFunctionNode BuildFunctionDeclaration(string structName, bool isToJson)
        => new(
            new IdentifierNode(isToJson ? "to_json" : "from_json"),
            new RawTypeNode("void"),
            BuildParameters(structName, isToJson),
            isDeclarationOnly: true);

    private CppFreeFunctionNode BuildFunctionDefinition(string structName, bool isToJson, BlockNode body)
        => new(
            new IdentifierNode(isToJson ? "to_json" : "from_json"),
            new RawTypeNode("void"),
            BuildParameters(structName, isToJson),
            body: body);

    /// <summary>to_json(nlohmann::json&amp; j, const Struct&amp; value) / from_json(const nlohmann::json&amp; j, Struct&amp; value).</summary>
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

    // ===== to_json: j["wire"] = value.Field; optional → if (value.Field) j["wire"] = *value.Field; =====

    private BlockNode BuildToJsonBody(string structName, IrInheritancePlan plan, IReadOnlyList<IrProperty> properties)
    {
        var statements = new List<StatementNode>();

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

            if (IsOptional(property))
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

    // ===== from_json: required → j.at("wire").get_to(value.Field);
    //                   optional → if (j.contains("wire") && !j.at("wire").is_null()) { j.at("wire").get_to(...); } =====

    private BlockNode BuildFromJsonBody(string structName, IrInheritancePlan plan, IReadOnlyList<IrProperty> properties)
    {
        var statements = new List<StatementNode>();

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

            if (IsOptional(property))
            {
                // В дефолтной сборке nlohmann from_json(std::optional) отсутствует
                // (JSON_USE_IMPLICIT_CONVERSIONS=1), поэтому optional-поле заполняется
                // присваиванием внутреннего значения: value.Field = j.at("wire").get<Inner>();
                var innerRendered = StdTypeText.Render(_typeMapper.MapType(
                    IrTypeQueries.UnwrapNullableOptional(property.Type)));
                statements.Add(new IfStatementNode(
                    new BinaryExpressionNode(
                        BuildContainsCall(wire),
                        BinaryOperator.LogicalAnd,
                        new UnaryExpressionNode(
                            UnaryOperator.LogicalNot,
                            new CallExpressionNode(
                                new MemberAccessExpressionNode(BuildAtCall(wire), new IdentifierNode("is_null"))))),
                    new BlockNode(
                    [
                        new ExpressionStatementNode(new BinaryExpressionNode(
                            fieldAccess,
                            BinaryOperator.Assign,
                            new CallExpressionNode(
                                new MemberAccessExpressionNode(BuildAtCall(wire), new IdentifierNode("get")),
                                [],
                                genericTypeName: innerRendered),
                            isAssignment: true)),
                    ])));
            }
            else
            {
                statements.Add(new ExpressionStatementNode(BuildGetToCall(wire, fieldAccess)));
            }
        }

        return new BlockNode(statements);
    }

    private static bool IsOptional(IrProperty property)
        => !property.IsRequired || property.Type is IrNullableType or IrOptionalType;

    private static IndexAccessExpressionNode JsonKey(string wire)
        => new(
            StdExpressions.Ident("j"),
            new LiteralExpressionNode(wire, LiteralKind.String));

    private static CallExpressionNode BuildAtCall(string wire)
        => new(
            new MemberAccessExpressionNode(StdExpressions.Ident("j"), new IdentifierNode("at")),
            [new LiteralExpressionNode(wire, LiteralKind.String)]);

    private static CallExpressionNode BuildContainsCall(string wire)
        => new(
            new MemberAccessExpressionNode(StdExpressions.Ident("j"), new IdentifierNode("contains")),
            [new LiteralExpressionNode(wire, LiteralKind.String)]);

    private static CallExpressionNode BuildGetToCall(string wire, ExpressionNode target)
        => new(
            new MemberAccessExpressionNode(BuildAtCall(wire), new IdentifierNode("get_to")),
            [target]);

    /// <summary>
    /// Инклуды исходника: свой хедер первым, полный nlohmann, затем кодеки всех
    /// вложенных моделей/enum'ов (ADL-декларации должны быть видны) — по OriginalName.
    /// </summary>
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
        };

        foreach (var refModel in referencedModels.Where(m => m != model.Name).OrderBy(x => x, StringComparer.Ordinal))
            includes.Add(new CppIncludeDirectiveNode(_naming.SerializerHeaderPath(orig.GetValueOrDefault(refModel, refModel))));

        foreach (var refEnum in referencedEnums.OrderBy(x => x, StringComparer.Ordinal))
            includes.Add(new CppIncludeDirectiveNode(_naming.SerializerHeaderPath(orig.GetValueOrDefault(refEnum, refEnum))));

        return includes;
    }
	}
