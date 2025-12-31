using Microsoft.Extensions.Logging;
using Microsoft.OpenApi;
using ValIO.Core.Interfaces;
using ValIO.Core.Models;
using ValIO.Generators.CSharp.Models;
using ValIO.Generators.TypeScript;
using ValIO.Generators.TypeScript.Models;
using ValIO.Generators.TypeScript.TanStackQuery.Models;

namespace ValIO.Generators.TypeScript.TanStackQuery;

/// <summary>
/// Генератор TanStack Query хуков для TypeScript
/// </summary>
public class TanStackQueryGenerator
{
    private readonly INamingConverter _namingConverter;
    private readonly TypeScriptTypeMapper _typeMapper;
    private readonly ITemplateRegistry _templateRegistry;
    private readonly ILogger<TanStackQueryGenerator>? _logger;

    public TanStackQueryGenerator(
        INamingConverter namingConverter,
        TypeScriptTypeMapper typeMapper,
        ITemplateRegistry templateRegistry,
        ILogger<TanStackQueryGenerator>? logger = null)
    {
        _namingConverter = namingConverter ?? throw new ArgumentNullException(nameof(namingConverter));
        _typeMapper = typeMapper ?? throw new ArgumentNullException(nameof(typeMapper));
        _templateRegistry = templateRegistry ?? throw new ArgumentNullException(nameof(templateRegistry));
        _logger = logger;
    }

    /// <summary>
    /// Генерирует React Query hook для query операции (GET)
    /// </summary>
    public string GenerateQueryHook(
        ApiOperation operation,
        TypeScriptOperationTemplateData operationTemplate,
        TypeScriptGenerationOptions options,
        ISchemaResolver schemaResolver,
        IReadOnlyList<string>? importedTypes = null,
        string? modelsPath = null)
    {
        if (operation == null)
            throw new ArgumentNullException(nameof(operation));
        if (operationTemplate == null)
            throw new ArgumentNullException(nameof(operationTemplate));

        var operationId = string.IsNullOrWhiteSpace(operation.OperationId)
            ? "execute"
            : operation.OperationId;
        var hookName = GenerateHookName(operationId, isMutation: false);
        var functionName = _namingConverter.ToCamelCase(operationId);

        // Определяем имена схем для валидации response
        string? responseSchemaName = null;
        string? responseSchemaFileName = null;
        if (options.ModelFormat == ModelFormat.ValidationSchemas && 
            options.ValidationLibrary == ValidationLibrary.Zod && 
            options.DeserializeWithSchemas)
        {
            var responseTypeName = operationTemplate.ReturnTypeInfo.IsTask && 
                operationTemplate.ReturnTypeInfo.GenericArguments.Count > 0
                ? operationTemplate.ReturnTypeInfo.GenericArguments[0].FullTypeName
                : operationTemplate.ReturnTypeInfo.FullTypeName;
            
            // Извлекаем имя типа без массивов и дженериков
            var cleanTypeName = responseTypeName.Split('<')[0].Split('[')[0].Trim();
            responseSchemaName = $"{_namingConverter.ToPascalCase(cleanTypeName)}Schema";
            responseSchemaFileName = _namingConverter.ToCamelCase(cleanTypeName);
        }

        // Вычисляем имя функции для query key
        var queryKeyFunctionName = $"get{_namingConverter.ToPascalCase(functionName)}QueryKey";

        // Создаем данные для шаблона
        var templateData = new TanStackQueryHookTemplateData
        {
            HookName = hookName,
            FunctionName = functionName,
            QueryKeyFunctionName = queryKeyFunctionName,
            Operation = operation,
            OperationTemplate = operationTemplate,
            IsQuery = true,
            IsMutation = false,
            Options = options,
            ResponseSchemaName = responseSchemaName,
            ResponseSchemaFileName = responseSchemaFileName,
            ImportedTypes = importedTypes ?? Array.Empty<string>(),
            ModelsPath = modelsPath,
            GenerateImports = false
        };

        return _templateRegistry.Render("tanstack-query-hook.cshtml", templateData);
    }

    /// <summary>
    /// Генерирует React Query hook для mutation операции (POST, PUT, DELETE, PATCH)
    /// </summary>
    public string GenerateMutationHook(
        ApiOperation operation,
        TypeScriptOperationTemplateData operationTemplate,
        TypeScriptGenerationOptions options,
        ISchemaResolver schemaResolver,
        IReadOnlyList<string>? importedTypes = null,
        string? modelsPath = null)
    {
        if (operation == null)
            throw new ArgumentNullException(nameof(operation));
        if (operationTemplate == null)
            throw new ArgumentNullException(nameof(operationTemplate));

        var operationId = string.IsNullOrWhiteSpace(operation.OperationId)
            ? "execute"
            : operation.OperationId;
        var hookName = GenerateHookName(operationId, isMutation: true);
        var functionName = _namingConverter.ToCamelCase(operationId);

        // Определяем имена схем для валидации request body
        string? requestSchemaName = null;
        string? requestSchemaFileName = null;
        if (options.ModelFormat == ModelFormat.ValidationSchemas && 
            options.ValidationLibrary == ValidationLibrary.Zod && 
            options.SerializeWithSchemas &&
            operationTemplate.HasRequestBody &&
            operationTemplate.RequestBodyTypeInfo != null)
        {
            var requestTypeName = operationTemplate.RequestBodyTypeInfo.FullTypeName;
            // Извлекаем имя типа без массивов и дженериков
            var cleanTypeName = requestTypeName.Split('<')[0].Split('[')[0].Trim();
            requestSchemaName = $"{_namingConverter.ToPascalCase(cleanTypeName)}Schema";
            requestSchemaFileName = _namingConverter.ToCamelCase(cleanTypeName);
        }

        // Определяем имена схем для валидации response
        string? responseSchemaName = null;
        string? responseSchemaFileName = null;
        if (options.ModelFormat == ModelFormat.ValidationSchemas && 
            options.ValidationLibrary == ValidationLibrary.Zod && 
            options.DeserializeWithSchemas)
        {
            var responseTypeName = operationTemplate.ReturnTypeInfo.IsTask && 
                operationTemplate.ReturnTypeInfo.GenericArguments.Count > 0
                ? operationTemplate.ReturnTypeInfo.GenericArguments[0].FullTypeName
                : operationTemplate.ReturnTypeInfo.FullTypeName;
            
            // Извлекаем имя типа без массивов и дженериков
            var cleanTypeName = responseTypeName.Split('<')[0].Split('[')[0].Trim();
            responseSchemaName = $"{_namingConverter.ToPascalCase(cleanTypeName)}Schema";
            responseSchemaFileName = _namingConverter.ToCamelCase(cleanTypeName);
        }

        var templateData = new TanStackQueryHookTemplateData
        {
            HookName = hookName,
            FunctionName = functionName,
            Operation = operation,
            OperationTemplate = operationTemplate,
            IsQuery = false,
            IsMutation = true,
            Options = options,
            RequestSchemaName = requestSchemaName,
            RequestSchemaFileName = requestSchemaFileName,
            ResponseSchemaName = responseSchemaName,
            ResponseSchemaFileName = responseSchemaFileName,
            ImportedTypes = importedTypes ?? Array.Empty<string>(),
            ModelsPath = modelsPath,
            GenerateImports = false
        };

        return _templateRegistry.Render("tanstack-mutation-hook.cshtml", templateData);
    }

    /// <summary>
    /// Генерирует имя хука на основе operationId
    /// </summary>
    public string GenerateHookName(string operationId, bool isMutation)
    {
        if (string.IsNullOrWhiteSpace(operationId))
            return isMutation ? "useMutation" : "useQuery";

        var prefix = isMutation ? "use" : "use";
        var name = _namingConverter.ToPascalCase(operationId);
        return $"{prefix}{name}";
    }
}
