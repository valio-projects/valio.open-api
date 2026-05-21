using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Ast.Nodes.Types;
using ValIO.Core.Interfaces;
using ValIO.Core.IR;
using ValIO.Core.IR.Types;
using ValIO.Generators.FSharp.Ast.Nodes;

namespace ValIO.Generators.FSharp.Ast.Builders;

/// <summary>
/// Builds an F# record (or a discriminated union when the model is oneOf/anyOf-composed)
/// for one <see cref="IrModel"/>. Emits a single <see cref="FSharpFileNode"/> per model.
/// </summary>
public sealed class FSharpModelAstBuilder
{
    private readonly INamingConverter _namingConverter;
    private readonly FSharpIrTypeMapper _typeMapper;

    public FSharpModelAstBuilder(INamingConverter namingConverter, FSharpIrTypeMapper typeMapper)
    {
        _namingConverter = namingConverter;
        _typeMapper = typeMapper;
    }

    public FSharpFileNode BuildFile(IrModel model, string moduleName)
    {
        // OneOf/AnyOf compose into a DU; everything else is a record.
        if (model.Composition is { Kind: IrCompositionKind.OneOf or IrCompositionKind.AnyOf } composition)
        {
            var du = BuildDu(model, composition);
            return new FSharpFileNode(
                moduleName: moduleName,
                declarations: [du],
                opens: ["System"]);
        }

        var record = BuildRecord(model);
        return new FSharpFileNode(
            moduleName: moduleName,
            declarations: [record],
            opens: ["System"]);
    }

    private FSharpRecordNode BuildRecord(IrModel model)
    {
        var fields = new List<FSharpRecordNode.Field>(model.Properties.Count);
        foreach (var prop in model.Properties)
        {
            var pascalName = _namingConverter.ToPascalCase(prop.Name);
            var safeName = FSharpReservedNames.Avoid(pascalName);
            var mapped = _typeMapper.MapType(prop.Type);
            var isOptional = _typeMapper.IsOptionalInLanguage(prop);
            var finalType = isOptional ? EnsureNullable(mapped) : mapped;
            fields.Add(new FSharpRecordNode.Field(safeName, finalType));
        }

        return new FSharpRecordNode(
            new IdentifierNode(_namingConverter.ToPascalCase(model.Name)),
            fields,
            documentation: !string.IsNullOrWhiteSpace(model.Description)
                ? new CommentNode(model.Description!, CommentKind.MultiLine)
                : null);
    }

    private FSharpDuNode BuildDu(IrModel model, IrComposition composition)
    {
        var cases = new List<FSharpDuNode.Case>(composition.Types.Count);
        var index = 0;
        foreach (var t in composition.Types)
        {
            index++;
            var caseName = ChooseCaseName(t, index);
            var payload = _typeMapper.MapType(t);
            cases.Add(new FSharpDuNode.Case(caseName, payload));
        }

        return new FSharpDuNode(
            new IdentifierNode(_namingConverter.ToPascalCase(model.Name)),
            cases,
            documentation: !string.IsNullOrWhiteSpace(model.Description)
                ? new CommentNode(model.Description!, CommentKind.MultiLine)
                : null);
    }

    private string ChooseCaseName(IrType t, int index) => t switch
    {
        IrModelReference r => _namingConverter.ToPascalCase(r.ModelName),
        _ => $"Case{index}",
    };

    private static TypeNode EnsureNullable(TypeNode type) =>
        type is NullableTypeNode ? type : new NullableTypeNode(type);
}
