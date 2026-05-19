using ValIO.Core.Interfaces;
using ValIO.Core.IR.Types;
using ValIO.Core.Plugins;
using ValIO.Generators.TypeScript.Ast.Builders;
using ValIO.Generators.TypeScript.Ast.Renderers;
using ValIO.Generators.TypeScript.Options;

namespace ValIO.Generators.TypeScript.Steps;

/// <summary>
/// Step: генерирует <c>{operation}.results.ts</c> файлы рядом с клиентом
/// операции. Каждый файл содержит discriminated union типа результата
/// (varianty по status code).
/// </summary>
public sealed class TypeScriptResultsStep : ICodeGenerationStep
{
    private readonly INamingConverter _namingConverter;
    private readonly TypeScriptAstRenderer _renderer = new();

    public string Name => "typescript-operation-results";
    public int Order => 16;

    public TypeScriptResultsStep(INamingConverter namingConverter)
    {
        _namingConverter = namingConverter ?? throw new ArgumentNullException(nameof(namingConverter));
    }

    public bool ShouldExecute(GenerationContext context)
        => context.Configuration.GenerateClients && context.IrDocument.Clients.Count > 0;

    public Task ExecuteAsync(GenerationContext context, CancellationToken ct = default)
    {
        var opts = context.PluginOptions as TypeScriptGenerationOptions;
        var astBuilder = new TypeScriptAstBuilder(_namingConverter, opts);
        var resultsBuilder = new TypeScriptResultsAstBuilder(_namingConverter, astBuilder.BuildType);
        var fileOrg = context.Configuration.FileOrganization;
        var clientsPath = fileOrg.ClientsPath ?? "api";

        foreach (var client in context.IrDocument.Clients)
        {
            foreach (var operation in client.Operations)
            {
                ct.ThrowIfCancellationRequested();

                if (!operation.Responses.Any(r => r.Type != null))
                    continue;

                var fileName = _namingConverter.ToCamelCase(operation.MethodName);
                var sourceFile = resultsBuilder.BuildResultsSourceFile(
                    operation,
                    importPath: null,
                    fileOrganization: fileOrg,
                    currentOperationName: operation.MethodName);
                var content = _renderer.Render(sourceFile);
                var relativePath = $"{clientsPath}/{fileName}/{fileName}.results.ts";
                context.GeneratedFiles.Add(new GeneratedFile(relativePath, content));
            }
        }

        return Task.CompletedTask;
    }
}
