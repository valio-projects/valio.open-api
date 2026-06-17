using ValIO.Core.Interfaces;
using ValIO.Core.Plugins;
using ValIO.Generators.Ruby.Ast.Builders;
using ValIO.Generators.Ruby.Ast.Renderers;
using ValIO.Generators.Ruby.Options;

namespace ValIO.Generators.Ruby.Steps;

/// <summary>
/// Generates one Ruby file per IR enum (Ruby class with frozen-string
/// constants — the idiomatic representation of enums in Ruby).
/// </summary>
public sealed class RubyEnumsStep : ICodeGenerationStep
{
    private readonly INamingConverter _namingConverter;
    private readonly IRubyAstRenderer _renderer;

    public string Name => "ruby-enums";
    public int Order => 20;

    public RubyEnumsStep(INamingConverter namingConverter, IRubyAstRenderer renderer)
    {
        _namingConverter = namingConverter;
        _renderer = renderer;
    }

    public bool ShouldExecute(GenerationContext context)
        => context.Configuration.GenerateEnums && context.IrDocument.Enums.Count > 0;

    public Task ExecuteAsync(GenerationContext context, CancellationToken ct = default)
    {
        var opts = context.PluginOptions as RubyGenerationOptions ?? new RubyGenerationOptions();
        var modelsDir = RubyStepHelper.ModelsDir(opts);

        var builder = new RubyEnumAstBuilder(_namingConverter);
        foreach (var irEnum in context.IrDocument.Enums)
        {
            var file = builder.BuildFile(irEnum);
            var content = _renderer.Render(file);
            // File name uses the PRE-affix name so generated file paths stay stable
            // when an IR affix renames the emitted class. The class identifier built
            // by the builder still uses the affixed irEnum.Name.
            var fileName = _namingConverter.ToSnakeCase(irEnum.OriginalName);
            var relativePath = $"{modelsDir}/{fileName}.rb";
            context.GeneratedFiles.Add(new GeneratedFile(relativePath, content));
        }

        return Task.CompletedTask;
    }
}
