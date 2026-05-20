using ValIO.Core.Interfaces;
using ValIO.Core.Plugins;
using ValIO.Generators.Rust.Ast.Builders;
using ValIO.Generators.Rust.Ast.Renderers;
using ValIO.Generators.Rust.Options;

namespace ValIO.Generators.Rust.Steps;

/// <summary>
/// Generates one Rust file per IR client under <c>{crate}/src/api/{snake_tag}.rs</c>,
/// containing async <c>fn</c>s that call <c>reqwest::Client</c>.
/// </summary>
public sealed class ReqwestClientStep : ICodeGenerationStep
{
    private readonly INamingConverter _namingConverter;
    private readonly IRustAstRenderer _renderer;
    private readonly RustIrTypeMapper _typeMapper;

    public string Name => "rust-reqwest-client";
    public int Order => 40;

    public ReqwestClientStep(
        INamingConverter namingConverter,
        IRustAstRenderer renderer,
        RustIrTypeMapper typeMapper)
    {
        _namingConverter = namingConverter;
        _renderer = renderer;
        _typeMapper = typeMapper;
    }

    public bool ShouldExecute(GenerationContext context)
        => context.Configuration.GenerateClients && context.IrDocument.Clients.Count > 0;

    public Task ExecuteAsync(GenerationContext context, CancellationToken ct = default)
    {
        var opts = context.PluginOptions as RustGenerationOptions ?? new RustGenerationOptions();
        var apiDir = RustStepHelper.ApiDir(opts);

        var builder = new ReqwestClientAstBuilder(_namingConverter, _typeMapper);
        foreach (var client in context.IrDocument.Clients)
        {
            var file = builder.BuildFile(client);
            var content = _renderer.Render(file);
            var snake = _namingConverter.ToSnakeCase(client.Name);
            var relativePath = $"{apiDir}/{snake}.rs";
            context.GeneratedFiles.Add(new GeneratedFile(relativePath, content));
        }

        return Task.CompletedTask;
    }
}
