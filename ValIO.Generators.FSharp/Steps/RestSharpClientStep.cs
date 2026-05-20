using ValIO.Core.Interfaces;
using ValIO.Core.Plugins;
using ValIO.Generators.FSharp.Ast.Builders;
using ValIO.Generators.FSharp.Ast.Renderers;
using ValIO.Generators.FSharp.Options;

namespace ValIO.Generators.FSharp.Steps;

/// <summary>
/// Generates one F# file per IR client. Each operation becomes a top-level
/// <c>let</c>-bound function that issues the request via RestSharp.
/// </summary>
public sealed class RestSharpClientStep : ICodeGenerationStep
{
    private readonly INamingConverter _namingConverter;
    private readonly IFSharpAstRenderer _renderer;
    private readonly FSharpIrTypeMapper _typeMapper;

    public string Name => "fsharp-restsharp-client";
    public int Order => 40;

    public RestSharpClientStep(
        INamingConverter namingConverter,
        IFSharpAstRenderer renderer,
        FSharpIrTypeMapper typeMapper)
    {
        _namingConverter = namingConverter;
        _renderer = renderer;
        _typeMapper = typeMapper;
    }

    public bool ShouldExecute(GenerationContext context)
        => context.Configuration.GenerateClients && context.IrDocument.Clients.Count > 0;

    public Task ExecuteAsync(GenerationContext context, CancellationToken ct = default)
    {
        var opts = context.PluginOptions as FSharpGenerationOptions ?? new FSharpGenerationOptions();
        var builder = new RestSharpClientAstBuilder(_namingConverter, _typeMapper);

        foreach (var client in context.IrDocument.Clients)
        {
            var typeName = ToClientFileName(client.Name);
            var moduleName = FSharpStepHelper.ApiModuleName(opts, typeName);
            var file = builder.BuildFile(client, moduleName);
            var content = _renderer.Render(file);
            var relativePath = $"{FSharpStepHelper.ApiDir}/{typeName}.fs";
            context.GeneratedFiles.Add(new GeneratedFile(relativePath, content));
        }

        return Task.CompletedTask;
    }

    private string ToClientFileName(string clientName)
    {
        var pascal = _namingConverter.ToPascalCase(string.IsNullOrWhiteSpace(clientName) ? "Default" : clientName);
        return pascal.EndsWith("Client", StringComparison.Ordinal) ? pascal : pascal + "Client";
    }
}
