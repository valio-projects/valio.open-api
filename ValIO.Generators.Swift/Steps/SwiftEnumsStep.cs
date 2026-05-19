using ValIO.Core.Interfaces;
using ValIO.Core.Plugins;
using ValIO.Generators.Swift.Ast.Builders;
using ValIO.Generators.Swift.Ast.Renderers;
using ValIO.Generators.Swift.Options;

namespace ValIO.Generators.Swift.Steps;

/// <summary>
/// Generates one Swift file per IR enum (<c>public enum: String, Codable, Sendable</c>).
/// </summary>
public sealed class SwiftEnumsStep : ICodeGenerationStep
{
    private readonly INamingConverter _namingConverter;
    private readonly ISwiftAstRenderer _renderer;

    public string Name => "swift-enums";
    public int Order => 20;

    public SwiftEnumsStep(INamingConverter namingConverter, ISwiftAstRenderer renderer)
    {
        _namingConverter = namingConverter;
        _renderer = renderer;
    }

    public bool ShouldExecute(GenerationContext context)
        => context.Configuration.GenerateEnums && context.IrDocument.Enums.Count > 0;

    public Task ExecuteAsync(GenerationContext context, CancellationToken ct = default)
    {
        var opts = context.PluginOptions as SwiftGenerationOptions ?? new SwiftGenerationOptions();
        var modelsDir = SwiftStepHelper.ModelsDir(opts);

        var builder = new SwiftEnumAstBuilder(_namingConverter);
        foreach (var irEnum in context.IrDocument.Enums)
        {
            var file = builder.BuildFile(irEnum);
            var content = _renderer.Render(file);
            var typeName = _namingConverter.ToPascalCase(irEnum.Name);
            var relativePath = $"{modelsDir}/{typeName}.swift";
            context.GeneratedFiles.Add(new GeneratedFile(relativePath, content));
        }

        return Task.CompletedTask;
    }
}
