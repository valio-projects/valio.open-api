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
        var enumNames = document.Enums.Select(e => e.Name).ToHashSet();
        _typeMapper.SetEnumNamesForDocument(enumNames);

        var naming = new UENamingConvention();
        var modelLookup = document.Models.ToDictionary(m => m.Name);

        // 1. Модели → Public/Models/{StructName}.h
        foreach (var model in document.Models)
        {
            var headerNode = _modelBuilder.Build(model, enumNames, modelLookup, result.Warnings);
            result.Files[naming.ModelFullPath(model.Name)] = headerNode;
        }

        // 2. Enums → Public/Enums/{EnumName}.h
        foreach (var irEnum in document.Enums)
        {
            var headerNode = _enumBuilder.Build(irEnum);
            result.Files[naming.EnumFullPath(irEnum.Name)] = headerNode;
        }

        // 3. Per-client: Results, Params, endpoints
        foreach (var client in document.Clients)
        {
            foreach (var operation in client.Operations)
            {
                var opId = operation.MethodName;
                // Params, Result, endpoint header → Public/Endpoints/{OpId}/
                var paramsNode = _paramsBuilder.Build(operation, enumNames, client.Name);
                result.Files[naming.ParamsFullPath(opId)] = paramsNode;

                var resultNode = _resultBuilder.Build(operation, enumNames, client.Name);
                result.Files[naming.ResultFullPath(opId)] = resultNode;

                var (header, source) = _apiBuilder.BuildEndpoint(client, operation, modelLookup);
                result.Files[naming.EndpointHeaderFullPath(opId)] = header;
                result.Files[naming.EndpointSourceFullPath(opId)] = source;
            }
        }

        return result;
    }
}
