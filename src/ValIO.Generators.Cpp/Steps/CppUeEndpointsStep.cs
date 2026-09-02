using ValIO.Core.Plugins;
using ValIO.Generators.Cpp.Ast.Builders;
using ValIO.Generators.Cpp.Ast.Renderers;
using ValIO.Generators.Cpp.Services;

namespace ValIO.Generators.Cpp.Steps;

/// <summary>
/// Endpoint-файлы UE-профиля: для каждой операции — Params.h, Result.h
/// (Public/Endpoints/{OpId}/) и endpoint .h/.cpp. Пути строятся по ORIGINAL имени
/// операции, чтобы не менялись при affix-преобразованиях (тот же контракт, что у CppAstBuilder.Build).
/// </summary>
public sealed class CppUeEndpointsStep(ICppAstRenderer renderer) : CppUeStepBase(renderer)
{
    public override string Name => "cpp-ue-endpoints";
    public override int Order => 30;

    public override bool ShouldExecute(GenerationContext context)
        => base.ShouldExecute(context)
           && context.Configuration.GenerateClients
           && context.IrDocument.Clients.Count > 0;

    public override Task ExecuteAsync(GenerationContext context, CancellationToken ct = default)
    {
        var builder = CreateBuilder(context);
        var naming = new UENamingConvention();
        var (_, modelLookup) = CppAstBuilder.PrepareInputs(context.IrDocument);
        var enumNames = context.IrDocument.Enums.Select(e => e.Name).ToHashSet();

        foreach (var client in context.IrDocument.Clients)
        {
            foreach (var operation in client.Operations)
            {
                ct.ThrowIfCancellationRequested();
                var opId = operation.OriginalMethodName;

                var paramsHeader = builder.BuildParamsHeader(operation, enumNames, client.Name);
                context.GeneratedFiles.Add(new GeneratedFile(
                    naming.ParamsFullPath(opId), Renderer.Render(paramsHeader)));

                var resultHeader = builder.BuildResultHeader(operation, enumNames, client.Name);
                context.GeneratedFiles.Add(new GeneratedFile(
                    naming.ResultFullPath(opId), Renderer.Render(resultHeader)));

                var (endpointHeader, endpointSource) = builder.BuildEndpointFiles(client, operation, modelLookup);
                context.GeneratedFiles.Add(new GeneratedFile(
                    naming.EndpointHeaderFullPath(opId), Renderer.Render(endpointHeader)));
                context.GeneratedFiles.Add(new GeneratedFile(
                    naming.EndpointSourceFullPath(opId), Renderer.Render(endpointSource)));
            }
        }

        return Task.CompletedTask;
    }
}
