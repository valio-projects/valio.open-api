using ValIO.Core.Interfaces;
using ValIO.Core.Plugins;
using ValIO.Generators.TypeScript.Angular.Ast.Builders;
using ValIO.Generators.TypeScript.Angular.Ast.Renderers;
using ValIO.Generators.TypeScript.Options;

namespace ValIO.Generators.TypeScript.Steps;

/// <summary>
/// Step: emits an Angular RxJS HttpClient service per <c>IrClient</c>
/// (grouped by tag), with one method per operation returning
/// <c>Observable&lt;{Op}Result&gt;</c>. Unlike per-operation transports
/// (axios/ky/fetch/etc.), Angular's convention is one injectable service
/// class per tag/resource — so this step writes a single
/// <c>{ServiceName}.service.ts</c> file per IrClient.
/// <para/>
/// Activated when <c>options.Transports</c> contains "angular" / "angular-rxjs"
/// (descriptor normalises to <see cref="TypeScriptOptionIds.Transport.AngularRxjs"/>).
/// </summary>
public sealed class AngularRxjsClientStep : ICodeGenerationStep
{
    private readonly INamingConverter _namingConverter;
    private readonly AngularSourceFileRenderer _renderer = new();

    public string Name => "typescript-angular-rxjs-client";
    public int Order => 46;

    public AngularRxjsClientStep(INamingConverter namingConverter)
    {
        _namingConverter = namingConverter ?? throw new ArgumentNullException(nameof(namingConverter));
    }

    public bool ShouldExecute(GenerationContext context)
    {
        if (!context.Configuration.GenerateClients) return false;
        var opts = context.PluginOptions as TypeScriptGenerationOptions;
        return opts?.Transports?.Contains(TypeScriptOptionIds.Transport.AngularRxjs, StringComparer.OrdinalIgnoreCase) == true;
    }

    public Task ExecuteAsync(GenerationContext context, CancellationToken ct = default)
    {
        var opts = context.PluginOptions as TypeScriptGenerationOptions;
        var builder = new AngularServiceAstBuilder(_namingConverter, opts);
        var fileOrg = context.Configuration.FileOrganization;
        var clientsPath = fileOrg.ClientsPath ?? "api";

        foreach (var client in context.IrDocument.Clients)
        {
            ct.ThrowIfCancellationRequested();

            var sourceFile = builder.BuildServiceSourceFile(client, fileOrg, opts);
            var content = _renderer.Render(sourceFile);
            var fileName = builder.ServiceFileName(client.Name);
            // {clientsPath}/{name}Service.service.ts — Angular CLI's `--name` convention
            // would have produced `{name}.service.ts` instead; we follow the task's
            // brief which combines the suffix into the basename for parity with
            // per-operation transports' `{op}.{transport}.ts` shape.
            var relativePath = $"{clientsPath}/{fileName}.service.ts";
            context.GeneratedFiles.Add(new GeneratedFile(relativePath, content));
        }

        return Task.CompletedTask;
    }
}
