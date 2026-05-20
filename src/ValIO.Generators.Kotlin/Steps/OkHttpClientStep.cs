using ValIO.Core.Interfaces;
using ValIO.Core.Plugins;
using ValIO.Generators.Kotlin.Ast.Builders;
using ValIO.Generators.Kotlin.Ast.Renderers;
using ValIO.Generators.Kotlin.Options;

namespace ValIO.Generators.Kotlin.Steps;

/// <summary>
/// Generates one Kotlin file per IR client (OkHttp + coroutines, suspend methods).
/// </summary>
public sealed class OkHttpClientStep : ICodeGenerationStep
{
    private readonly INamingConverter _namingConverter;
    private readonly IKotlinAstRenderer _renderer;
    private readonly KotlinIrTypeMapper _typeMapper;

    public string Name => "kotlin-okhttp-client";
    public int Order => 40;

    public OkHttpClientStep(
        INamingConverter namingConverter,
        IKotlinAstRenderer renderer,
        KotlinIrTypeMapper typeMapper)
    {
        _namingConverter = namingConverter;
        _renderer = renderer;
        _typeMapper = typeMapper;
    }

    public bool ShouldExecute(GenerationContext context)
        => context.Configuration.GenerateClients && context.IrDocument.Clients.Count > 0;

    public Task ExecuteAsync(GenerationContext context, CancellationToken ct = default)
    {
        var opts = context.PluginOptions as KotlinGenerationOptions ?? new KotlinGenerationOptions();
        var apiPackage = KotlinStepHelper.ApiPackage(opts);
        var apiDir = KotlinStepHelper.PackageToPath(apiPackage);
        var modelsPackage = KotlinStepHelper.ModelsPackage(opts);
        var knownNames = new HashSet<string>(
            context.IrDocument.Models.Select(m => m.Name)
                .Concat(context.IrDocument.Enums.Select(e => e.Name)),
            StringComparer.Ordinal);

        var builder = new OkHttpClientAstBuilder(_namingConverter, _typeMapper);
        foreach (var client in context.IrDocument.Clients)
        {
            var file = builder.BuildFile(client, apiPackage, modelsPackage, knownNames);
            var content = _renderer.Render(file);
            var className = ToClassName(client.Name);
            var relativePath = $"{apiDir}/{className}.kt";
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
