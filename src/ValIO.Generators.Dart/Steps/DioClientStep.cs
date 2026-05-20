using ValIO.Core.Interfaces;
using ValIO.Core.Plugins;
using ValIO.Generators.Dart.Ast.Builders;
using ValIO.Generators.Dart.Ast.Renderers;

namespace ValIO.Generators.Dart.Steps;

/// <summary>
/// Generates one Dart Dio service per IR client: <c>api/{snake_name}_service.dart</c>.
/// </summary>
public sealed class DioClientStep : ICodeGenerationStep
{
    private readonly INamingConverter _namingConverter;
    private readonly IDartAstRenderer _renderer;
    private readonly DartIrTypeMapper _typeMapper;

    public string Name => "dart-dio-client";
    public int Order => 40;

    public DioClientStep(
        INamingConverter namingConverter,
        IDartAstRenderer renderer,
        DartIrTypeMapper typeMapper)
    {
        _namingConverter = namingConverter;
        _renderer = renderer;
        _typeMapper = typeMapper;
    }

    public bool ShouldExecute(GenerationContext context)
        => context.Configuration.GenerateClients && context.IrDocument.Clients.Count > 0;

    public Task ExecuteAsync(GenerationContext context, CancellationToken ct = default)
    {
        var builder = new DioClientAstBuilder(_namingConverter, _typeMapper);
        foreach (var client in context.IrDocument.Clients)
        {
            var file = builder.BuildFile(client);
            var content = _renderer.Render(file);
            var fileBase = _namingConverter.ToSnakeCase(client.Name);
            context.GeneratedFiles.Add(new GeneratedFile($"api/{fileBase}_service.dart", content));
        }
        return Task.CompletedTask;
    }
}
