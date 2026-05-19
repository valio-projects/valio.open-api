using ValIO.Core.Interfaces;
using ValIO.Core.Plugins;
using ValIO.Generators.TypeScript.Ast.Renderers;
using ValIO.Generators.TypeScript.OFetch.Ast.Builders;
using ValIO.Generators.TypeScript.Options;

namespace ValIO.Generators.TypeScript.Steps;

/// <summary>
/// Step: emits an ofetch client (<c>{op}.ofetch.ts</c>) per IR operation via AST.
/// Activated when <c>options.Transports</c> contains "ofetch".
/// </summary>
public sealed class OFetchClientStep : ICodeGenerationStep
{
    private readonly INamingConverter _namingConverter;
    private readonly TypeScriptAstRenderer _renderer = new();

    public string Name => "typescript-ofetch-client";
    public int Order => 44;

    public OFetchClientStep(INamingConverter namingConverter)
    {
        _namingConverter = namingConverter ?? throw new ArgumentNullException(nameof(namingConverter));
    }

    public bool ShouldExecute(GenerationContext context)
    {
        if (!context.Configuration.GenerateClients) return false;
        var opts = context.PluginOptions as TypeScriptGenerationOptions;
        return opts?.Transports?.Contains(TypeScriptOptionIds.Transport.OFetch, StringComparer.OrdinalIgnoreCase) == true;
    }

    public Task ExecuteAsync(GenerationContext context, CancellationToken ct = default)
    {
        var opts = context.PluginOptions as TypeScriptGenerationOptions;
        var builder = new OFetchClientAstBuilder(_namingConverter, opts);
        var fileOrg = context.Configuration.FileOrganization;
        var clientsPath = fileOrg.ClientsPath ?? "api";

        foreach (var client in context.IrDocument.Clients)
        {
            foreach (var operation in client.Operations)
            {
                ct.ThrowIfCancellationRequested();

                var sourceFile = builder.BuildOperationClientSourceFile(operation, fileOrg);
                var content = _renderer.Render(sourceFile);
                var fileName = _namingConverter.ToCamelCase(operation.MethodName);
                var relativePath = $"{clientsPath}/{fileName}/{fileName}.ofetch.ts";
                context.GeneratedFiles.Add(new GeneratedFile(relativePath, content));
            }
        }

        return Task.CompletedTask;
    }
}
