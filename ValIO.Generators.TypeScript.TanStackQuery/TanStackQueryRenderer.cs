using System.Text;
using ValIO.Core.Interfaces;
using ValIO.Core.Models;
using ValIO.Core.Models.TemplateData;
using ValIO.Core.Utilities;
using ValIO.Generators.TypeScript.Models;

namespace ValIO.Generators.TypeScript.TanStackQuery;

/// <summary>
/// Рендерер TanStack Query хуков для TypeScript
/// </summary>
public class TanStackQueryRenderer : IQueryWrapperRenderer
{
    private readonly INamingConverter _namingConverter;

    public TanStackQueryRenderer(INamingConverter namingConverter)
    {
        _namingConverter = namingConverter ?? throw new ArgumentNullException(nameof(namingConverter));
    }

    public QueryWrapperLibrary Library => QueryWrapperLibrary.TanStackQuery;
    public GenerationLanguage Language => GenerationLanguage.TypeScript;

    public string RenderQueryHook(OperationTemplateData operation, string? clientMethodName = null, string? clientImportPath = null)
    {
        if (operation == null)
            throw new ArgumentNullException(nameof(operation));

        var sb = new StringBuilder();
        
        // Генерируем импорты моделей
        RenderModelImports(sb, operation, clientImportPath);
        
        // Генерируем имя хука и функции
        var operationId = operation.MethodName;
        var hookName = GenerateHookName(operationId, isMutation: false);
        var functionName = _namingConverter.ToCamelCase(operationId);
        var queryKeyFunctionName = $"get{_namingConverter.ToPascalCase(functionName)}QueryKey";
        
        // Получаем опции из OperationTemplateData
        var options = GetTypeScriptOptions(operation);
        
        // Имя метода клиента
        var actualClientMethodName = clientMethodName ?? functionName;
        
        // Генерируем комментарии
        CommentGenerator.GenerateJSDocComment(sb, 0, operation.Summary);
        CommentGenerator.GenerateJSDocComment(sb, 0, operation.Description);
        
        // Генерируем функцию клиента (если clientMethodName не указан)
        if (string.IsNullOrEmpty(clientMethodName))
        {
            RenderClientFunction(sb, operation, functionName, options);
        }
        
        // Генерируем query key функцию
        RenderQueryKeyFunction(sb, operation, queryKeyFunctionName);
        
        // Генерируем hook
        RenderQueryHookFunction(sb, operation, hookName, queryKeyFunctionName, actualClientMethodName, options);
        
        // Генерируем query options helper
        RenderQueryOptionsHelper(sb, operation, functionName, queryKeyFunctionName, actualClientMethodName, options);
        
        // Генерируем infinite query support
        RenderInfiniteQuerySupport(sb, operation, functionName, actualClientMethodName, options);
        
        // Генерируем prefetch support
        RenderPrefetchSupport(sb, operation, functionName, queryKeyFunctionName, actualClientMethodName, options);
        
        // Генерируем экспорты для ResultType и AwaitedResultType
        RenderTypeExports(sb, operation);
        
        return sb.ToString();
    }

    public string RenderMutationHook(OperationTemplateData operation, string? clientMethodName = null, string? clientImportPath = null)
    {
        if (operation == null)
            throw new ArgumentNullException(nameof(operation));

        var sb = new StringBuilder();
        
        // Генерируем импорты моделей
        RenderModelImports(sb, operation, clientImportPath);
        
        // Генерируем имя хука и функции
        var operationId = operation.MethodName;
        var hookName = GenerateHookName(operationId, isMutation: true);
        var functionName = _namingConverter.ToCamelCase(operationId);
        
        // Получаем опции из OperationTemplateData
        var options = GetTypeScriptOptions(operation);
        
        // Имя метода клиента
        var actualClientMethodName = clientMethodName ?? functionName;
        
        // Генерируем комментарии
        CommentGenerator.GenerateJSDocComment(sb, 0, operation.Summary);
        CommentGenerator.GenerateJSDocComment(sb, 0, operation.Description);
        
        // Генерируем функцию клиента (если clientMethodName не указан)
        if (string.IsNullOrEmpty(clientMethodName))
        {
            RenderClientFunction(sb, operation, functionName, options);
        }
        
        // Генерируем mutation options helper
        RenderMutationOptionsHelper(sb, operation, functionName, actualClientMethodName, options);
        
        // Генерируем mutation hook
        RenderMutationHookFunction(sb, operation, hookName, functionName, actualClientMethodName, options);
        
        // Генерируем экспорты для ResultType и AwaitedResultType
        RenderTypeExports(sb, operation);
        
        return sb.ToString();
    }

    private string GenerateHookName(string operationId, bool isMutation)
    {
        if (string.IsNullOrWhiteSpace(operationId))
            return isMutation ? "useMutation" : "useQuery";

        var prefix = "use";
        var name = _namingConverter.ToPascalCase(operationId);
        return $"{prefix}{name}";
    }

    private TypeScriptGenerationOptions GetTypeScriptOptions(OperationTemplateData operation)
    {
        // Извлекаем опции из OperationTemplateData.Options через приведение через object
        var options = new TypeScriptGenerationOptions();
        
        try
        {
            var typedOperation = (OperationTemplateData<TypeScriptOperationTemplateDataOptions>)(object)operation;
            // TypeScriptOperationTemplateDataOptions сейчас пустой класс, но может быть расширен в будущем
            // HttpClientLibrary и Axios опции должны приходить из ClientTemplateData, а не из OperationTemplateData
        }
        catch
        {
            // Используем значения по умолчанию
        }
        
        return options;
    }


    /// <summary>
    /// Рендерит функцию клиента (если clientMethodName не указан, генерируем локальную функцию)
    /// </summary>
    private void RenderClientFunction(StringBuilder sb, OperationTemplateData operation, string functionName, TypeScriptGenerationOptions options)
    {
        // Если clientMethodName не указан, значит функция клиента уже существует в другом файле
        // Здесь мы не генерируем функцию, так как она должна быть импортирована
        // Этот метод оставлен для будущего расширения, если понадобится встроенная функция
    }

    /// <summary>
    /// Рендерит функцию для генерации query key
    /// </summary>
    private void RenderQueryKeyFunction(StringBuilder sb, OperationTemplateData operation, string queryKeyFunctionName)
    {
        // Собираем параметры для query key
        var keyParams = new List<string>();
        
        // Path параметры всегда включены в query key
        foreach (var param in operation.PathParameters)
        {
            keyParams.Add($"{param.VariableName}: {param.TypeInfo.FullTypeName}");
        }
        
        // Query параметры также включены в query key
        if (operation.UseQueryParametersModel && !string.IsNullOrEmpty(operation.QueryParametersModelTypeName))
        {
            var hasRequiredParam = operation.QueryParameters.Any(p => p.IsRequired);
            var optional = hasRequiredParam ? "" : "?";
            keyParams.Add($"params{optional}: {operation.QueryParametersModelTypeName}");
        }
        else
        {
            foreach (var param in operation.QueryParameters)
            {
                var optional = param.IsRequired ? "" : "?";
                keyParams.Add($"{param.VariableName}{optional}: {param.TypeInfo.FullTypeName}");
            }
        }
        
        // Генерируем параметры функции
        var functionParams = keyParams.Count > 0 ? string.Join(", ", keyParams) : "";
        
        // Генерируем query key массив
        var keyItems = new List<string>();
        keyItems.Add($"'{operation.Path}'");
        
        // Добавляем path параметры в query key
        foreach (var param in operation.PathParameters)
        {
            keyItems.Add(param.VariableName);
        }
        
        // Добавляем query параметры в query key
        if (operation.UseQueryParametersModel && !string.IsNullOrEmpty(operation.QueryParametersModelTypeName))
        {
            keyItems.Add("params");
        }
        else
        {
            foreach (var param in operation.QueryParameters)
            {
                keyItems.Add(param.VariableName);
            }
        }
        
        var keyArray = string.Join(", ", keyItems);
        
        CodeFormatter.AppendLine(sb, 0, $"export const {queryKeyFunctionName} = ({functionParams}): QueryKey => [");
        CodeFormatter.AppendLine(sb, 1, keyArray);
        CodeFormatter.AppendLine(sb, 0, "];");
        CodeFormatter.AppendEmptyLine(sb);
    }

    /// <summary>
    /// Рендерит query hook функцию
    /// </summary>
    private void RenderQueryHookFunction(StringBuilder sb, OperationTemplateData operation, string hookName, string queryKeyFunctionName, string clientMethodName, TypeScriptGenerationOptions options)
    {
        // Определяем тип ответа
        var responseType = operation.ReturnTypeInfo.FullTypeName;
        var errorType = "Error";
        
        // Собираем параметры функции
        var functionParams = BuildQueryHookParameters(operation);
        
        // Генерируем query key вызов
        var queryKeyParams = BuildQueryKeyParameters(operation);
        var queryKeyCall = queryKeyParams.Count > 0 
            ? $"{queryKeyFunctionName}({string.Join(", ", queryKeyParams)})"
            : $"{queryKeyFunctionName}()";
        
        // Генерируем query function
        var queryFnParams = BuildClientMethodParameters(operation, clientMethodName);
        
        CodeFormatter.AppendLine(sb, 0, $"export function {hookName}<TData = {responseType}, TError = {errorType}>(");
        CodeFormatter.AppendLine(sb, 1, functionParams);
        CodeFormatter.AppendLine(sb, 0, $") : UseQueryResult<TData, TError> {{");
        CodeFormatter.AppendEmptyLine(sb);
        CodeFormatter.AppendLine(sb, 1, $"const queryKey = {queryKeyCall};");
        CodeFormatter.AppendLine(sb, 1, $"const queryFn: QueryFunction<TData> = () => {clientMethodName}({queryFnParams});");
        CodeFormatter.AppendEmptyLine(sb);
        CodeFormatter.AppendLine(sb, 1, $"return useQuery<TData, TError>({{");
        CodeFormatter.AppendLine(sb, 2, "queryKey,");
        CodeFormatter.AppendLine(sb, 2, "queryFn,");
        CodeFormatter.AppendLine(sb, 2, "...options");
        CodeFormatter.AppendLine(sb, 1, "});");
        CodeFormatter.AppendLine(sb, 0, "}");
        CodeFormatter.AppendEmptyLine(sb);
    }

    /// <summary>
    /// Рендерит query options helper функцию
    /// </summary>
    private void RenderQueryOptionsHelper(StringBuilder sb, OperationTemplateData operation, string functionName, string queryKeyFunctionName, string clientMethodName, TypeScriptGenerationOptions options)
    {
        var responseType = operation.ReturnTypeInfo.FullTypeName;
        var errorType = "Error";
        var helperName = $"get{_namingConverter.ToPascalCase(functionName)}QueryOptions";
        
        var functionParams = BuildQueryHookParameters(operation);
        var queryKeyParams = BuildQueryKeyParameters(operation);
        var queryKeyCall = queryKeyParams.Count > 0 
            ? $"{queryKeyFunctionName}({string.Join(", ", queryKeyParams)})"
            : $"{queryKeyFunctionName}()";
        
        var queryFnParams = BuildClientMethodParameters(operation, clientMethodName);
        
        CodeFormatter.AppendLine(sb, 0, $"export const {helperName} = <TData = {responseType}, TError = {errorType}>(");
        CodeFormatter.AppendLine(sb, 1, functionParams);
        CodeFormatter.AppendLine(sb, 0, $") : UseQueryOptions<TData, TError> => {{");
        CodeFormatter.AppendEmptyLine(sb);
        CodeFormatter.AppendLine(sb, 1, $"const queryKey = {queryKeyCall};");
        CodeFormatter.AppendLine(sb, 1, $"const queryFn: QueryFunction<TData> = () => {clientMethodName}({queryFnParams});");
        CodeFormatter.AppendEmptyLine(sb);
        CodeFormatter.AppendLine(sb, 1, "return {");
        CodeFormatter.AppendLine(sb, 2, "queryKey,");
        CodeFormatter.AppendLine(sb, 2, "queryFn,");
        CodeFormatter.AppendLine(sb, 2, "...options");
        CodeFormatter.AppendLine(sb, 1, "};");
        CodeFormatter.AppendLine(sb, 0, "};");
        CodeFormatter.AppendEmptyLine(sb);
    }

    /// <summary>
    /// Рендерит infinite query support
    /// </summary>
    private void RenderInfiniteQuerySupport(StringBuilder sb, OperationTemplateData operation, string functionName, string clientMethodName, TypeScriptGenerationOptions options)
    {
        var responseType = operation.ReturnTypeInfo.FullTypeName;
        var errorType = "Error";
        var infiniteHookName = $"use{_namingConverter.ToPascalCase(functionName)}Infinite";
        var queryKeyFunctionName = $"get{_namingConverter.ToPascalCase(functionName)}QueryKey";
        
        var functionParams = BuildQueryHookParameters(operation);
        var queryKeyParamsStr = string.Join(", ", BuildQueryKeyParameters(operation));
        var infiniteQueryFnParams = BuildInfiniteQueryFnParams(operation, clientMethodName);
        var clientMethodParams = BuildClientMethodParameters(operation, clientMethodName);
        
        CodeFormatter.AppendLine(sb, 0, $"export function {infiniteHookName}<TData = InfiniteData<{responseType}>, TError = {errorType}>(");
        CodeFormatter.AppendLine(sb, 1, functionParams);
        CodeFormatter.AppendLine(sb, 0, $") : UseInfiniteQueryResult<TData, TError> {{");
        CodeFormatter.AppendEmptyLine(sb);
        CodeFormatter.AppendLine(sb, 1, $"const queryKey = {queryKeyFunctionName}({queryKeyParamsStr});");
        CodeFormatter.AppendLine(sb, 1, $"const queryFn: QueryFunction<TData> = ({infiniteQueryFnParams}) => {clientMethodName}({clientMethodParams});");
        CodeFormatter.AppendEmptyLine(sb);
        CodeFormatter.AppendLine(sb, 1, $"return useInfiniteQuery<TData, TError>({{");
        CodeFormatter.AppendLine(sb, 2, "queryKey,");
        CodeFormatter.AppendLine(sb, 2, "queryFn,");
        CodeFormatter.AppendLine(sb, 2, "...options");
        CodeFormatter.AppendLine(sb, 1, "});");
        CodeFormatter.AppendLine(sb, 0, "}");
        CodeFormatter.AppendEmptyLine(sb);
    }

    /// <summary>
    /// Рендерит prefetch support
    /// </summary>
    private void RenderPrefetchSupport(StringBuilder sb, OperationTemplateData operation, string functionName, string queryKeyFunctionName, string clientMethodName, TypeScriptGenerationOptions options)
    {
        var prefetchFunctionName = $"prefetch{_namingConverter.ToPascalCase(functionName)}";
        var functionParams = BuildQueryHookParameters(operation);
        var queryKeyParams = BuildQueryKeyParameters(operation);
        var queryKeyCall = queryKeyParams.Count > 0 
            ? $"{queryKeyFunctionName}({string.Join(", ", queryKeyParams)})"
            : $"{queryKeyFunctionName}()";
        
        var queryFnParams = BuildClientMethodParameters(operation, clientMethodName);
        
        CodeFormatter.AppendLine(sb, 0, $"export const {prefetchFunctionName} = async (");
        CodeFormatter.AppendLine(sb, 1, "queryClient: QueryClient,");
        CodeFormatter.AppendLine(sb, 1, functionParams);
        CodeFormatter.AppendLine(sb, 0, $") : Promise<void> => {{");
        CodeFormatter.AppendEmptyLine(sb);
        CodeFormatter.AppendLine(sb, 1, $"const queryKey = {queryKeyCall};");
        CodeFormatter.AppendLine(sb, 1, $"const queryFn: QueryFunction = () => {clientMethodName}({queryFnParams});");
        CodeFormatter.AppendEmptyLine(sb);
        CodeFormatter.AppendLine(sb, 1, $"await queryClient.prefetchQuery({{");
        CodeFormatter.AppendLine(sb, 2, "queryKey,");
        CodeFormatter.AppendLine(sb, 2, "queryFn");
        CodeFormatter.AppendLine(sb, 1, "});");
        CodeFormatter.AppendLine(sb, 0, "};");
        CodeFormatter.AppendEmptyLine(sb);
    }

    /// <summary>
    /// Рендерит mutation options helper
    /// </summary>
    private void RenderMutationOptionsHelper(StringBuilder sb, OperationTemplateData operation, string functionName, string clientMethodName, TypeScriptGenerationOptions options)
    {
        var responseType = operation.ReturnTypeInfo.FullTypeName;
        var errorType = "Error";
        var helperName = $"get{_namingConverter.ToPascalCase(functionName)}MutationOptions";
        
        var mutationParams = BuildMutationHookParameters(operation);
        var clientMethodParams = BuildClientMethodParameters(operation, clientMethodName);
        
        CodeFormatter.AppendLine(sb, 0, $"export const {helperName} = <TError = {errorType}, TContext = unknown>(");
        CodeFormatter.AppendLine(sb, 1, mutationParams);
        CodeFormatter.AppendLine(sb, 0, $") : UseMutationOptions<{responseType}, TError, void, TContext> => {{");
        CodeFormatter.AppendEmptyLine(sb);
        CodeFormatter.AppendLine(sb, 1, $"const mutationFn: MutationFunction<{responseType}, void> = () => {{");
        CodeFormatter.AppendLine(sb, 2, $"return {clientMethodName}({clientMethodParams});");
        CodeFormatter.AppendLine(sb, 1, "};");
        CodeFormatter.AppendEmptyLine(sb);
        CodeFormatter.AppendLine(sb, 1, "return {");
        CodeFormatter.AppendLine(sb, 2, "mutationFn,");
        CodeFormatter.AppendLine(sb, 2, "...options");
        CodeFormatter.AppendLine(sb, 1, "};");
        CodeFormatter.AppendLine(sb, 0, "};");
        CodeFormatter.AppendEmptyLine(sb);
    }

    /// <summary>
    /// Рендерит mutation hook функцию
    /// </summary>
    private void RenderMutationHookFunction(StringBuilder sb, OperationTemplateData operation, string hookName, string functionName, string clientMethodName, TypeScriptGenerationOptions options)
    {
        var responseType = operation.ReturnTypeInfo.FullTypeName;
        var errorType = "Error";
        
        var mutationParams = BuildMutationHookParameters(operation);
        var clientMethodParams = BuildClientMethodParameters(operation, clientMethodName);
        
        CodeFormatter.AppendLine(sb, 0, $"export function {hookName}<TError = {errorType}, TContext = unknown>(");
        CodeFormatter.AppendLine(sb, 1, mutationParams);
        CodeFormatter.AppendLine(sb, 0, $") : UseMutationResult<{responseType}, TError, void, TContext> {{");
        CodeFormatter.AppendEmptyLine(sb);
        CodeFormatter.AppendLine(sb, 1, $"const mutationFn: MutationFunction<{responseType}, void> = () => {{");
        CodeFormatter.AppendLine(sb, 2, $"return {clientMethodName}({clientMethodParams});");
        CodeFormatter.AppendLine(sb, 1, "};");
        CodeFormatter.AppendEmptyLine(sb);
        CodeFormatter.AppendLine(sb, 1, $"return useMutation<{responseType}, TError, void, TContext>({{");
        CodeFormatter.AppendLine(sb, 2, "mutationFn,");
        CodeFormatter.AppendLine(sb, 2, "...options");
        CodeFormatter.AppendLine(sb, 1, "});");
        CodeFormatter.AppendLine(sb, 0, "}");
        CodeFormatter.AppendEmptyLine(sb);
    }

    #region Private Helper Methods


    /// <summary>
    /// Строит параметры для query hook
    /// </summary>
    private string BuildQueryHookParameters(OperationTemplateData operation)
    {
        var parameters = new List<string>();
        
        // Path параметры
        foreach (var param in operation.PathParameters)
        {
            parameters.Add($"{param.VariableName}: {param.TypeInfo.FullTypeName}");
        }
        
        // Query параметры
        if (operation.UseQueryParametersModel && !string.IsNullOrEmpty(operation.QueryParametersModelTypeName))
        {
            var hasRequiredParam = operation.QueryParameters.Any(p => p.IsRequired);
            var optional = hasRequiredParam ? "" : "?";
            parameters.Add($"params{optional}: {operation.QueryParametersModelTypeName}");
        }
        else
        {
            foreach (var param in operation.QueryParameters)
            {
                var optional = param.IsRequired ? "" : "?";
                parameters.Add($"{param.VariableName}{optional}: {param.TypeInfo.FullTypeName}");
            }
        }
        
        // Query options
        var responseType = operation.ReturnTypeInfo.FullTypeName;
        var errorType = "Error";
        parameters.Add($"options?: UseQueryOptions<{responseType}, {errorType}>");
        
        return string.Join(",\n  ", parameters);
    }

    /// <summary>
    /// Строит параметры для query key функции
    /// </summary>
    private List<string> BuildQueryKeyParameters(OperationTemplateData operation)
    {
        var parameters = new List<string>();
        
        // Path параметры
        foreach (var param in operation.PathParameters)
        {
            parameters.Add(param.VariableName);
        }
        
        // Query параметры
        if (operation.UseQueryParametersModel && !string.IsNullOrEmpty(operation.QueryParametersModelTypeName))
        {
            parameters.Add("params");
        }
        else
        {
            foreach (var param in operation.QueryParameters)
            {
                parameters.Add(param.VariableName);
            }
        }
        
        return parameters;
    }

    /// <summary>
    /// Строит параметры для вызова метода клиента
    /// </summary>
    private string BuildClientMethodParameters(OperationTemplateData operation, string clientMethodName)
    {
        var parameters = new List<string>();
        
        // Path параметры
        foreach (var param in operation.PathParameters)
        {
            parameters.Add(param.VariableName);
        }
        
        // Query параметры
        if (operation.UseQueryParametersModel && !string.IsNullOrEmpty(operation.QueryParametersModelTypeName))
        {
            parameters.Add("params");
        }
        else
        {
            foreach (var param in operation.QueryParameters)
            {
                parameters.Add(param.VariableName);
            }
        }
        
        // Header параметры
        foreach (var param in operation.HeaderParameters)
        {
            parameters.Add(param.VariableName);
        }
        
        // Request body
        if (operation.HasRequestBody && !string.IsNullOrEmpty(operation.RequestBodyParameter))
        {
            parameters.Add(operation.RequestBodyParameter);
        }
        else if (operation.IsFormData && operation.FormDataProperties != null && operation.FormDataProperties.Count > 0)
        {
            // Для form-data передаем объект с полями
            var formDataProps = operation.FormDataProperties.Select(p => p.VariableName).ToList();
            if (formDataProps.Count > 0)
            {
                parameters.Add($"{{ {string.Join(", ", formDataProps)} }}");
            }
        }
        
        return string.Join(", ", parameters);
    }

    /// <summary>
    /// Строит параметры для infinite query function
    /// </summary>
    private string BuildInfiniteQueryFnParams(OperationTemplateData operation, string clientMethodName)
    {
        // Infinite query function получает pageParam
        return "pageParam: unknown";
    }

    /// <summary>
    /// Строит параметры для mutation hook
    /// </summary>
    private string BuildMutationHookParameters(OperationTemplateData operation)
    {
        var parameters = new List<string>();
        
        // Path параметры
        foreach (var param in operation.PathParameters)
        {
            parameters.Add($"{param.VariableName}: {param.TypeInfo.FullTypeName}");
        }
        
        // Query параметры
        if (operation.UseQueryParametersModel && !string.IsNullOrEmpty(operation.QueryParametersModelTypeName))
        {
            var hasRequiredParam = operation.QueryParameters.Any(p => p.IsRequired);
            var optional = hasRequiredParam ? "" : "?";
            parameters.Add($"params{optional}: {operation.QueryParametersModelTypeName}");
        }
        else
        {
            foreach (var param in operation.QueryParameters)
            {
                var optional = param.IsRequired ? "" : "?";
                parameters.Add($"{param.VariableName}{optional}: {param.TypeInfo.FullTypeName}");
            }
        }
        
        // Header параметры
        foreach (var param in operation.HeaderParameters)
        {
            var optional = param.IsRequired ? "" : "?";
            parameters.Add($"{param.VariableName}{optional}: {param.TypeInfo.FullTypeName}");
        }
        
        // Request body
        if (operation.HasRequestBody && !string.IsNullOrEmpty(operation.RequestBodyParameter))
        {
            var optional = operation.RequestBodyTypeInfo != null && !operation.RequestBodyTypeInfo.IsNullable ? "" : "?";
            var typeStr = operation.RequestBodyTypeInfo?.FullTypeName ?? "any";
            parameters.Add($"{operation.RequestBodyParameter}{optional}: {typeStr}");
        }
        else if (operation.IsFormData && operation.FormDataProperties != null && operation.FormDataProperties.Count > 0)
        {
            foreach (var formProp in operation.FormDataProperties)
            {
                var optional = formProp.IsRequired ? "" : "?";
                parameters.Add($"{formProp.VariableName}{optional}: {formProp.TypeInfo.FullTypeName}");
            }
        }
        
        // Mutation options
        var responseType = operation.ReturnTypeInfo.FullTypeName;
        var errorType = "Error";
        parameters.Add($"options?: UseMutationOptions<{responseType}, {errorType}, void, unknown>");
        
        return string.Join(",\n  ", parameters);
    }

    /// <summary>
    /// Рендерит импорты моделей для операции
    /// </summary>
    private void RenderModelImports(StringBuilder sb, OperationTemplateData operation, string? modelsPath)
    {
        if (string.IsNullOrEmpty(modelsPath))
            return;
            
        // Используем уже собранные зависимости из операции
        var dependencies = new HashSet<string>();
        
        // Добавляем зависимости из operation.Dependencies
        if (operation.Dependencies != null)
        {
            foreach (var dependency in operation.Dependencies)
            {
                if (!string.IsNullOrEmpty(dependency) && !IsBuiltInTypeScriptType(dependency))
                {
                    // Извлекаем имя типа из FullTypeName (убираем generic параметры)
                    var typeName = ExtractTypeName(dependency);
                    if (!string.IsNullOrEmpty(typeName) && !IsBuiltInTypeScriptType(typeName))
                    {
                        dependencies.Add(typeName);
                    }
                }
            }
        }
        
        // Также добавляем QueryParametersModelTypeName, если используется
        if (operation.UseQueryParametersModel && !string.IsNullOrEmpty(operation.QueryParametersModelTypeName))
        {
            dependencies.Add(operation.QueryParametersModelTypeName);
        }
        
        // Генерируем импорты
        if (dependencies.Count > 0)
        {
            foreach (var dependency in dependencies.OrderBy(d => d))
            {
                var importFileName = GetModelFileName(dependency);
                sb.AppendLine($"import type {{ {dependency} }} from '{modelsPath}/{importFileName}.model';");
            }
            sb.AppendLine();
        }
    }
    
    /// <summary>
    /// Извлекает имя типа из полного имени типа (убирает generic параметры и массивы)
    /// </summary>
    private string ExtractTypeName(string fullTypeName)
    {
        if (string.IsNullOrEmpty(fullTypeName))
            return string.Empty;
            
        // Убираем generic параметры (например, List<User> -> List, но нам нужен User)
        // Для простоты, если есть <, берем первый generic аргумент
        if (fullTypeName.Contains('<'))
        {
            var startIndex = fullTypeName.IndexOf('<');
            var endIndex = fullTypeName.LastIndexOf('>');
            if (startIndex > 0 && endIndex > startIndex)
            {
                var genericArgs = fullTypeName.Substring(startIndex + 1, endIndex - startIndex - 1);
                // Берем первый аргумент до запятой или до конца
                var commaIndex = genericArgs.IndexOf(',');
                if (commaIndex > 0)
                {
                    genericArgs = genericArgs.Substring(0, commaIndex).Trim();
                }
                return genericArgs.Trim();
            }
        }
        
        // Убираем массивы (например, User[] -> User)
        if (fullTypeName.EndsWith("[]"))
        {
            return fullTypeName.Substring(0, fullTypeName.Length - 2);
        }
        
        return fullTypeName;
    }
    
    /// <summary>
    /// Проверяет, является ли тип встроенным типом TypeScript
    /// </summary>
    private bool IsBuiltInTypeScriptType(string typeName)
    {
        if (string.IsNullOrEmpty(typeName))
            return false;
            
        var builtInTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "any",
            "unknown",
            "void",
            "null",
            "undefined",
            "string",
            "number",
            "boolean",
            "object",
            "Array",
            "Promise",
            "Record"
        };
        
        return builtInTypes.Contains(typeName);
    }
    
    /// <summary>
    /// Получает имя файла модели из имени типа
    /// </summary>
    private string GetModelFileName(string typeName)
    {
        if (string.IsNullOrEmpty(typeName))
            return string.Empty;
            
        // Преобразуем PascalCase в camelCase для имени файла
        return char.ToLowerInvariant(typeName[0]) + typeName.Substring(1);
    }
    
    /// <summary>
    /// Рендерит экспорты для ResultType и AwaitedResultType
    /// </summary>
    private void RenderTypeExports(StringBuilder sb, OperationTemplateData operation)
    {
        var responseType = operation.ReturnTypeInfo.FullTypeName;
        
        // Экспортируем ResultType на основе Response model
        CodeFormatter.AppendLine(sb, 0, $"export type ResultType = Awaited<ReturnType<typeof {_namingConverter.ToCamelCase(operation.MethodName)}>>;");
        CodeFormatter.AppendEmptyLine(sb);
        
        // Экспортируем AwaitedResultType на основе ResultType
        CodeFormatter.AppendLine(sb, 0, "export type AwaitedResultType = Awaited<ResultType>;");
        CodeFormatter.AppendEmptyLine(sb);
    }

    #endregion
}
