using ValIO.Core.Interfaces;
using ValIO.Core.Plugins;
using ValIO.Generators.Php.Ast.Builders;
using ValIO.Generators.Php.Ast.Renderers;
using ValIO.Generators.Php.Options;

namespace ValIO.Generators.Php.Steps;

/// <summary>
/// Step: генерирует Guzzle-клиент для каждого IR <c>IrClient</c> (по тэгам OpenAPI).
/// </summary>
public sealed class GuzzleClientStep : ICodeGenerationStep
{
    private readonly INamingConverter _namingConverter;
    private readonly IPhpAstRenderer _renderer;
    private readonly PhpIrTypeMapper _typeMapper;

    public string Name => "php-guzzle-client";
    public int Order => 40;

    public GuzzleClientStep(INamingConverter namingConverter, IPhpAstRenderer renderer, PhpIrTypeMapper typeMapper)
    {
        _namingConverter = namingConverter;
        _renderer = renderer;
        _typeMapper = typeMapper;
    }

    public bool ShouldExecute(GenerationContext context)
    {
        if (!context.Configuration.GenerateClients) return false;
        if (context.IrDocument.Clients.Count == 0) return false;
        var opts = context.PluginOptions as PhpGenerationOptions;
        // Если плагин выбран — Guzzle включен по умолчанию; явно отключаем только при laravel.
        return opts == null || string.Equals(opts.UseHttpClient, PhpOptionIds.HttpClient.Guzzle, StringComparison.OrdinalIgnoreCase);
    }

    public Task ExecuteAsync(GenerationContext context, CancellationToken ct = default)
    {
        var opts = context.PluginOptions as PhpGenerationOptions ?? new PhpGenerationOptions();
        var builder = new GuzzleClientAstBuilder(_namingConverter, _typeMapper);

        foreach (var client in context.IrDocument.Clients)
        {
            var sourceFile = builder.BuildSourceFile(client, opts.Namespace);
            var content = _renderer.Render(sourceFile);
            var clientName = string.IsNullOrWhiteSpace(client.Name) ? "ApiClient" : client.Name;
            var fileName = clientName.EndsWith("Api", StringComparison.OrdinalIgnoreCase) ? clientName : clientName + "Api";
            var relativePath = $"Api/{fileName}.php";
            context.GeneratedFiles.Add(new GeneratedFile(relativePath, content));
        }

        if (opts.GenerateComposerJson)
        {
            const string composer = """
{
    "require": {
        "php": ">=8.2",
        "guzzlehttp/guzzle": "^7.0",
        "psr/http-message": "^2.0"
    },
    "autoload": {
        "psr-4": {
            "App\\Generated\\": "."
        }
    }
}
""";
            context.GeneratedFiles.Add(new GeneratedFile("composer.json", composer));
        }

        return Task.CompletedTask;
    }
}
