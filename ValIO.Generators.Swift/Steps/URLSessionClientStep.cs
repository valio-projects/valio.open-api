using ValIO.Core.Interfaces;
using ValIO.Core.Plugins;
using ValIO.Generators.Swift.Ast.Builders;
using ValIO.Generators.Swift.Ast.Renderers;
using ValIO.Generators.Swift.Options;

namespace ValIO.Generators.Swift.Steps;

/// <summary>
/// Generates one Swift file per IR client (URLSession + async/await).
/// </summary>
public sealed class URLSessionClientStep : ICodeGenerationStep
{
    private readonly INamingConverter _namingConverter;
    private readonly ISwiftAstRenderer _renderer;
    private readonly SwiftIrTypeMapper _typeMapper;

    public string Name => "swift-urlsession-client";
    public int Order => 40;

    public URLSessionClientStep(
        INamingConverter namingConverter,
        ISwiftAstRenderer renderer,
        SwiftIrTypeMapper typeMapper)
    {
        _namingConverter = namingConverter;
        _renderer = renderer;
        _typeMapper = typeMapper;
    }

    public bool ShouldExecute(GenerationContext context)
        => context.Configuration.GenerateClients && context.IrDocument.Clients.Count > 0;

    public Task ExecuteAsync(GenerationContext context, CancellationToken ct = default)
    {
        var opts = context.PluginOptions as SwiftGenerationOptions ?? new SwiftGenerationOptions();
        var apiDir = SwiftStepHelper.ApiDir(opts);

        var builder = new URLSessionClientAstBuilder(_namingConverter, _typeMapper);
        foreach (var client in context.IrDocument.Clients)
        {
            var file = builder.BuildFile(client);
            var content = _renderer.Render(file);
            var className = ToClassName(client.Name);
            var relativePath = $"{apiDir}/{className}.swift";
            context.GeneratedFiles.Add(new GeneratedFile(relativePath, content));
        }

        return Task.CompletedTask;
    }

    private string ToClassName(string clientName)
    {
        var pascal = _namingConverter.ToPascalCase(clientName);
        return pascal.EndsWith("Api", StringComparison.Ordinal) ? pascal : pascal + "Api";
    }
}
