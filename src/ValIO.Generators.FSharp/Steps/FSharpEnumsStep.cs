using ValIO.Core.Interfaces;
using ValIO.Core.Plugins;
using ValIO.Generators.FSharp.Ast.Builders;
using ValIO.Generators.FSharp.Ast.Renderers;
using ValIO.Generators.FSharp.Options;

namespace ValIO.Generators.FSharp.Steps;

/// <summary>
/// Generates one F# file per IR enum (modeled as a DU with no-payload cases).
/// </summary>
public sealed class FSharpEnumsStep : ICodeGenerationStep
{
    private readonly INamingConverter _namingConverter;
    private readonly IFSharpAstRenderer _renderer;

    public string Name => "fsharp-enums";
    public int Order => 20;

    public FSharpEnumsStep(INamingConverter namingConverter, IFSharpAstRenderer renderer)
    {
        _namingConverter = namingConverter;
        _renderer = renderer;
    }

    public bool ShouldExecute(GenerationContext context)
        => context.Configuration.GenerateEnums && context.IrDocument.Enums.Count > 0;

    public Task ExecuteAsync(GenerationContext context, CancellationToken ct = default)
    {
        var opts = context.PluginOptions as FSharpGenerationOptions ?? new FSharpGenerationOptions();
        var builder = new FSharpEnumAstBuilder(_namingConverter);

        foreach (var irEnum in context.IrDocument.Enums)
        {
            var typeName = _namingConverter.ToPascalCase(irEnum.Name);
            var moduleName = FSharpStepHelper.ModelModuleName(opts, typeName);
            var file = builder.BuildFile(irEnum, moduleName);
            var content = _renderer.Render(file);
            var relativePath = $"{FSharpStepHelper.ModelsDir}/{typeName}.fs";
            context.GeneratedFiles.Add(new GeneratedFile(relativePath, content));
        }

        return Task.CompletedTask;
    }
}
