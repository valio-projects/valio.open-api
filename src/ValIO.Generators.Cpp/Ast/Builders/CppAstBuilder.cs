using ValIO.Core.IR;
using ValIO.Generators.Cpp.Ast.Nodes;
using ValIO.Generators.Cpp.Models;
using ValIO.Generators.Cpp.Services;

namespace ValIO.Generators.Cpp.Ast.Builders;

/// <summary>
/// Координирующий фасадный строитель, который управляет всеми sub-builders
/// и производит полный набор AST узлов из IrDocument.
/// </summary>
public sealed class CppAstBuilder
{
    private readonly UETypeMapper _typeMapper;
    private readonly UEModelAstBuilder _modelBuilder;
    private readonly UEEnumAstBuilder _enumBuilder;
    private readonly UEResultAstBuilder _resultBuilder;
    private readonly UEParamsAstBuilder _paramsBuilder;
    private readonly UEApiAstBuilder _apiBuilder;

    public CppAstBuilder(CppGenerationOptions options)
    {
        var naming = new UENamingConvention();
        _typeMapper = new UETypeMapper(naming);

        _modelBuilder = new UEModelAstBuilder(_typeMapper, naming, options);
        _enumBuilder = new UEEnumAstBuilder(naming, options);
        _resultBuilder = new UEResultAstBuilder(_typeMapper, naming, options);
        _paramsBuilder = new UEParamsAstBuilder(_typeMapper, naming, options);
        _apiBuilder = new UEApiAstBuilder(_typeMapper, naming, options, _resultBuilder);
    }

    /// <summary>
    /// Результат сборки: словарь файлового пути → AST-узла
    /// </summary>
    public sealed class BuildResult
    {
        /// <summary>
        /// Все сгенерированные файлы: относительный путь → AST-узел верхнего уровня
        /// </summary>
        public Dictionary<string, Core.Ast.Nodes.AstNode> Files { get; } = new();

        /// <summary>
        /// Предупреждения при сборке (например, пропуск рекурсивных свойств)
        /// </summary>
        public List<string> Warnings { get; } = new();
    }

    /// <summary>
    /// Строит полный набор AST узлов из IrDocument
    /// </summary>
    public BuildResult Build(IrDocument document)
    {
        var result = new BuildResult();
        var (enumNames, modelLookup) = PrepareInputs(document);
        _typeMapper.SetEnumNamesForDocument(enumNames);

        var naming = new UENamingConvention();

        // 1. Модели → Public/Models/{StructName}.h
        foreach (var model in document.Models)
        {
            var headerNode = BuildModelHeader(model, enumNames, modelLookup, result.Warnings);
            result.Files[naming.ModelFullPath(model.OriginalName)] = headerNode;
        }

        // 2. Enums → Public/Enums/{EnumName}.h
        foreach (var irEnum in document.Enums)
        {
            var headerNode = BuildEnumHeader(irEnum);
            result.Files[naming.EnumFullPath(irEnum.OriginalName)] = headerNode;
        }

        // 3. Per-client: Results, Params, endpoints
        foreach (var client in document.Clients)
        {
            foreach (var operation in client.Operations)
            {
                var opId = operation.OriginalMethodName;
                // Params, Result, endpoint header → Public/Endpoints/{OpId}/ (use ORIGINAL name so file paths don't change with affixes)
                result.Files[naming.ParamsFullPath(opId)] = BuildParamsHeader(operation, enumNames, client.Name);
                result.Files[naming.ResultFullPath(opId)] = BuildResultHeader(operation, enumNames, client.Name);

                var (header, source) = BuildEndpointFiles(client, operation, modelLookup);
                result.Files[naming.EndpointHeaderFullPath(opId)] = header;
                result.Files[naming.EndpointSourceFullPath(opId)] = source;
            }
        }

        return result;
    }

    // ===================================================================
    // Гранулярные методы: по одному файлу/сущности — используются
    // pipeline-шагами (CppUe*Step) и монолитным Build() выше,
    // поэтому оба пути дают идентичный вывод.
    // ===================================================================

    /// <summary>
    /// Общие входы для генерации: имена enum'ов (чтобы отличать ссылки на enum
    /// от ссылок на struct) и словарь моделей по имени (для раскрытия allOf).
    /// </summary>
    public static (IReadOnlySet<string> EnumNames, IReadOnlyDictionary<string, IrModel> ModelLookup) PrepareInputs(IrDocument document)
        => (document.Enums.Select(e => e.Name).ToHashSet(), document.Models.ToDictionary(m => m.Name));

    /// <summary>
    /// Прокидывает множество имён enum'ов в type mapper — обязан быть вызван
    /// до BuildModelHeader/BuildParamsHeader/BuildResultHeader/BuildEndpointFiles,
    /// если конструктор фасада создан вручную (Build() делает это сам).
    /// </summary>
    public void SetEnumNamesForDocument(IReadOnlySet<string> enumNames) => _typeMapper.SetEnumNamesForDocument(enumNames);

    /// <summary>Хедер модели: Public/Models/{Model}.h (USTRUCT).</summary>
    public CppHeaderFileNode BuildModelHeader(IrModel model, IReadOnlySet<string> enumNames, IReadOnlyDictionary<string, IrModel> modelLookup, ICollection<string>? warnings = null)
        => _modelBuilder.Build(model, enumNames, modelLookup, warnings);

    /// <summary>Хедер enum'а: Public/Enums/{Enum}.h (UENUM).</summary>
    public CppHeaderFileNode BuildEnumHeader(IrEnum irEnum) => _enumBuilder.Build(irEnum);

    /// <summary>Хедер Params-структуры операции.</summary>
    public CppHeaderFileNode BuildParamsHeader(IrOperation operation, IReadOnlySet<string> enumNames, string clientName)
        => _paramsBuilder.Build(operation, enumNames, clientName);

    /// <summary>Хедер Result-структуры операции.</summary>
    public CppHeaderFileNode BuildResultHeader(IrOperation operation, IReadOnlySet<string> enumNames, string clientName)
        => _resultBuilder.Build(operation, enumNames, clientName);

    /// <summary>Endpoint-файлы операции (.h + .cpp).</summary>
    public (CppHeaderFileNode Header, CppSourceFileNode Source) BuildEndpointFiles(IrClient client, IrOperation operation, IReadOnlyDictionary<string, IrModel> modelLookup)
        => _apiBuilder.BuildEndpoint(client, operation, modelLookup);
}
