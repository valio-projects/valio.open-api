using ValIO.Core.Interfaces;
using ValIO.Core.Plugins;
using ValIO.Generators.Python.Ast.Builders;
using ValIO.Generators.Python.Ast.Renderers;

namespace ValIO.Generators.Python.Steps;

/// <summary>
/// Генерирует один Pydantic-модуль на каждую IR-модель.
/// </summary>
public sealed class PythonModelsStep : ICodeGenerationStep
{
    private readonly INamingConverter _namingConverter;
    private readonly IPythonAstRenderer _renderer;
    private readonly PythonIrTypeMapper _typeMapper;

    public string Name => "python-models";
    public int Order => 10;

    public PythonModelsStep(
        INamingConverter namingConverter,
        IPythonAstRenderer renderer,
        PythonIrTypeMapper typeMapper)
    {
        _namingConverter = namingConverter;
        _renderer = renderer;
        _typeMapper = typeMapper;
    }

    public bool ShouldExecute(GenerationContext context)
        => context.Configuration.GenerateModels && context.IrDocument.Models.Count > 0;

    public Task ExecuteAsync(GenerationContext context, CancellationToken ct = default)
    {
        // Models И enums попадают в .models/* — для forward-ref импорта нужны оба.
        var modelModuleByName = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var kvp in PythonStepHelper.BuildModelModuleMap(context.IrDocument, _namingConverter))
            modelModuleByName[kvp.Key] = kvp.Value;
        foreach (var kvp in PythonStepHelper.BuildEnumModuleMap(context.IrDocument, _namingConverter))
            modelModuleByName[kvp.Key] = kvp.Value;
        var builder = new PydanticModelAstBuilder(_namingConverter, _typeMapper);

        var orderedNames = context.IrDocument.TypeDependencies.TopologicalOrder;
        var byName = context.IrDocument.Models.ToDictionary(m => m.Name);
        var processed = new HashSet<string>();

        foreach (var name in orderedNames)
        {
            if (!byName.TryGetValue(name, out var model)) continue;
            AddModelFile(context, builder, model, modelModuleByName);
            processed.Add(name);
        }

        foreach (var model in context.IrDocument.Models)
        {
            if (!processed.Contains(model.Name))
                AddModelFile(context, builder, model, modelModuleByName);
        }

        return Task.CompletedTask;
    }

    private void AddModelFile(
        GenerationContext context,
        PydanticModelAstBuilder builder,
        ValIO.Core.IR.IrModel model,
        IReadOnlyDictionary<string, string> modelModuleByName)
    {
        var module = builder.BuildModule(model, modelModuleByName);
        var content = _renderer.Render(module);
        var fileName = _namingConverter.ToSnakeCase(model.Name);
        var relativePath = $"models/{fileName}.py";
        context.GeneratedFiles.Add(new GeneratedFile(relativePath, content));
    }
}
