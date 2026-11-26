using ValIO.Core.Interfaces;
using ValIO.Core.IR;
using ValIO.Core.Plugins;
using ValIO.Generators.CSharp.Ast.Builders;
using ValIO.Generators.CSharp.Ast.Renderers;
using ValIO.Generators.CSharp.Options;

namespace ValIO.Generators.CSharp.Steps;

/// <summary>
/// Step: генерирует C# enum из IR enums.
/// </summary>
public sealed class CSharpEnumsStep : ICodeGenerationStep
{
    /// <summary>Converts schema names to C# identifiers.</summary>
    private readonly INamingConverter _namingConverter;
    /// <summary>Renders the generated enum declarations.</summary>
    private readonly ICSharpAstRenderer _renderer;

    /// <inheritdoc />
    public string Name => "csharp-enums";
    /// <inheritdoc />
    public int Order => 20;

    /// <summary>Initializes the enum generation step.</summary>
    /// <param name="namingConverter">The converter for C# identifiers.</param>
    /// <param name="renderer">The renderer for generated enum source.</param>
    public CSharpEnumsStep(INamingConverter namingConverter, ICSharpAstRenderer renderer)
    {
        _namingConverter = namingConverter;
        _renderer = renderer;
    }

    /// <inheritdoc />
    public bool ShouldExecute(GenerationContext context)
        => context.Configuration.GenerateEnums && context.IrDocument.Enums.Count > 0;

    /// <inheritdoc/>
    public Task ExecuteAsync(GenerationContext context, CancellationToken ct = default)
    {
        var opts = context.PluginOptions as CSharpGenerationOptions;
        var ns = opts?.Namespace ?? context.Configuration.Namespace ?? "Generated";
        var builder = new CSharpAstBuilder(_namingConverter, context.DocumentContext, opts);

        foreach (var irEnum in context.IrDocument.Enums)
        {
            var compilationUnit = builder.BuildCompilationUnit(irEnum, ns);
            var content = _renderer.Render(compilationUnit);
            var relativePath = $"Models/{irEnum.OriginalName}.cs";
            context.GeneratedFiles.Add(new GeneratedFile(relativePath, content));
        }

        if (opts?.StrictStringEnums == true && context.IrDocument.Enums.Any(model => model.BaseType == IrEnumBaseType.String))
        {
            context.GeneratedFiles.Add(new GeneratedFile(
                "Serialization/ValioStrictStringEnumConverter.cs", CSharpRuntimeSource.Render("StrictStringEnumConverter.cs", ns)));
        }

        return Task.CompletedTask;
    }
}
