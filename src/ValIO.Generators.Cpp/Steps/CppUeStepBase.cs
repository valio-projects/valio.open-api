using ValIO.Core.Plugins;
using ValIO.Generators.Cpp.Ast.Builders;
using ValIO.Generators.Cpp.Ast.Renderers;
using ValIO.Generators.Cpp.Models;

namespace ValIO.Generators.Cpp.Steps;

/// <summary>
/// Общий каркас UE-шагов: рендерер + гейтинг по runtime-профилю.
/// UE-профиль активен, когда опции отсутствуют (defaults) или Runtime == Ue;
/// std-профиль (cpp-httplib + nlohmann_json) отключает все UE-шаги.
/// </summary>
public abstract class CppUeStepBase : ICodeGenerationStep
{
    protected CppUeStepBase(ICppAstRenderer renderer)
    {
        Renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
    }

    /// <summary>Рендерер AST → текст файла.</summary>
    protected ICppAstRenderer Renderer { get; }

    /// <inheritdoc />
    public abstract string Name { get; }

    /// <inheritdoc />
    public abstract int Order { get; }

    /// <inheritdoc />
    public virtual bool ShouldExecute(GenerationContext context)
    {
        var opts = context.PluginOptions as CppGenerationOptions;
        return opts?.Runtime != CppRuntimeProfile.Std;
    }

    /// <inheritdoc />
    public abstract Task ExecuteAsync(GenerationContext context, CancellationToken ct = default);

    /// <summary>
    /// Опции генерации (null-опции → значения по умолчанию = UE-профиль).
    /// </summary>
    protected static CppGenerationOptions EffectiveOptions(GenerationContext context)
        => context.PluginOptions as CppGenerationOptions ?? new CppGenerationOptions();

    /// <summary>
    /// Фасадный билдер с прокинутыми именами enum'ов документа.
    /// </summary>
    protected static CppAstBuilder CreateBuilder(GenerationContext context)
    {
        var builder = new CppAstBuilder(EffectiveOptions(context));
        builder.SetEnumNamesForDocument(context.IrDocument.Enums.Select(e => e.Name).ToHashSet());
        return builder;
    }
}
