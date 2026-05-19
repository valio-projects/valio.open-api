using ValIO.Core.Interfaces;
using ValIO.Core.Plugins;
using ValIO.Generators.TypeScript.Ast.Renderers;
using ValIO.Generators.TypeScript.Options;
using ValIO.Generators.TypeScript.Undici.Ast.Builders;

namespace ValIO.Generators.TypeScript.Steps;

/// <summary>
/// Step: emits an undici client (<c>{op}.undici.ts</c>) per IR operation via AST.
/// Activated when <c>options.Transports</c> contains "undici".
/// </summary>
public sealed class UndiciClientStep : ICodeGenerationStep
{
    private readonly INamingConverter _namingConverter;
    private readonly TypeScriptAstRenderer _renderer = new();

    public string Name => "typescript-undici-client";
    public int Order => 43;

    public UndiciClientStep(INamingConverter namingConverter)
    {
        _namingConverter = namingConverter ?? throw new ArgumentNullException(nameof(namingConverter));
    }

    public bool ShouldExecute(GenerationContext context)
    {
        if (!context.Configuration.GenerateClients) return false;
        var opts = context.PluginOptions as TypeScriptGenerationOptions;
        return opts?.Transports?.Contains(TypeScriptOptionIds.Transport.Undici, StringComparer.OrdinalIgnoreCase) == true;
    }

    public Task ExecuteAsync(GenerationContext context, CancellationToken ct = default)
    {
        var opts = context.PluginOptions as TypeScriptGenerationOptions;
        var builder = new UndiciClientAstBuilder(_namingConverter, opts);
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
                var relativePath = $"{clientsPath}/{fileName}/{fileName}.undici.ts";
                context.GeneratedFiles.Add(new GeneratedFile(relativePath, content));
            }
        }

        return Task.CompletedTask;
    }
}
