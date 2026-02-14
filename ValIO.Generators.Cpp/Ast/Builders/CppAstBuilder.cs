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
    private readonly UEModelAstBuilder _modelBuilder;
    private readonly UEEnumAstBuilder _enumBuilder;
    private readonly UEResultAstBuilder _resultBuilder;
    private readonly UEParamsAstBuilder _paramsBuilder;
    private readonly UEApiAstBuilder _apiBuilder;

    public CppAstBuilder(CppGenerationOptions options)
    {
        var naming = new UENamingConvention();
        var typeMapper = new UETypeMapper(naming);

        _modelBuilder = new UEModelAstBuilder(typeMapper, naming, options);
        _enumBuilder = new UEEnumAstBuilder(naming, options);
        _resultBuilder = new UEResultAstBuilder(typeMapper, naming, options);
        _paramsBuilder = new UEParamsAstBuilder(typeMapper, naming, options);
        _apiBuilder = new UEApiAstBuilder(typeMapper, naming, options, _resultBuilder);
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
    }

    /// <summary>
    /// Строит полный набор AST узлов из IrDocument
    /// </summary>
    public BuildResult Build(IrDocument document)
    {
        var result = new BuildResult();

        // 1. Модели → Models/{StructName}.h
        foreach (var model in document.Models)
        {
            var headerNode = _modelBuilder.Build(model);
            result.Files[$"Models/{headerNode.FileName}"] = headerNode;
        }

        // 2. Enums → Enums/{EnumName}.h
        foreach (var irEnum in document.Enums)
        {
            var headerNode = _enumBuilder.Build(irEnum);
            result.Files[$"Enums/{headerNode.FileName}"] = headerNode;
        }

        // 3. Per-client: Results, Params, API files
        foreach (var client in document.Clients)
        {
            // Results → Results/{ResultName}.h
            foreach (var operation in client.Operations)
            {
                var resultNode = _resultBuilder.Build(operation);
                result.Files[$"Results/{resultNode.FileName}"] = resultNode;
            }

            // Params → Params/{ParamsName}.h
            foreach (var operation in client.Operations)
            {
                var paramsNode = _paramsBuilder.Build(operation);
                result.Files[$"Params/{paramsNode.FileName}"] = paramsNode;
            }

            // API files → {ClientName}Api.h + {ClientName}Api.cpp
            var (header, source) = _apiBuilder.Build(client);
            result.Files[header.FileName] = header;
            result.Files[source.FileName] = source;
        }

        return result;
    }
}
