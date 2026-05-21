using ValIO.Core.Ast.Enums;
using ValIO.Core.Ast.Nodes;
using ValIO.Core.Interfaces;
using ValIO.Core.IR;
using ValIO.Generators.FSharp.Ast.Nodes;

namespace ValIO.Generators.FSharp.Ast.Builders;

/// <summary>
/// Builds an F# discriminated union (no-payload cases) for one <see cref="IrEnum"/>.
/// </summary>
public sealed class FSharpEnumAstBuilder
{
    private readonly INamingConverter _namingConverter;

    public FSharpEnumAstBuilder(INamingConverter namingConverter)
    {
        _namingConverter = namingConverter;
    }

    public FSharpFileNode BuildFile(IrEnum irEnum, string moduleName)
    {
        var cases = new List<FSharpDuNode.Case>(irEnum.Values.Count);
        foreach (var v in irEnum.Values)
        {
            var caseName = FSharpReservedNames.Avoid(_namingConverter.ToPascalCase(v.Name));
            cases.Add(new FSharpDuNode.Case(caseName, Payload: null));
        }

        var du = new FSharpDuNode(
            new IdentifierNode(_namingConverter.ToPascalCase(irEnum.Name)),
            cases,
            documentation: !string.IsNullOrWhiteSpace(irEnum.Description)
                ? new CommentNode(irEnum.Description!, CommentKind.MultiLine)
                : null);

        return new FSharpFileNode(
            moduleName: moduleName,
            declarations: [du],
            opens: ["System"]);
    }
}
