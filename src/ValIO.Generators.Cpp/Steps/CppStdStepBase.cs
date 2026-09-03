using ValIO.Core.Plugins;
using ValIO.Generators.Cpp.Ast.Renderers;
using ValIO.Generators.Cpp.Models;
using ValIO.Generators.Cpp.Services;

namespace ValIO.Generators.Cpp.Steps;

/// <summary>
/// Общий каркас std-шагов (cpp-httplib + nlohmann_json): активны только при
/// Runtime == Std (без опций работает UE-профиль). Формирует mapper/нейминг
/// с прокинутыми именами enum'ов и дренажирует warnings маппера в context.Warnings.
/// </summary>
public abstract class CppStdStepBase : ICodeGenerationStep
{
    protected CppStdStepBase(ICppAstRenderer renderer)
    {
        Renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
    }

    protected ICppAstRenderer Renderer { get; }

    /// <inheritdoc />
    public abstract string Name { get; }

    /// <inheritdoc />
    public abstract int Order { get; }

    /// <inheritdoc />
    public virtual bool ShouldExecute(GenerationContext context)
    {
        var opts = context.PluginOptions as CppGenerationOptions;
        return opts?.Runtime == CppRuntimeProfile.Std;
    }

    /// <inheritdoc />
    public abstract Task ExecuteAsync(GenerationContext context, CancellationToken ct = default);

    protected static CppGenerationOptions EffectiveOptions(GenerationContext context)
        => context.PluginOptions as CppGenerationOptions ?? new CppGenerationOptions { Runtime = CppRuntimeProfile.Std };

    /// <summary>
    /// Корневой namespace: опция «namespace» (валидный C++ идентификатор) либо «Api».
    /// Configuration.Namespace не используется — это C#-формат (точечные сегменты).
    /// </summary>
    protected static string ResolveRootNamespace(GenerationContext context)
    {
        var opts = EffectiveOptions(context);
        return string.IsNullOrWhiteSpace(opts.Namespace) ? "Api" : new StdNamingConvention().Namespace(opts.Namespace);
    }

    /// <summary>Связка нейминга и маппера, сконфигурированная под документ.</summary>
    protected static (StdNamingConvention Naming, StdCppTypeMapper Mapper) CreateServices(GenerationContext context)
    {
        var naming = new StdNamingConvention();
        var mapper = new StdCppTypeMapper(naming);
        mapper.SetEnumNamesForDocument(context.IrDocument.Enums.Select(e => e.Name).ToHashSet());
        return (naming, mapper);
    }

    /// <summary>Переносит warnings маппера (union/any/…) в диагностику прогона (с дедупом).</summary>
    protected static void DrainWarnings(GenerationContext context, StdCppTypeMapper mapper)
    {
        // Шаги создают собственные мапперы с одинаковыми warnings — дедуп на уровне прогона.
        foreach (var warning in mapper.Warnings.Distinct())
        {
            if (!context.Warnings.Contains(warning))
                context.Warnings.Add(warning);
        }
    }
}
