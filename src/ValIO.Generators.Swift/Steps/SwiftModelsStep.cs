using ValIO.Core.Interfaces;
using ValIO.Core.Plugins;
using ValIO.Generators.Swift.Ast.Builders;
using ValIO.Generators.Swift.Ast.Renderers;
using ValIO.Generators.Swift.Options;

namespace ValIO.Generators.Swift.Steps;

/// <summary>
/// Generates one Swift file per IR model (<c>public struct</c> conforming to
/// <c>Codable, Sendable</c>).
/// </summary>
public sealed class SwiftModelsStep : ICodeGenerationStep
{
    private readonly INamingConverter _namingConverter;
    private readonly ISwiftAstRenderer _renderer;
    private readonly SwiftIrTypeMapper _typeMapper;

    public string Name => "swift-models";
    public int Order => 10;

    public SwiftModelsStep(
        INamingConverter namingConverter,
        ISwiftAstRenderer renderer,
        SwiftIrTypeMapper typeMapper)
    {
        _namingConverter = namingConverter;
        _renderer = renderer;
        _typeMapper = typeMapper;
    }

    public bool ShouldExecute(GenerationContext context)
        => context.Configuration.GenerateModels && context.IrDocument.Models.Count > 0;

    public Task ExecuteAsync(GenerationContext context, CancellationToken ct = default)
    {
        var opts = context.PluginOptions as SwiftGenerationOptions ?? new SwiftGenerationOptions();
        var modelsDir = SwiftStepHelper.ModelsDir(opts);

        var builder = new SwiftStructAstBuilder(_namingConverter, _typeMapper);
        foreach (var model in context.IrDocument.Models)
        {
            var file = builder.BuildFile(model);
            var content = _renderer.Render(file);
            // Emitted identifiers keep the affixed name (built in the AST builder from model.Name),
            // but the FILE NAME must use the pre-affix name so generated files don't move.
            var typeName = _namingConverter.ToPascalCase(model.OriginalName);
            var relativePath = $"{modelsDir}/{typeName}.swift";
            context.GeneratedFiles.Add(new GeneratedFile(relativePath, content));
        }

        return Task.CompletedTask;
    }
}
