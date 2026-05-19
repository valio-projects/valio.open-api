using ValIO.Core.Interfaces;
using ValIO.Core.Plugins;
using ValIO.Generators.Python.Ast.Builders;
using ValIO.Generators.Python.Ast.Renderers;

namespace ValIO.Generators.Python.Steps;

/// <summary>
/// Генерирует один <c>StrEnum</c> / <c>IntEnum</c> модуль на каждый IR-enum.
/// </summary>
public sealed class PythonEnumsStep : ICodeGenerationStep
{
    private readonly INamingConverter _namingConverter;
    private readonly IPythonAstRenderer _renderer;

    public string Name => "python-enums";
    public int Order => 20;

    public PythonEnumsStep(INamingConverter namingConverter, IPythonAstRenderer renderer)
    {
        _namingConverter = namingConverter;
        _renderer = renderer;
    }

    public bool ShouldExecute(GenerationContext context)
        => context.Configuration.GenerateEnums && context.IrDocument.Enums.Count > 0;

    public Task ExecuteAsync(GenerationContext context, CancellationToken ct = default)
    {
        var builder = new PythonEnumAstBuilder(_namingConverter);

        foreach (var irEnum in context.IrDocument.Enums)
        {
            var module = builder.BuildModule(irEnum);
            var content = _renderer.Render(module);
            var fileName = _namingConverter.ToSnakeCase(irEnum.Name);
            var relativePath = $"models/{fileName}.py";
            context.GeneratedFiles.Add(new GeneratedFile(relativePath, content));
        }

        return Task.CompletedTask;
    }
}
