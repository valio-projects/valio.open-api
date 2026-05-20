using ValIO.Core.Interfaces;
using ValIO.Core.Plugins;
using ValIO.Generators.Dart.Ast.Builders;
using ValIO.Generators.Dart.Ast.Renderers;

namespace ValIO.Generators.Dart.Steps;

/// <summary>
/// Generates one Dart file per IR enum: <c>models/{snake_name}.dart</c>.
/// </summary>
public sealed class DartEnumsStep : ICodeGenerationStep
{
    private readonly INamingConverter _namingConverter;
    private readonly IDartAstRenderer _renderer;

    public string Name => "dart-enums";
    public int Order => 20;

    public DartEnumsStep(INamingConverter namingConverter, IDartAstRenderer renderer)
    {
        _namingConverter = namingConverter;
        _renderer = renderer;
    }

    public bool ShouldExecute(GenerationContext context)
        => context.Configuration.GenerateEnums && context.IrDocument.Enums.Count > 0;

    public Task ExecuteAsync(GenerationContext context, CancellationToken ct = default)
    {
        var builder = new DartEnumAstBuilder(_namingConverter);
        foreach (var en in context.IrDocument.Enums)
        {
            var file = builder.BuildFile(en);
            var content = _renderer.Render(file);
            var fileBase = _namingConverter.ToSnakeCase(en.Name);
            context.GeneratedFiles.Add(new GeneratedFile($"models/{fileBase}.dart", content));
        }
        return Task.CompletedTask;
    }
}
