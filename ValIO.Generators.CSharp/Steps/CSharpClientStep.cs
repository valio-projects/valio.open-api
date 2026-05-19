using ValIO.Core.Interfaces;
using ValIO.Core.Plugins;
using ValIO.Generators.CSharp.Ast.Builders;
using ValIO.Generators.CSharp.Ast.Renderers;
using ValIO.Generators.CSharp.Options;

namespace ValIO.Generators.CSharp.Steps;

/// <summary>
/// Step: генерирует C# HTTP-клиент из IR clients.
/// </summary>
public sealed class CSharpClientStep : ICodeGenerationStep
{
    private readonly INamingConverter _namingConverter;
    private readonly ICSharpAstRenderer _renderer;

    public string Name => "csharp-client";
    public int Order => 40;

    public CSharpClientStep(INamingConverter namingConverter, ICSharpAstRenderer renderer)
    {
        _namingConverter = namingConverter;
        _renderer = renderer;
    }

    public bool ShouldExecute(GenerationContext context)
        => context.Configuration.GenerateClients && context.IrDocument.Clients.Count > 0;

    public Task ExecuteAsync(GenerationContext context, CancellationToken ct = default)
    {
        var opts = context.PluginOptions as CSharpGenerationOptions;
        var ns = opts?.Namespace ?? context.Configuration.Namespace ?? "Generated";
        var builder = new CSharpAstBuilder(_namingConverter);

        foreach (var client in context.IrDocument.Clients)
        {
            var compilationUnit = builder.BuildCompilationUnit(client, ns);
            var content = _renderer.Render(compilationUnit);
            var clientName = string.IsNullOrWhiteSpace(client.Name) ? "ApiClient" : client.Name;
            var relativePath = $"Api/{clientName}Client.cs";
            context.GeneratedFiles.Add(new GeneratedFile(relativePath, content));
        }

        return Task.CompletedTask;
    }
}
