using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Expressions;
using ValIO.Core.Ast.Nodes.Statements;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.IR;
using ValIO.Core.IR.Enums;
using ValIO.Generators.Cpp.Ast.Nodes;
using ValIO.Generators.Cpp.Models;
using ValIO.Generators.Cpp.Services;

namespace ValIO.Generators.Cpp.Ast.Builders.Std;

/// <summary>
/// Строитель enum'ов std-профиля: чистый хедер (enum class + ToWire/FromWire без JSON)
/// и файлы JSON-кодека (Serializers/{N}Json.h/.cpp — единственное место, знающее nlohmann).
/// </summary>
public sealed class StdEnumAstBuilder
{
    private readonly StdNamingConvention _naming;

    public StdEnumAstBuilder(StdNamingConvention naming)
    {
        _naming = naming ?? throw new ArgumentNullException(nameof(naming));
    }

    /// <summary>
    /// Хедер enum'а: enum class + wire-маппинг (ToWire/FromWire), только std-инклуды.
    /// </summary>
    public CppHeaderFileNode Build(IrEnum irEnum, string rootNamespace)
    {
        var enumName = _naming.EnumName(irEnum.Name);
        var isInteger = irEnum.BaseType == IrEnumBaseType.Integer;

        var members = new List<EnumMemberDeclarationNode>();
        for (var i = 0; i < irEnum.Values.Count; i++)
        {
            var value = irEnum.Values[i];
            var wire = value.Value?.ToString() ?? value.Name;
            var numeric = isInteger ? ParseIntegerWire(wire, i) : i;
            members.Add(new EnumMemberDeclarationNode(
                new IdentifierNode(_naming.EnumMemberName(value.Name)),
                new LiteralExpressionNode(numeric.ToString(), LiteralKind.Number),
                wireValue: isInteger ? null : wire));
        }

        var enumNode = new EnumDeclarationNode(
            new IdentifierNode(enumName),
            underlyingType: new SimpleTypeNode(new IdentifierNode("std::int32_t"), isPrimitive: true),
            members: members,
            documentation: BuildDocumentation(irEnum.Description));

        var toWire = new CppFreeFunctionNode(
            new IdentifierNode("ToWire"),
            isInteger
                ? new SimpleTypeNode(new IdentifierNode("std::int32_t"), isPrimitive: true)
                : new SimpleTypeNode(new IdentifierNode("std::string_view")),
            [new CppParameterNode(new IdentifierNode("v"), new SimpleTypeNode(new IdentifierNode(enumName)))],
            body: new BlockNode(BuildToWireStatements(irEnum, enumName, isInteger)),
            documentation: new CommentNode("Wire-представление значения (исходное OpenAPI-значение).", CommentKind.MultiLine),
            isConstexpr: true);

        var fromWire = new CppFreeFunctionNode(
            new IdentifierNode("FromWire"),
            new GenericTypeNode(new IdentifierNode("std::optional"), [new SimpleTypeNode(new IdentifierNode(enumName))]),
            [new CppParameterNode(
                new IdentifierNode("w"),
                isInteger
                    ? new SimpleTypeNode(new IdentifierNode("std::int32_t"), isPrimitive: true)
                    : new SimpleTypeNode(new IdentifierNode("std::string_view")))],
            body: new BlockNode(BuildFromWireStatements(irEnum, enumName, isInteger)),
            documentation: new CommentNode("Обратный wire-маппинг; неизвестное значение → std::nullopt.", CommentKind.MultiLine),
            isConstexpr: true);

        var includes = new List<CppIncludeDirectiveNode>
        {
            new("cstdint", isSystemInclude: true),
            new("optional", isSystemInclude: true),
        };
        if (!isInteger)
            includes.Add(new("string_view", isSystemInclude: true));

        return new CppHeaderFileNode(
            fileName: _naming.EnumFileName(irEnum.OriginalName),
            includes: includes,
            declarations:
            [
                new NamespaceDeclarationNode(new IdentifierNode(rootNamespace), [enumNode, toWire, fromWire]),
            ]);
    }

    /// <summary>
    /// JSON-кодек enum'а: заголовок с декларациями ADL to_json/from_json (json_fwd)
    /// и исходник с определениями (полный nlohmann) через ToWire/FromWire.
    /// </summary>
    public (CppHeaderFileNode Header, CppSourceFileNode Source) BuildJson(IrEnum irEnum, string rootNamespace, CppGenerationOptions options)
    {
        var enumName = _naming.EnumName(irEnum.Name);
        var isInteger = irEnum.BaseType == IrEnumBaseType.Integer;
        var wireCppType = isInteger ? "std::int32_t" : "std::string";

        var codecDeclarations = new DeclarationNode[]
        {
            new CppFreeFunctionNode(
                new IdentifierNode("to_json"),
                new RawTypeNode("void"),
                [
                    new CppParameterNode(new IdentifierNode("j"), new RawTypeNode("nlohmann::json"), isReference: true),
                    new CppParameterNode(new IdentifierNode("value"), new SimpleTypeNode(new IdentifierNode(enumName)), isConst: true, isReference: true),
                ],
                isDeclarationOnly: true),
            new CppFreeFunctionNode(
                new IdentifierNode("from_json"),
                new RawTypeNode("void"),
                [
                    new CppParameterNode(new IdentifierNode("j"), new RawTypeNode("const nlohmann::json"), isReference: true),
                    new CppParameterNode(new IdentifierNode("value"), new SimpleTypeNode(new IdentifierNode(enumName)), isReference: true),
                ],
                isDeclarationOnly: true),
        };

        var header = new CppHeaderFileNode(
            fileName: _naming.SerializerHeaderPath(irEnum.OriginalName),
            includes:
            [
                new(options.JsonFwdInclude, isSystemInclude: true),
                new(_naming.EnumHeaderPath(irEnum.OriginalName)),
            ],
            declarations: [new NamespaceDeclarationNode(new IdentifierNode(rootNamespace), codecDeclarations)]);

        var toJsonBody = new BlockNode(
        [
            new ExpressionStatementNode(new BinaryExpressionNode(
                Ident("j"),
                BinaryOperator.Assign,
                new CallExpressionNode(Ident("ToWire"), [Ident("value")]),
                isAssignment: true)),
        ]);

        var fromJsonBody = new BlockNode(
        [
            new VariableDeclarationStatementNode(
                new IdentifierNode("parsed"),
                new GenericTypeNode(new IdentifierNode("std::optional"), [new SimpleTypeNode(new IdentifierNode(enumName))]),
                new CallExpressionNode(
                    Ident("FromWire"),
                    [new CallExpressionNode(
                        new MemberAccessExpressionNode(Ident("j"), new IdentifierNode("get")),
                        [],
                        genericTypeName: wireCppType)])),
            new IfStatementNode(
                Ident("parsed"),
                new BlockNode(
                [
                    new ExpressionStatementNode(new BinaryExpressionNode(
                        Ident("value"),
                        BinaryOperator.Assign,
                        new UnaryExpressionNode(UnaryOperator.Dereference, Ident("parsed")),
                        isAssignment: true)),
                ]),
                new BlockNode(
                [
                    new ThrowStatementNode(new CallExpressionNode(
                        BuildNlohmannOutOfRangeCreate($"unknown {enumName} wire value"),
                        [
                            new LiteralExpressionNode("1701", LiteralKind.Number),
                            new LiteralExpressionNode($"unknown {enumName} wire value", LiteralKind.QuotedString),
                            Ident("nullptr"),
                        ])),
                ])),
        ]);

        var source = new CppSourceFileNode(
            fileName: _naming.SerializerSourcePath(irEnum.OriginalName),
            includes:
            [
                new(_naming.SerializerHeaderPath(irEnum.OriginalName)),
                new(options.JsonInclude, isSystemInclude: true),
            ],
            sections:
            [
                new CppSourceSection(members:
                [
                    new NamespaceDeclarationNode(new IdentifierNode(rootNamespace),
                    [
                        new CppFreeFunctionNode(
                            new IdentifierNode("to_json"),
                            new RawTypeNode("void"),
                            [
                                new CppParameterNode(new IdentifierNode("j"), new RawTypeNode("nlohmann::json"), isReference: true),
                                new CppParameterNode(new IdentifierNode("value"), new SimpleTypeNode(new IdentifierNode(enumName)), isConst: true, isReference: true),
                            ],
                            body: toJsonBody),
                        new CppFreeFunctionNode(
                            new IdentifierNode("from_json"),
                            new RawTypeNode("void"),
                            [
                                new CppParameterNode(new IdentifierNode("j"), new RawTypeNode("const nlohmann::json"), isReference: true),
                                new CppParameterNode(new IdentifierNode("value"), new SimpleTypeNode(new IdentifierNode(enumName)), isReference: true),
                            ],
                            body: fromJsonBody),
                    ]),
                ]),
            ]);

        return (header, source);
    }

    private List<StatementNode> BuildToWireStatements(IrEnum irEnum, string enumName, bool isInteger)
    {
        var statements = new List<StatementNode>();
        foreach (var value in irEnum.Values)
        {
            var wire = value.Value?.ToString() ?? value.Name;
            var memberName = _naming.EnumMemberName(value.Name);
            statements.Add(new IfStatementNode(
                new BinaryExpressionNode(
                    Ident("v"),
                    BinaryOperator.Equal,
                    BuildScopedMember(enumName, memberName)),
                new BlockNode(
                [
                    isInteger
                        ? new ReturnStatementNode(new CastExpressionNode(
                            new SimpleTypeNode(new IdentifierNode("std::int32_t"), isPrimitive: true),
                            Ident("v")))
                        : new ReturnStatementNode(new LiteralExpressionNode(wire, LiteralKind.QuotedString)),
                ])));
        }

        statements.Add(new ReturnStatementNode(
            isInteger
                ? new LiteralExpressionNode("0", LiteralKind.Number)
                : new LiteralExpressionNode(string.Empty, LiteralKind.QuotedString)));
        return statements;
    }

    private List<StatementNode> BuildFromWireStatements(IrEnum irEnum, string enumName, bool isInteger)
    {
        var statements = new List<StatementNode>();
        for (var i = 0; i < irEnum.Values.Count; i++)
        {
            var value = irEnum.Values[i];
            var wire = value.Value?.ToString() ?? value.Name;
            var memberName = _naming.EnumMemberName(value.Name);
            statements.Add(new IfStatementNode(
                new BinaryExpressionNode(
                    Ident("w"),
                    BinaryOperator.Equal,
                    isInteger
                        ? new LiteralExpressionNode(ParseIntegerWire(wire, i).ToString(), LiteralKind.Number)
                        : new LiteralExpressionNode(wire, LiteralKind.QuotedString)),
                new BlockNode([new ReturnStatementNode(BuildScopedMember(enumName, memberName))])));
        }

        statements.Add(new ReturnStatementNode(Ident("std::nullopt")));
        return statements;
    }

    /// <summary>nlohmann::json::out_of_range::create(id, message) для throw при неизвестном wire-значении.</summary>
    private static MemberAccessExpressionNode BuildNlohmannOutOfRangeCreate(string message)
    {
        return new MemberAccessExpressionNode(
            new MemberAccessExpressionNode(
                new MemberAccessExpressionNode(Ident("nlohmann"), new IdentifierNode("json"), useScopeOperator: true),
                new IdentifierNode("out_of_range"),
                useScopeOperator: true),
            new IdentifierNode("create"),
            useScopeOperator: true);
    }

    private static MemberAccessExpressionNode BuildScopedMember(string enumName, string memberName)
        => new(Ident(enumName), new IdentifierNode(memberName), useScopeOperator: true);

    private static int ParseIntegerWire(string wire, int ordinal)
    {
        return int.TryParse(wire, out var parsed) ? parsed : ordinal;
    }

    private static CommentNode? BuildDocumentation(string? description)
        => !string.IsNullOrWhiteSpace(description)
            ? new CommentNode(description, CommentKind.MultiLine)
            : null;
	private static IdentifierExpressionNode Ident(string name) => new(new IdentifierNode(name));

}
