using ValIO.Core.Interfaces;
using ValIO.Core.Plugins;
using ValIO.Generators.Dart.Ast.Builders;
using ValIO.Generators.Dart.Ast.Renderers;

namespace ValIO.Generators.Dart.Steps;

/// <summary>
/// Generates one Dart file per IR model: <c>models/{snake_name}.dart</c>.
/// </summary>
public sealed class DartModelsStep : ICodeGenerationStep
{
    private readonly INamingConverter _namingConverter;
    private readonly IDartAstRenderer _renderer;
    private readonly DartIrTypeMapper _typeMapper;

    public string Name => "dart-models";
    public int Order => 10;

    public DartModelsStep(
        INamingConverter namingConverter,
        IDartAstRenderer renderer,
        DartIrTypeMapper typeMapper)
    {
        _namingConverter = namingConverter;
        _renderer = renderer;
        _typeMapper = typeMapper;
    }

    public bool ShouldExecute(GenerationContext context)
        => context.Configuration.GenerateModels && context.IrDocument.Models.Count > 0;

    public Task ExecuteAsync(GenerationContext context, CancellationToken ct = default)
    {
        var builder = new DartModelAstBuilder(_namingConverter, _typeMapper);
        foreach (var model in context.IrDocument.Models)
        {
            var file = builder.BuildFile(model);
            var content = _renderer.Render(file);
            var fileBase = _namingConverter.ToSnakeCase(model.OriginalName);
            context.GeneratedFiles.Add(new GeneratedFile($"models/{fileBase}.dart", content));
        }
        return Task.CompletedTask;
    }
}
