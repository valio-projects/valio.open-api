using ValIO.Core.Interfaces;
using ValIO.Core.Plugins;
using ValIO.Generators.Ruby.Ast.Builders;
using ValIO.Generators.Ruby.Ast.Renderers;
using ValIO.Generators.Ruby.Options;

namespace ValIO.Generators.Ruby.Steps;

/// <summary>
/// Generates one Ruby file per IR client. Each file holds a
/// <c>{Tag}Service</c> class that wraps a Faraday connection and exposes one
/// method per operation.
/// </summary>
public sealed class FaradayClientStep : ICodeGenerationStep
{
    private readonly INamingConverter _namingConverter;
    private readonly IRubyAstRenderer _renderer;
    private readonly RubyIrTypeMapper _typeMapper;

    public string Name => "ruby-faraday-client";
    public int Order => 40;

    public FaradayClientStep(
        INamingConverter namingConverter,
        IRubyAstRenderer renderer,
        RubyIrTypeMapper typeMapper)
    {
        _namingConverter = namingConverter;
        _renderer = renderer;
        _typeMapper = typeMapper;
    }

    public bool ShouldExecute(GenerationContext context)
        => context.Configuration.GenerateClients && context.IrDocument.Clients.Count > 0;

    public Task ExecuteAsync(GenerationContext context, CancellationToken ct = default)
    {
        var opts = context.PluginOptions as RubyGenerationOptions ?? new RubyGenerationOptions();
        var apiDir = RubyStepHelper.ApiDir(opts);

        var builder = new FaradayClientAstBuilder(_namingConverter, _typeMapper);
        foreach (var client in context.IrDocument.Clients)
        {
            var file = builder.BuildFile(client);
            var content = _renderer.Render(file);
            var fileName = _namingConverter.ToSnakeCase(client.Name);
            var relativePath = $"{apiDir}/{fileName}_service.rb";
            context.GeneratedFiles.Add(new GeneratedFile(relativePath, content));
        }

        return Task.CompletedTask;
    }
}
