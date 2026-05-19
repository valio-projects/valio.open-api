using ValIO.Core.Interfaces;
using ValIO.Core.Plugins;
using ValIO.Generators.Python.Ast.Builders;
using ValIO.Generators.Python.Ast.Renderers;
using ValIO.Generators.Python.Options;

namespace ValIO.Generators.Python.Steps;

/// <summary>
/// Генерирует HTTPX-клиент (по одному модулю на IrClient).
/// </summary>
public sealed class HttpxClientStep : ICodeGenerationStep
{
    private readonly INamingConverter _namingConverter;
    private readonly IPythonAstRenderer _renderer;
    private readonly PythonIrTypeMapper _typeMapper;

    public string Name => "python-httpx-client";
    public int Order => 40;

    public HttpxClientStep(
        INamingConverter namingConverter,
        IPythonAstRenderer renderer,
        PythonIrTypeMapper typeMapper)
    {
        _namingConverter = namingConverter;
        _renderer = renderer;
        _typeMapper = typeMapper;
    }

    public bool ShouldExecute(GenerationContext context)
        => context.Configuration.GenerateClients && context.IrDocument.Clients.Count > 0;

    public Task ExecuteAsync(GenerationContext context, CancellationToken ct = default)
    {
        var opts = context.PluginOptions as PythonGenerationOptions ?? new PythonGenerationOptions();
        var modelModuleByName = PythonStepHelper.BuildModelModuleMapForClient(context.IrDocument, _namingConverter);
        var enumModuleByName = PythonStepHelper.BuildEnumModuleMapForClient(context.IrDocument, _namingConverter);
        var builder = new HttpxClientAstBuilder(_namingConverter, _typeMapper);

        foreach (var client in context.IrDocument.Clients)
        {
            var module = builder.BuildModule(client, modelModuleByName, enumModuleByName, opts);
            var content = _renderer.Render(module);
            var fileName = _namingConverter.ToSnakeCase(client.Name);
            var relativePath = $"api/{fileName}_api.py";
            context.GeneratedFiles.Add(new GeneratedFile(relativePath, content));
        }

        return Task.CompletedTask;
    }
}
