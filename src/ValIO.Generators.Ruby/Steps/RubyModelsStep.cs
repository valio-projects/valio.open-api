using ValIO.Core.Interfaces;
using ValIO.Core.Plugins;
using ValIO.Generators.Ruby.Ast.Builders;
using ValIO.Generators.Ruby.Ast.Renderers;
using ValIO.Generators.Ruby.Options;

namespace ValIO.Generators.Ruby.Steps;

/// <summary>
/// Generates one Ruby file per IR model — a plain Ruby class with
/// <c>attr_accessor</c> declarations and a keyword-arg <c>initialize</c>.
/// </summary>
public sealed class RubyModelsStep : ICodeGenerationStep
{
    private readonly INamingConverter _namingConverter;
    private readonly IRubyAstRenderer _renderer;
    private readonly RubyIrTypeMapper _typeMapper;

    public string Name => "ruby-models";
    public int Order => 10;

    public RubyModelsStep(
        INamingConverter namingConverter,
        IRubyAstRenderer renderer,
        RubyIrTypeMapper typeMapper)
    {
        _namingConverter = namingConverter;
        _renderer = renderer;
        _typeMapper = typeMapper;
    }

    public bool ShouldExecute(GenerationContext context)
        => context.Configuration.GenerateModels && context.IrDocument.Models.Count > 0;

    public Task ExecuteAsync(GenerationContext context, CancellationToken ct = default)
    {
        var opts = context.PluginOptions as RubyGenerationOptions ?? new RubyGenerationOptions();
        var modelsDir = RubyStepHelper.ModelsDir(opts);

        var builder = new RubyModelAstBuilder(_namingConverter, _typeMapper);
        foreach (var model in context.IrDocument.Models)
        {
            var file = builder.BuildFile(model);
            var content = _renderer.Render(file);
            // File name uses the PRE-affix name so generated file paths (and any
            // requires that point at them) stay stable when an IR affix renames
            // the emitted class. Identifiers below still use the affixed model.Name.
            var fileName = _namingConverter.ToSnakeCase(model.OriginalName);
            var relativePath = $"{modelsDir}/{fileName}.rb";
            context.GeneratedFiles.Add(new GeneratedFile(relativePath, content));
        }

        return Task.CompletedTask;
    }
}
