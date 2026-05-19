using ValIO.Core.Interfaces;
using ValIO.Core.Plugins;
using ValIO.Generators.TypeScript.Ast.Builders;
using ValIO.Generators.TypeScript.Ast.Renderers;
using ValIO.Generators.TypeScript.Options;

namespace ValIO.Generators.TypeScript.Steps;

/// <summary>
/// Step: генерирует axios-клиент для каждой операции.
/// Активируется если в options.Transports содержится "axios".
/// </summary>
public sealed class AxiosClientStep : ICodeGenerationStep
{
    private readonly INamingConverter _namingConverter;
    private readonly TypeScriptAstRenderer _renderer;

    public string Name => "typescript-axios-client";
    public int Order => 41;

    public AxiosClientStep(INamingConverter namingConverter)
        : this(namingConverter, Array.Empty<ITypeScriptNodeRenderer>())
    {
    }

    public AxiosClientStep(INamingConverter namingConverter, IEnumerable<ITypeScriptNodeRenderer> nodeRenderers)
    {
        _namingConverter = namingConverter;
        _renderer = new TypeScriptAstRenderer(nodeRenderers);
    }

    public bool ShouldExecute(GenerationContext context)
    {
        if (!context.Configuration.GenerateClients) return false;
        var opts = context.PluginOptions as TypeScriptGenerationOptions;
        return opts?.Transports?.Contains("axios", StringComparer.OrdinalIgnoreCase) == true;
    }

    public Task ExecuteAsync(GenerationContext context, CancellationToken ct = default)
    {
        var opts = context.PluginOptions as TypeScriptGenerationOptions;
        var builder = new TypeScriptAstBuilder(_namingConverter, opts);
        var fileOrg = context.Configuration.FileOrganization;
        var clientsPath = fileOrg.ClientsPath ?? "api";

        foreach (var client in context.IrDocument.Clients)
        {
            foreach (var operation in client.Operations)
            {
                var sourceFile = builder.BuildSourceFileForOperation(
                    operation, fileOrganization: fileOrg, transport: "axios");
                var content = _renderer.Render(sourceFile);
                var fileName = _namingConverter.ToCamelCase(operation.MethodName);
                var relativePath = $"{clientsPath}/{fileName}/{fileName}.axios.ts";
                context.GeneratedFiles.Add(new GeneratedFile(relativePath, content));
            }
        }

        return Task.CompletedTask;
    }
}
