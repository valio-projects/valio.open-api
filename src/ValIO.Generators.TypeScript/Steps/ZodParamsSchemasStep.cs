using ValIO.Core.Ast.Nodes;
using ValIO.Core.Interfaces;
using ValIO.Core.IR.Types;
using ValIO.Core.Plugins;
using ValIO.Generators.TypeScript.Ast.Builders;
using ValIO.Generators.TypeScript.Options;
using ValIO.Generators.TypeScript.Zod;

namespace ValIO.Generators.TypeScript.Steps;

/// <summary>
/// Step: генерирует <c>{operation}.params.schema.ts</c> файлы с Zod‑схемами
/// для path/query параметров операции. Активируется когда выбраны
/// <c>validation == "zod3"</c> или <c>"zod4"</c>.
/// </summary>
public sealed class ZodParamsSchemasStep : ICodeGenerationStep
{
    private readonly INamingConverter _namingConverter;

    public string Name => "typescript-zod-params-schemas";
    public int Order => 33;

    public ZodParamsSchemasStep(INamingConverter namingConverter)
    {
        _namingConverter = namingConverter ?? throw new ArgumentNullException(nameof(namingConverter));
    }

    public bool ShouldExecute(GenerationContext context)
    {
        if (!context.Configuration.GenerateClients) return false;
        var opts = context.PluginOptions as TypeScriptGenerationOptions;
        return opts?.ValidationLibrary is "zod3" or "zod4";
    }

    public Task ExecuteAsync(GenerationContext context, CancellationToken ct = default)
    {
        var opts = context.PluginOptions as TypeScriptGenerationOptions;
        var zodVersion = opts?.ValidationLibrary ?? "zod3";
        var astBuilder = new TypeScriptAstBuilder(_namingConverter, opts);
        TypeNode TypeBuilder(IrType type) => astBuilder.BuildType(type);

        var renderer = new ZodSchemaRenderer(_namingConverter, TypeBuilder, zodVersion: zodVersion);

        var fileOrg = context.Configuration.FileOrganization;
        var clientsPath = fileOrg.ClientsPath ?? "api";
        var modelsPath = fileOrg.ModelsPath ?? "models";

        foreach (var client in context.IrDocument.Clients)
        {
            foreach (var operation in client.Operations)
            {
                ct.ThrowIfCancellationRequested();

                var hasRoute = operation.PathParameters.Count > 0;
                var hasQuery = operation.QueryParameters.Count > 0;
                if (!hasRoute && !hasQuery)
                    continue;

                var fileName = _namingConverter.ToCamelCase(operation.MethodName);
                var content = renderer.RenderParamsSchemasFromOperation(
                    operation,
                    operationFileName: fileName,
                    modelsPath: modelsPath);

                var relativePath = $"{clientsPath}/{fileName}/{fileName}.params.schema.ts";
                context.GeneratedFiles.Add(new GeneratedFile(relativePath, content));
            }
        }

        return Task.CompletedTask;
    }
}
