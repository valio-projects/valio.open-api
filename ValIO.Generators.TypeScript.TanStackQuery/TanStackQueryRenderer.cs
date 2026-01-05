using System.Text;
using ValIO.Core.Interfaces;
using ValIO.Core.Models;
using ValIO.Core.Models.TemplateData;
using ValIO.Core.Utilities;
using ValIO.Generators.TypeScript.Models;
using ValIO.Generators.TypeScript.Models.CodeGeneration;

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

    public string RenderQueryHook(OperationTemplateData operation, string? clientMethodName = null, object? options = null)
    {
        if (operation == null)
            throw new ArgumentNullException(nameof(operation));

        var sb = new StringBuilder();
        
        // Генерируем имя хука и функции
        var operationId = operation.MethodName;
        var hookName = GenerateHookName(operationId, isMutation: false);
        var functionName = _namingConverter.ToCamelCase(operationId);
        var queryKeyFunctionName = $"get{_namingConverter.ToPascalCase(functionName)}QueryKey";
        
        // Получаем опции из параметра или используем значения по умолчанию
        var typeScriptOptions = options as TypeScriptGenerationOptions ?? new TypeScriptGenerationOptions();
        
        // Имя метода клиента
        var actualClientMethodName = clientMethodName ?? functionName;
        
        // Генерируем комментарии
        CommentGenerator.GenerateJSDocComment(sb, 0, operation.Summary);
        CommentGenerator.GenerateJSDocComment(sb, 0, operation.Description);
        
        // Генерируем функцию клиента (если clientMethodName не указан)
        if (string.IsNullOrEmpty(clientMethodName))
        {
            RenderClientFunction(sb, operation, functionName, typeScriptOptions);
        }
        
        // Генерируем экспорты для ResultType и AwaitedResultType (до хуков)
        RenderTypeExports(sb, operation, actualClientMethodName);
        
        // Генерируем query key функцию
        RenderQueryKeyFunction(sb, operation, queryKeyFunctionName);
        
        // Генерируем hook
        RenderQueryHookFunction(sb, operation, hookName, queryKeyFunctionName, actualClientMethodName, typeScriptOptions);
        
        // Генерируем query options helper
        RenderQueryOptionsHelper(sb, operation, functionName, queryKeyFunctionName, actualClientMethodName, typeScriptOptions);
        
        // Генерируем infinite query support
        RenderInfiniteQuerySupport(sb, operation, functionName, actualClientMethodName, typeScriptOptions);
        
        // Генерируем prefetch support
        RenderPrefetchSupport(sb, operation, functionName, queryKeyFunctionName, actualClientMethodName, typeScriptOptions);
        
        return sb.ToString();
    }

    public string RenderMutationHook(OperationTemplateData operation, string? clientMethodName = null, object? options = null)
    {
        if (operation == null)
            throw new ArgumentNullException(nameof(operation));

        var sb = new StringBuilder();
        
        // Генерируем имя хука и функции
        var operationId = operation.MethodName;
        var hookName = GenerateHookName(operationId, isMutation: true);
        var functionName = _namingConverter.ToCamelCase(operationId);
        
        // Получаем опции из параметра или используем значения по умолчанию
        var typeScriptOptions = options as TypeScriptGenerationOptions ?? new TypeScriptGenerationOptions();
        
        // Имя метода клиента
        var actualClientMethodName = clientMethodName ?? functionName;
        
        // Генерируем комментарии
        CommentGenerator.GenerateJSDocComment(sb, 0, operation.Summary);
        CommentGenerator.GenerateJSDocComment(sb, 0, operation.Description);
        
        // Генерируем функцию клиента (если clientMethodName не указан)
        if (string.IsNullOrEmpty(clientMethodName))
        {
            RenderClientFunction(sb, operation, functionName, typeScriptOptions);
        }
        
        // Генерируем экспорты для ResultType и AwaitedResultType (до хуков)
        RenderTypeExports(sb, operation, actualClientMethodName);
        
        // Генерируем mutation options helper
        RenderMutationOptionsHelper(sb, operation, functionName, actualClientMethodName, typeScriptOptions);
        
        // Генерируем mutation hook
        RenderMutationHookFunction(sb, operation, hookName, functionName, actualClientMethodName, typeScriptOptions);
        
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
        // Параметры уже дедуплицированы на этапе извлечения в OperationExtractor
        var queryKeyParams = BuildQueryKeyParameters(operation);
        
        // Собираем параметры для query key функции (с типами)
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
        
        // Добавляем параметры из BuildQueryKeyParameters (уже уникальные)
        keyItems.AddRange(queryKeyParams);
        
        var keyArray = string.Join(", ", keyItems);
        
        CodeFormatter.AppendLine(sb, 0, $"export const {queryKeyFunctionName} = (");
        if (!string.IsNullOrEmpty(functionParams))
        {
            CodeFormatter.AppendLine(sb, 1, functionParams);
        }
        CodeFormatter.AppendLine(sb, 0, ") => {");
        CodeFormatter.AppendEmptyLine(sb);
        CodeFormatter.AppendLine(sb, 1, $"return [{keyArray}];");
        CodeFormatter.AppendLine(sb, 0, "};");
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
        
        // Генерируем имена типов на основе имени функции клиента
        var functionNamePascal = _namingConverter.ToPascalCase(clientMethodName);
        var awaitedReturnTypeName = $"{functionNamePascal}AwaitedReturnType";
        
        // Собираем параметры функции
        var functionParams = BuildQueryHookParameters(operation, awaitedReturnTypeName, options);
        var functionParamsStr = FormatFunctionParameters(functionParams);
        
        // Генерируем query key вызов
        var queryKeyParams = BuildQueryKeyParameters(operation);
        var queryKeyCall = queryKeyParams.Count > 0 
            ? $"{queryKeyFunctionName}({string.Join(", ", queryKeyParams)})"
            : $"{queryKeyFunctionName}()";
        
        // Генерируем query function с signal
        var queryFnParams = BuildQueryFnClientMethodParameters(operation, clientMethodName, options);
        
        CodeFormatter.AppendLine(sb, 0, $"export function {hookName}<TData = {awaitedReturnTypeName}, TError = {errorType}>(");
        if (!string.IsNullOrEmpty(functionParamsStr))
        {
            CodeFormatter.AppendLine(sb, 1, functionParamsStr);
        }
        CodeFormatter.AppendLine(sb, 0, $") : UseQueryResult<TData, TError> {{");
        CodeFormatter.AppendEmptyLine(sb);
        if (options.HttpClientLibrary == HttpClientLibrary.Axios)
        {
            CodeFormatter.AppendLine(sb, 1, "const { query: queryOptions, timeout, axiosInstance } = options ?? {};");
        }
        else
        {
            CodeFormatter.AppendLine(sb, 1, "const { query: queryOptions, timeout } = options ?? {};");
        }
        CodeFormatter.AppendEmptyLine(sb);
        CodeFormatter.AppendLine(sb, 1, $"const queryKey = queryOptions?.queryKey ?? {queryKeyCall};");
        CodeFormatter.AppendLine(sb, 1, $"const queryFn: QueryFunction<{awaitedReturnTypeName}> = ({{ signal }}) => {{");
        CodeFormatter.AppendLine(sb, 2, $"return {clientMethodName}({queryFnParams});");
        CodeFormatter.AppendLine(sb, 1, "};");
        CodeFormatter.AppendEmptyLine(sb);
        CodeFormatter.AppendLine(sb, 1, $"const query = useQuery<{awaitedReturnTypeName}, TError, TData>({{");
        CodeFormatter.AppendLine(sb, 2, "queryKey,");
        CodeFormatter.AppendLine(sb, 2, "queryFn,");
        CodeFormatter.AppendLine(sb, 2, "enabled: true,");
        CodeFormatter.AppendLine(sb, 2, "...queryOptions");
        CodeFormatter.AppendLine(sb, 1, "});");
        CodeFormatter.AppendEmptyLine(sb);
        CodeFormatter.AppendLine(sb, 1, "return query;");
        CodeFormatter.AppendLine(sb, 0, "}");
        CodeFormatter.AppendEmptyLine(sb);
    }

    /// <summary>
    /// Рендерит query options helper функцию
    /// </summary>
    private void RenderQueryOptionsHelper(StringBuilder sb, OperationTemplateData operation, string functionName, string queryKeyFunctionName, string clientMethodName, TypeScriptGenerationOptions options)
    {
        var errorType = "Error";
        var helperName = $"get{_namingConverter.ToPascalCase(functionName)}QueryOptions";
        
        // Генерируем имена типов на основе имени функции клиента
        var functionNamePascal = _namingConverter.ToPascalCase(clientMethodName);
        var awaitedReturnTypeName = $"{functionNamePascal}AwaitedReturnType";
        
        var functionParams = BuildQueryHookParameters(operation, awaitedReturnTypeName, options);
        var functionParamsStr = FormatFunctionParameters(functionParams);
        var queryKeyParams = BuildQueryKeyParameters(operation);
        var queryKeyCall = queryKeyParams.Count > 0 
            ? $"{queryKeyFunctionName}({string.Join(", ", queryKeyParams)})"
            : $"{queryKeyFunctionName}()";
        
        var queryFnParams = BuildQueryFnClientMethodParameters(operation, clientMethodName, options);
        
        CodeFormatter.AppendLine(sb, 0, $"export const {helperName} = <TData = {awaitedReturnTypeName}, TError = {errorType}>(");
        if (!string.IsNullOrEmpty(functionParamsStr))
        {
            CodeFormatter.AppendLine(sb, 1, functionParamsStr);
        }
        CodeFormatter.AppendLine(sb, 0, $") => {{");
        CodeFormatter.AppendEmptyLine(sb);
        if (options.HttpClientLibrary == HttpClientLibrary.Axios)
        {
            CodeFormatter.AppendLine(sb, 1, "const { query: queryOptions, timeout, axiosInstance } = options ?? {};");
        }
        else
        {
            CodeFormatter.AppendLine(sb, 1, "const { query: queryOptions, timeout } = options ?? {};");
        }
        CodeFormatter.AppendEmptyLine(sb);
        CodeFormatter.AppendLine(sb, 1, $"const queryKey = queryOptions?.queryKey ?? {queryKeyCall};");
        CodeFormatter.AppendLine(sb, 1, $"const queryFn: QueryFunction<{awaitedReturnTypeName}> = ({{ signal }}) => {{");
        CodeFormatter.AppendLine(sb, 2, $"return {clientMethodName}({queryFnParams});");
        CodeFormatter.AppendLine(sb, 1, "};");
        CodeFormatter.AppendEmptyLine(sb);
        CodeFormatter.AppendLine(sb, 1, "return {");
        CodeFormatter.AppendLine(sb, 2, "queryKey,");
        CodeFormatter.AppendLine(sb, 2, "queryFn,");
        CodeFormatter.AppendLine(sb, 2, "enabled: true,");
        CodeFormatter.AppendLine(sb, 2, "...queryOptions");
        CodeFormatter.AppendLine(sb, 1, $"}} as UseQueryOptions<{awaitedReturnTypeName}, TError, TData> & {{ queryKey: QueryKey }};");
        CodeFormatter.AppendLine(sb, 0, "};");
        CodeFormatter.AppendEmptyLine(sb);
    }

    /// <summary>
    /// Рендерит infinite query support
    /// </summary>
    private void RenderInfiniteQuerySupport(StringBuilder sb, OperationTemplateData operation, string functionName, string clientMethodName, TypeScriptGenerationOptions options)
    {
        var errorType = "Error";
        var infiniteHookName = $"use{_namingConverter.ToPascalCase(functionName)}Infinite";
        var infiniteQueryKeyFunctionName = $"get{_namingConverter.ToPascalCase(functionName)}InfiniteQueryKey";
        var infiniteQueryOptionsFunctionName = $"get{_namingConverter.ToPascalCase(functionName)}InfiniteQueryOptions";
        var queryKeyFunctionName = $"get{_namingConverter.ToPascalCase(functionName)}QueryKey";
        
        // Генерируем имена типов на основе имени функции клиента
        var functionNamePascal = _namingConverter.ToPascalCase(clientMethodName);
        var awaitedReturnTypeName = $"{functionNamePascal}AwaitedReturnType";
        
        // Генерируем Infinite Query Key функцию
        RenderInfiniteQueryKeyFunction(sb, operation, infiniteQueryKeyFunctionName, queryKeyFunctionName);
        
        // Генерируем Infinite Query Options функцию
        RenderInfiniteQueryOptionsFunction(sb, operation, functionName, infiniteQueryKeyFunctionName, infiniteQueryOptionsFunctionName, clientMethodName, options);
        
        // Генерируем Infinite Query Hook
        // Используем объектные представления вместо строк
        var hookParameters = BuildInfiniteQueryHookParameters(operation, awaitedReturnTypeName, options);
        var hookParametersStr = FormatFunctionParameters(hookParameters);
        var queryKeyParamsStr = string.Join(", ", BuildQueryKeyParameters(operation));
        
        CodeFormatter.AppendLine(sb, 0, $"export const {infiniteHookName} = <");
        CodeFormatter.AppendLine(sb, 1, $"TData = InfiniteData<{awaitedReturnTypeName}, unknown>,");
        CodeFormatter.AppendLine(sb, 1, $"TError = {errorType},");
        CodeFormatter.AppendLine(sb, 0, ">(");
        // Добавляем параметры функции с правильным отступом
        if (!string.IsNullOrEmpty(hookParametersStr))
        {
            CodeFormatter.AppendLine(sb, 1, hookParametersStr);
        }
        CodeFormatter.AppendLine(sb, 0, $") : UseInfiniteQueryResult<TData, TError> => {{");
        CodeFormatter.AppendEmptyLine(sb);
        
        if (string.IsNullOrEmpty(queryKeyParamsStr))
        {
            CodeFormatter.AppendLine(sb, 1, $"const queryOptions = {infiniteQueryOptionsFunctionName}(options);");
        }
        else
        {
            CodeFormatter.AppendLine(sb, 1, $"const queryOptions = {infiniteQueryOptionsFunctionName}({queryKeyParamsStr}, options);");
        }
        CodeFormatter.AppendLine(sb, 1, "const query = useInfiniteQuery(queryOptions);");
        CodeFormatter.AppendEmptyLine(sb);
        CodeFormatter.AppendLine(sb, 1, "return query;");
        CodeFormatter.AppendLine(sb, 0, "};");
        CodeFormatter.AppendEmptyLine(sb);
    }
    
    /// <summary>
    /// Рендерит infinite query key функцию
    /// </summary>
    private void RenderInfiniteQueryKeyFunction(StringBuilder sb, OperationTemplateData operation, string infiniteQueryKeyFunctionName, string queryKeyFunctionName)
    {
        // Параметры уже дедуплицированы на этапе извлечения в OperationExtractor
        var queryKeyParams = BuildQueryKeyParameters(operation);
        
        // Собираем параметры для query key функции (с типами)
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
        
        // Генерируем query key массив (используем обычный query key, обернутый в 'infinite')
        var keyItems = new List<string> { $"'{operation.Path}'" };
        
        // Добавляем параметры из BuildQueryKeyParameters (уже уникальные)
        keyItems.AddRange(queryKeyParams);
        
        var keyArray = string.Join(", ", keyItems);
        
        CodeFormatter.AppendLine(sb, 0, $"export const {infiniteQueryKeyFunctionName} = (");
        if (!string.IsNullOrEmpty(functionParams))
        {
            CodeFormatter.AppendLine(sb, 1, functionParams);
        }
        CodeFormatter.AppendLine(sb, 0, ") => {");
        CodeFormatter.AppendEmptyLine(sb);
        CodeFormatter.AppendLine(sb, 1, $"return ['infinite', [{keyArray}]];");
        CodeFormatter.AppendLine(sb, 0, "};");
        CodeFormatter.AppendEmptyLine(sb);
    }
    
    /// <summary>
    /// Рендерит infinite query options функцию
    /// </summary>
    private void RenderInfiniteQueryOptionsFunction(StringBuilder sb, OperationTemplateData operation, string functionName, string infiniteQueryKeyFunctionName, string infiniteQueryOptionsFunctionName, string clientMethodName, TypeScriptGenerationOptions options)
    {
        var errorType = "Error";
        
        // Генерируем имена типов на основе имени функции клиента
        var functionNamePascal = _namingConverter.ToPascalCase(clientMethodName);
        var awaitedReturnTypeName = $"{functionNamePascal}AwaitedReturnType";
        
        // Строим параметры с Partial<UseInfiniteQueryOptions>
        // Используем объектные представления вместо строк
        var parameters = BuildInfiniteQueryHookParameters(operation, awaitedReturnTypeName, options);
        var functionParamsStr = FormatFunctionParameters(parameters);
        var queryKeyParams = BuildQueryKeyParameters(operation);
        var queryKeyParamsStr = string.Join(", ", queryKeyParams);
        var infiniteQueryFnParams = BuildQueryFnClientMethodParameters(operation, clientMethodName, options);
        
        CodeFormatter.AppendLine(sb, 0, $"export const {infiniteQueryOptionsFunctionName} = <");
        CodeFormatter.AppendLine(sb, 1, $"TData = InfiniteData<{awaitedReturnTypeName}, unknown>,");
        CodeFormatter.AppendLine(sb, 1, $"TError = {errorType},");
        CodeFormatter.AppendLine(sb, 0, ">(");
        // Добавляем параметры функции с правильным отступом
        if (!string.IsNullOrEmpty(functionParamsStr))
        {
            CodeFormatter.AppendLine(sb, 1, functionParamsStr);
        }
        CodeFormatter.AppendLine(sb, 0, $") => {{");
        CodeFormatter.AppendEmptyLine(sb);
        if (options.HttpClientLibrary == HttpClientLibrary.Axios)
        {
            CodeFormatter.AppendLine(sb, 1, "const { query: queryOptions, timeout, axiosInstance } = options ?? {};");
        }
        else
        {
            CodeFormatter.AppendLine(sb, 1, "const { query: queryOptions, timeout } = options ?? {};");
        }
        CodeFormatter.AppendEmptyLine(sb);
        
        if (string.IsNullOrEmpty(queryKeyParamsStr))
        {
            CodeFormatter.AppendLine(sb, 1, $"const queryKey = queryOptions?.queryKey ?? {infiniteQueryKeyFunctionName}();");
        }
        else
        {
            CodeFormatter.AppendLine(sb, 1, $"const queryKey = queryOptions?.queryKey ?? {infiniteQueryKeyFunctionName}({queryKeyParamsStr});");
        }
        CodeFormatter.AppendLine(sb, 1, $"const queryFn: QueryFunction<{awaitedReturnTypeName}, QueryKey, unknown> = ({{ signal }}) =>");
        CodeFormatter.AppendLine(sb, 2, $"{clientMethodName}({infiniteQueryFnParams});");
        CodeFormatter.AppendEmptyLine(sb);
        CodeFormatter.AppendLine(sb, 1, "return {");
        CodeFormatter.AppendLine(sb, 2, "queryKey,");
        CodeFormatter.AppendLine(sb, 2, "queryFn,");
        CodeFormatter.AppendLine(sb, 2, "enabled: true,");
        CodeFormatter.AppendLine(sb, 2, "...queryOptions");
        CodeFormatter.AppendLine(sb, 1, $"}} as UseInfiniteQueryOptions<{awaitedReturnTypeName}, TError, TData, QueryKey, unknown>;");
        CodeFormatter.AppendLine(sb, 0, "};");
        CodeFormatter.AppendEmptyLine(sb);
    }

    /// <summary>
    /// Рендерит prefetch support
    /// </summary>
    private void RenderPrefetchSupport(StringBuilder sb, OperationTemplateData operation, string functionName, string queryKeyFunctionName, string clientMethodName, TypeScriptGenerationOptions options)
    {
        var prefetchFunctionName = $"prefetch{_namingConverter.ToPascalCase(functionName)}";
        var prefetchInfiniteFunctionName = $"prefetch{_namingConverter.ToPascalCase(functionName)}Infinite";
        var queryOptionsFunctionName = $"get{_namingConverter.ToPascalCase(functionName)}QueryOptions";
        var infiniteQueryOptionsFunctionName = $"get{_namingConverter.ToPascalCase(functionName)}InfiniteQueryOptions";
        
        // Генерируем имена типов на основе имени функции клиента
        var functionNamePascal = _namingConverter.ToPascalCase(clientMethodName);
        var awaitedReturnTypeName = $"{functionNamePascal}AwaitedReturnType";
        
        var functionParams = BuildQueryHookParameters(operation, awaitedReturnTypeName, options);
        var functionParamsStr = FormatFunctionParameters(functionParams);
        var queryKeyParams = BuildQueryKeyParameters(operation);
        var queryKeyParamsStr = string.Join(", ", queryKeyParams);
        
        // Prefetch для обычного query
        CodeFormatter.AppendLine(sb, 0, $"export const {prefetchFunctionName} = async <");
        CodeFormatter.AppendLine(sb, 1, $"TData = {awaitedReturnTypeName},");
        CodeFormatter.AppendLine(sb, 1, "TError = Error,");
        CodeFormatter.AppendLine(sb, 0, ">(");
        CodeFormatter.AppendLine(sb, 1, "queryClient: QueryClient,");
        // Добавляем параметры функции с правильным отступом
        if (!string.IsNullOrEmpty(functionParamsStr))
        {
            CodeFormatter.AppendLine(sb, 1, functionParamsStr);
        }
        CodeFormatter.AppendLine(sb, 0, $") : Promise<QueryClient> => {{");
        CodeFormatter.AppendEmptyLine(sb);
        
        if (string.IsNullOrEmpty(queryKeyParamsStr))
        {
            CodeFormatter.AppendLine(sb, 1, $"const queryOptions = {queryOptionsFunctionName}(options);");
        }
        else
        {
            CodeFormatter.AppendLine(sb, 1, $"const queryOptions = {queryOptionsFunctionName}({queryKeyParamsStr}, options);");
        }
        CodeFormatter.AppendLine(sb, 1, "await queryClient.prefetchQuery(queryOptions);");
        CodeFormatter.AppendEmptyLine(sb);
        CodeFormatter.AppendLine(sb, 1, "return queryClient;");
        CodeFormatter.AppendLine(sb, 0, "};");
        CodeFormatter.AppendEmptyLine(sb);
        
        // Prefetch для infinite query
        // Используем отдельный метод для генерации параметров с UseInfiniteQueryOptions
        var infiniteFunctionParams = BuildInfiniteQueryHookParameters(operation, awaitedReturnTypeName, options);
        var infiniteFunctionParamsStr = FormatFunctionParameters(infiniteFunctionParams);
        CodeFormatter.AppendLine(sb, 0, $"export const {prefetchInfiniteFunctionName} = async <");
        CodeFormatter.AppendLine(sb, 1, $"TData = InfiniteData<{awaitedReturnTypeName}, unknown>,");
        CodeFormatter.AppendLine(sb, 1, "TError = Error,");
        CodeFormatter.AppendLine(sb, 0, ">(");
        CodeFormatter.AppendLine(sb, 1, "queryClient: QueryClient,");
        // Добавляем параметры функции с правильным отступом
        if (!string.IsNullOrEmpty(infiniteFunctionParamsStr))
        {
            CodeFormatter.AppendLine(sb, 1, infiniteFunctionParamsStr);
        }
        CodeFormatter.AppendLine(sb, 0, $") : Promise<QueryClient> => {{");
        CodeFormatter.AppendEmptyLine(sb);
        
        if (string.IsNullOrEmpty(queryKeyParamsStr))
        {
            CodeFormatter.AppendLine(sb, 1, $"const queryOptions = {infiniteQueryOptionsFunctionName}(options);");
        }
        else
        {
            CodeFormatter.AppendLine(sb, 1, $"const queryOptions = {infiniteQueryOptionsFunctionName}({queryKeyParamsStr}, options);");
        }
        CodeFormatter.AppendLine(sb, 1, "await queryClient.prefetchInfiniteQuery(queryOptions);");
        CodeFormatter.AppendEmptyLine(sb);
        CodeFormatter.AppendLine(sb, 1, "return queryClient;");
        CodeFormatter.AppendLine(sb, 0, "};");
        CodeFormatter.AppendEmptyLine(sb);
    }

    /// <summary>
    /// Рендерит mutation options helper
    /// </summary>
    private void RenderMutationOptionsHelper(StringBuilder sb, OperationTemplateData operation, string functionName, string clientMethodName, TypeScriptGenerationOptions options)
    {
        var errorType = "Error";
        var helperName = $"get{_namingConverter.ToPascalCase(functionName)}MutationOptions";
        
        // Генерируем имена типов на основе имени функции клиента
        var functionNamePascal = _namingConverter.ToPascalCase(clientMethodName);
        var awaitedReturnTypeName = $"{functionNamePascal}AwaitedReturnType";
        
        // Формируем тип variables
        var variablesType = BuildMutationVariablesType(operation);
        var variablesTypeStr = FormatTypeObjectDefinition(variablesType);
        var mutationParams = BuildMutationHookParameters(operation, awaitedReturnTypeName, variablesType, options);
        var mutationParamsStr = FormatMutationHookParameters(mutationParams, variablesType, awaitedReturnTypeName, options);
        
        // Формируем деструктуризацию variables
        var variablesDestructuring = BuildMutationVariablesDestructuring(operation);
        
        // Формируем аргументы для вызова клиентского метода
        var clientMethodParams = BuildClientMethodParameters(operation, clientMethodName, options);
        
        CodeFormatter.AppendLine(sb, 0, $"export const {helperName} = <");
        CodeFormatter.AppendLine(sb, 1, $"TError = {errorType},");
        CodeFormatter.AppendLine(sb, 1, "TContext = unknown,");
        CodeFormatter.AppendLine(sb, 0, ">(options?: {");
        CodeFormatter.AppendLine(sb, 1, "mutation?: UseMutationOptions<");
        CodeFormatter.AppendLine(sb, 2, $"{awaitedReturnTypeName},");
        CodeFormatter.AppendLine(sb, 2, "TError,");
        
        // Если properties пустой, генерируем просто {}
        if (variablesType.Properties.Count == 0)
        {
            CodeFormatter.AppendLine(sb, 2, "{},");
        }
        else
        {
            CodeFormatter.AppendLine(sb, 2, "{");
            // Добавляем переменные с правильным отступом
            foreach (var prop in variablesType.Properties)
            {
                CodeFormatter.AppendLine(sb, 2, prop.Format(0).TrimStart());
            }
            CodeFormatter.AppendLine(sb, 2, "},");
        }
        
        CodeFormatter.AppendLine(sb, 2, "TContext");
        CodeFormatter.AppendLine(sb, 1, ">;");
        CodeFormatter.AppendLine(sb, 1, "timeout?: number;");
        if (options.HttpClientLibrary == HttpClientLibrary.Axios)
        {
            CodeFormatter.AppendLine(sb, 1, "axiosInstance?: AxiosInstance;");
        }
        CodeFormatter.AppendLine(sb, 0, "}): UseMutationOptions<");
        CodeFormatter.AppendLine(sb, 1, $"{awaitedReturnTypeName},");
        CodeFormatter.AppendLine(sb, 1, "TError,");
        
        // Если properties пустой, генерируем просто {}
        if (variablesType.Properties.Count == 0)
        {
            CodeFormatter.AppendLine(sb, 1, "{},");
        }
        else
        {
            CodeFormatter.AppendLine(sb, 1, "{");
            // Добавляем переменные с правильным отступом
            foreach (var prop in variablesType.Properties)
            {
                CodeFormatter.AppendLine(sb, 2, prop.Format(0).TrimStart());
            }
            CodeFormatter.AppendLine(sb, 1, "},");
        }
        
        CodeFormatter.AppendLine(sb, 1, "TContext");
        CodeFormatter.AppendLine(sb, 0, "> => {");
        CodeFormatter.AppendEmptyLine(sb);
        CodeFormatter.AppendLine(sb, 1, $"const mutationKey = [\"{clientMethodName}\"];");
        CodeFormatter.AppendEmptyLine(sb);
        if (options.HttpClientLibrary == HttpClientLibrary.Axios)
        {
            CodeFormatter.AppendLine(sb, 1, "const { mutation: mutationOptions");
            CodeFormatter.AppendLine(sb, 1, ", axiosInstance: requestOptions");
            CodeFormatter.AppendLine(sb, 1, ", timeout } = options");
        }
        else
        {
            CodeFormatter.AppendLine(sb, 1, "const { mutation: mutationOptions");
            CodeFormatter.AppendLine(sb, 1, ", timeout } = options");
        }
        CodeFormatter.AppendLine(sb, 1, "");
        CodeFormatter.AppendLine(sb, 1, "? options.mutation &&");
        CodeFormatter.AppendLine(sb, 1, "");
        CodeFormatter.AppendLine(sb, 1, "  \"mutationKey\" in options.mutation &&");
        CodeFormatter.AppendLine(sb, 1, "");
        CodeFormatter.AppendLine(sb, 1, "  options.mutation.mutationKey");
        CodeFormatter.AppendLine(sb, 1, "");
        CodeFormatter.AppendLine(sb, 1, "  ? options");
        CodeFormatter.AppendLine(sb, 1, "");
        CodeFormatter.AppendLine(sb, 1, "  : { ...options, mutation: { ...options.mutation, mutationKey } }");
        CodeFormatter.AppendLine(sb, 1, "");
        if (options.HttpClientLibrary == HttpClientLibrary.Axios)
        {
            CodeFormatter.AppendLine(sb, 1, ": { mutation: { mutationKey }");
            CodeFormatter.AppendLine(sb, 1, ", axiosInstance: undefined");
            CodeFormatter.AppendLine(sb, 1, ", timeout: undefined };");
        }
        else
        {
            CodeFormatter.AppendLine(sb, 1, ": { mutation: { mutationKey }");
            CodeFormatter.AppendLine(sb, 1, ", timeout: undefined };");
        }
        CodeFormatter.AppendEmptyLine(sb);
        CodeFormatter.AppendLine(sb, 1, $"const mutationFn: MutationFunction<");
        CodeFormatter.AppendLine(sb, 1, "");
        CodeFormatter.AppendLine(sb, 2, $"{awaitedReturnTypeName},");
        CodeFormatter.AppendLine(sb, 1, "");
        
        // Если properties пустой, генерируем просто {}
        if (variablesType.Properties.Count == 0)
        {
            CodeFormatter.AppendLine(sb, 2, "{}");
        }
        else
        {
            CodeFormatter.AppendLine(sb, 2, "{");
            CodeFormatter.AppendLine(sb, 1, "");
            // Добавляем переменные с правильным отступом
            foreach (var prop in variablesType.Properties)
            {
                CodeFormatter.AppendLine(sb, 2, prop.Format(0).TrimStart());
            }
            CodeFormatter.AppendLine(sb, 1, "");
            CodeFormatter.AppendLine(sb, 2, "}");
        }
        
        CodeFormatter.AppendLine(sb, 1, "");
        CodeFormatter.AppendLine(sb, 1, "> = (props) => {");
        CodeFormatter.AppendLine(sb, 1, "");
        CodeFormatter.AppendLine(sb, 2, $"const {{ {variablesDestructuring} }} = props ?? {{}};");
        CodeFormatter.AppendEmptyLine(sb);
        
        // Собираем аргументы для вызова клиентского метода
        // Порядок должен соответствовать сигнатуре axios метода:
        // path params, query params, header params, request body (или FormData properties), axiosInstance, signal, timeout
        var methodArgs = new List<string>();
        if (!string.IsNullOrEmpty(clientMethodParams))
        {
            methodArgs.Add(clientMethodParams);
        }
        if (options.HttpClientLibrary == HttpClientLibrary.Axios)
        {
            methodArgs.Add("requestOptions");
        }
        // Для mutations signal обычно не используется, но если axios метод его принимает, можно передать undefined
        // timeout передаем из options
        methodArgs.Add("undefined"); // signal - не используется в mutations
        methodArgs.Add("timeout");
        var methodArgsStr = string.Join(", ", methodArgs);
        CodeFormatter.AppendEmptyLine(sb);
        CodeFormatter.AppendLine(sb, 2, $"return {clientMethodName}({methodArgsStr});");
        CodeFormatter.AppendEmptyLine(sb);
        CodeFormatter.AppendLine(sb, 1, "};");
        CodeFormatter.AppendEmptyLine(sb);
        CodeFormatter.AppendEmptyLine(sb);
        CodeFormatter.AppendLine(sb, 1, "return { mutationFn, ...mutationOptions };");
        CodeFormatter.AppendEmptyLine(sb);
        CodeFormatter.AppendLine(sb, 0, "};");
        CodeFormatter.AppendEmptyLine(sb);
    }

    /// <summary>
    /// Рендерит mutation hook функцию
    /// </summary>
    private void RenderMutationHookFunction(StringBuilder sb, OperationTemplateData operation, string hookName, string functionName, string clientMethodName, TypeScriptGenerationOptions options)
    {
        var errorType = "Error";
        var helperName = $"get{_namingConverter.ToPascalCase(functionName)}MutationOptions";
        
        // Генерируем имена типов на основе имени функции клиента
        var functionNamePascal = _namingConverter.ToPascalCase(clientMethodName);
        var awaitedReturnTypeName = $"{functionNamePascal}AwaitedReturnType";
        
        // Формируем тип variables
        var variablesType = BuildMutationVariablesType(operation);
        var variablesTypeStr = FormatTypeObjectDefinition(variablesType);
        var mutationParams = BuildMutationHookParameters(operation, awaitedReturnTypeName, variablesType, options);
        var mutationParamsStr = FormatMutationHookParameters(mutationParams, variablesType, awaitedReturnTypeName, options);
        
        CodeFormatter.AppendLine(sb, 0, $"export function {hookName}<TError = {errorType}, TContext = unknown>(");
        if (!string.IsNullOrEmpty(mutationParamsStr))
        {
            CodeFormatter.AppendLine(sb, 1, mutationParamsStr);
        }
        // Форматируем тип возвращаемого значения
        var returnTypeVariables = variablesType.Properties.Count == 0 
            ? "{}" 
            : $"{{\n{variablesTypeStr}\n  }}";
        CodeFormatter.AppendLine(sb, 0, $") : UseMutationResult<{awaitedReturnTypeName}, TError, {returnTypeVariables}, TContext> {{");
        CodeFormatter.AppendEmptyLine(sb);
        CodeFormatter.AppendLine(sb, 1, $"const mutationOptions = {helperName}(options);");
        CodeFormatter.AppendEmptyLine(sb);
        CodeFormatter.AppendLine(sb, 1, "return useMutation(mutationOptions, queryClient);");
        CodeFormatter.AppendLine(sb, 0, "}");
        CodeFormatter.AppendEmptyLine(sb);
    }

    #region Private Helper Methods


    /// <summary>
    /// Строит параметры для query hook
    /// </summary>
    private IReadOnlyList<FunctionParameter> BuildQueryHookParameters(OperationTemplateData operation, string awaitedReturnTypeName, TypeScriptGenerationOptions options)
    {
        // Параметры уже дедуплицированы на этапе извлечения в OperationExtractor
        var parameters = new List<FunctionParameter>();
        
        // Path параметры
        parameters.AddRange(FunctionParameterBuilder.FromParameters(operation.PathParameters));
        
        // Query параметры
        if (operation.UseQueryParametersModel && !string.IsNullOrEmpty(operation.QueryParametersModelTypeName))
        {
            parameters.Add(FunctionParameterBuilder.FromQueryParametersModel(
                operation.QueryParametersModelTypeName, 
                operation.QueryParameters));
        }
        else
        {
            parameters.AddRange(FunctionParameterBuilder.FromParameters(operation.QueryParameters));
        }
        
        // Query options
        var optionsDefinition = OptionsDefinitionBuilder.BuildQueryOptions(awaitedReturnTypeName, options);
        parameters.Add(FunctionParameter.CreateOptionsParameter(optionsDefinition));
        
        return parameters;
    }
    
    /// <summary>
    /// Форматирует список параметров функции в строку для использования в сигнатуре
    /// Использует CodeFormatter для правильного форматирования
    /// </summary>
    private string FormatFunctionParameters(IReadOnlyList<FunctionParameter> parameters, int indentLevel = 1)
    {
        if (parameters.Count == 0)
            return string.Empty;
        
        var sb = new StringBuilder();
        
        for (int i = 0; i < parameters.Count; i++)
        {
            var param = parameters[i];
            var isLast = i == parameters.Count - 1;
            
            // Если это options параметр с многострочным типом, форматируем его специальным образом
            if (param.Name == "options" && param.Type.Contains('\n'))
            {
                // Извлекаем тип из многострочного формата
                var typeLines = param.Type.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                
                CodeFormatter.AppendLine(sb, indentLevel, "options?: {");
                
                foreach (var typeLine in typeLines)
                {
                    var trimmed = typeLine.Trim();
                    if (!string.IsNullOrEmpty(trimmed))
                    {
                        CodeFormatter.AppendLine(sb, indentLevel + 1, trimmed);
                    }
                }
                
                // Добавляем запятую в конце, если это не последний параметр
                var closingBrace = isLast ? "}" : "},";
                CodeFormatter.AppendLine(sb, indentLevel, closingBrace);
            }
            else
            {
                // Добавляем запятую в конце, если это не последний параметр
                var formatted = param.Format();
                if (!isLast)
                {
                    formatted += ",";
                }
                CodeFormatter.AppendLine(sb, indentLevel, formatted);
            }
        }
        
        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Строит параметры для infinite query hook (с UseInfiniteQueryOptions вместо UseQueryOptions)
    /// </summary>
    private IReadOnlyList<FunctionParameter> BuildInfiniteQueryHookParameters(OperationTemplateData operation, string awaitedReturnTypeName, TypeScriptGenerationOptions options)
    {
        // Параметры уже дедуплицированы на этапе извлечения в OperationExtractor
        var parameters = new List<FunctionParameter>();
        
        // Path параметры
        parameters.AddRange(FunctionParameterBuilder.FromParameters(operation.PathParameters));

        // Query параметры
        if (operation.UseQueryParametersModel && !string.IsNullOrEmpty(operation.QueryParametersModelTypeName))
        {
            parameters.Add(FunctionParameterBuilder.FromQueryParametersModel(
                operation.QueryParametersModelTypeName, 
                operation.QueryParameters));
        }
        else
        {
            parameters.AddRange(FunctionParameterBuilder.FromParameters(operation.QueryParameters));
        }
        
        // Query options с Partial<UseInfiniteQueryOptions> и правильными generic параметрами
        var optionsDefinition = OptionsDefinitionBuilder.BuildInfiniteQueryOptions(awaitedReturnTypeName, options);
        parameters.Add(FunctionParameter.CreateOptionsParameter(optionsDefinition));
        
        return parameters;
    }

    /// <summary>
    /// Строит параметры для query key функции
    /// </summary>
    private List<string> BuildQueryKeyParameters(OperationTemplateData operation)
    {
        // Параметры уже дедуплицированы на этапе извлечения в OperationExtractor
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
    /// Строит параметры для вызова метода клиента (базовый набор + signal/timeout/axiosInstance для mutations)
    /// </summary>
    private string BuildClientMethodParameters(OperationTemplateData operation, string clientMethodName, TypeScriptGenerationOptions options)
    {
        // Параметры уже дедуплицированы на этапе извлечения в OperationExtractor
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
        // Для FormData передаем отдельные параметры (не объект), так как в axios методе они передаются отдельно
        if (operation.IsFormData && operation.FormDataProperties != null && operation.FormDataProperties.Count > 0)
        {
            foreach (var formProp in operation.FormDataProperties)
            {
                parameters.Add(formProp.VariableName);
            }
        }
        else if (operation.HasRequestBody && !string.IsNullOrEmpty(operation.RequestBodyParameter))
        {
            parameters.Add(operation.RequestBodyParameter);
        }
        
        return string.Join(", ", parameters);
    }
    
    /// <summary>
    /// Строит параметры для вызова метода клиента в queryFn (включает signal, timeout, axiosInstance если нужно)
    /// </summary>
    private string BuildQueryFnClientMethodParameters(OperationTemplateData operation, string clientMethodName, TypeScriptGenerationOptions options)
    {
        // Параметры уже дедуплицированы на этапе извлечения в OperationExtractor
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
        // Для FormData передаем отдельные параметры (не объект), так как в axios методе они передаются отдельно
        if (operation.IsFormData && operation.FormDataProperties != null && operation.FormDataProperties.Count > 0)
        {
            foreach (var formProp in operation.FormDataProperties)
            {
                parameters.Add(formProp.VariableName);
            }
        }
        else if (operation.HasRequestBody && !string.IsNullOrEmpty(operation.RequestBodyParameter))
        {
            parameters.Add(operation.RequestBodyParameter);
        }
        
        // Добавляем axiosInstance, signal, timeout (опциональные)
        // Порядок должен соответствовать порядку параметров в axios методах
        if (options.HttpClientLibrary == HttpClientLibrary.Axios)
        {
            parameters.Add("axiosInstance");
        }
        parameters.Add("signal");
        parameters.Add("timeout");
        
        return string.Join(",\n      ", parameters);
    }

    /// <summary>
    /// Строит тип TVariables для mutation hook (объект с полями параметров)
    /// Возвращает TypeObjectDefinition вместо строки
    /// </summary>
    private TypeObjectDefinition BuildMutationVariablesType(OperationTemplateData operation)
    {
        return TypeDefinitionBuilder.BuildMutationVariablesType(operation);
    }
    
    /// <summary>
    /// Форматирует TypeObjectDefinition в строку для использования в типах
    /// </summary>
    private string FormatTypeObjectDefinition(TypeObjectDefinition typeObject, int indentLevel = 0)
    {
        if (typeObject.Properties.Count == 0)
            return "";
        
        var lines = new List<string>();
        foreach (var property in typeObject.Properties)
        {
            lines.Add(property.Format(indentLevel));
        }
        
        return string.Join("\n", lines);
    }
    
    /// <summary>
    /// Строит строку деструктуризации variables для mutationFn
    /// </summary>
    private string BuildMutationVariablesDestructuring(OperationTemplateData operation)
    {
        // Параметры уже дедуплицированы на этапе извлечения в OperationExtractor
        var properties = new List<string>();
        
        // Path параметры
        foreach (var param in operation.PathParameters)
        {
            properties.Add(param.VariableName);
        }
        
        // Query параметры
        if (operation.UseQueryParametersModel && !string.IsNullOrEmpty(operation.QueryParametersModelTypeName))
        {
            properties.Add("params");
        }
        else
        {
            foreach (var param in operation.QueryParameters)
            {
                properties.Add(param.VariableName);
            }
        }
        
        // Header параметры
        foreach (var param in operation.HeaderParameters)
        {
            properties.Add(param.VariableName);
        }
        
        // Request body
        // Для FormData деструктурируем отдельные свойства, а не объект request
        if (operation.IsFormData && operation.FormDataProperties != null && operation.FormDataProperties.Count > 0)
        {
            foreach (var formProp in operation.FormDataProperties)
            {
                properties.Add(formProp.VariableName);
            }
        }
        else if (operation.HasRequestBody && !string.IsNullOrEmpty(operation.RequestBodyParameter))
        {
            properties.Add(operation.RequestBodyParameter);
        }
        
        return properties.Count > 0 ? string.Join(", ", properties) : "";
    }
    
    /// <summary>
    /// Строит параметры для mutation hook (только options и queryClient)
    /// Использует объектные представления вместо строк
    /// </summary>
    private IReadOnlyList<FunctionParameter> BuildMutationHookParameters(OperationTemplateData operation, string awaitedReturnTypeName, TypeObjectDefinition variablesType, TypeScriptGenerationOptions options)
    {
        var parameters = new List<FunctionParameter>();
        
        // Options параметр
        var optionsDefinition = OptionsDefinitionBuilder.BuildMutationOptions(awaitedReturnTypeName, variablesType, options);
        parameters.Add(FunctionParameter.CreateOptionsParameter(optionsDefinition));
        
        // QueryClient параметр
        parameters.Add(new FunctionParameter
        {
            Name = "queryClient",
            Type = "QueryClient",
            IsOptional = true
        });
        
        return parameters;
    }
    
    /// <summary>
    /// Форматирует параметры mutation hook с учетом многострочных типов
    /// Использует CodeFormatter для правильного форматирования
    /// </summary>
    private string FormatMutationHookParameters(IReadOnlyList<FunctionParameter> parameters, TypeObjectDefinition variablesType, string awaitedReturnTypeName, TypeScriptGenerationOptions options, int indentLevel = 1)
    {
        if (parameters.Count == 0)
            return string.Empty;
        
        var sb = new StringBuilder();
        
        for (int i = 0; i < parameters.Count; i++)
        {
            var param = parameters[i];
            var isLast = i == parameters.Count - 1;
            
            if (param.Name == "options")
            {
                // Специальная обработка для options с многострочным типом
                CodeFormatter.AppendLine(sb, indentLevel, "options?: {");
                CodeFormatter.AppendLine(sb, indentLevel + 1, "mutation?: UseMutationOptions<");
                CodeFormatter.AppendLine(sb, indentLevel + 2, $"{awaitedReturnTypeName},");
                CodeFormatter.AppendLine(sb, indentLevel + 2, "TError,");
                
                // Если properties пустой, генерируем просто {}
                if (variablesType.Properties.Count == 0)
                {
                    CodeFormatter.AppendLine(sb, indentLevel + 2, "{},");
                }
                else
                {
                    CodeFormatter.AppendLine(sb, indentLevel + 2, "{");
                    foreach (var prop in variablesType.Properties)
                    {
                        CodeFormatter.AppendLine(sb, indentLevel + 2, prop.Format(0).TrimStart());
                    }
                    CodeFormatter.AppendLine(sb, indentLevel + 2, "},");
                }
                
                CodeFormatter.AppendLine(sb, indentLevel + 2, "TContext");
                CodeFormatter.AppendLine(sb, indentLevel + 1, ">;");
                CodeFormatter.AppendLine(sb, indentLevel + 1, "timeout?: number;");
                if (options.HttpClientLibrary == HttpClientLibrary.Axios)
                {
                    CodeFormatter.AppendLine(sb, indentLevel + 1, "axiosInstance?: AxiosInstance;");
                }
                
                // Добавляем запятую в конце, если это не последний параметр
                var closingBrace = isLast ? "}" : "},";
                CodeFormatter.AppendLine(sb, indentLevel, closingBrace);
            }
            else
            {
                // Добавляем запятую в конце, если это не последний параметр
                var formatted = param.Format();
                if (!isLast)
                {
                    formatted += ",";
                }
                CodeFormatter.AppendLine(sb, indentLevel, formatted);
            }
        }
        
        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Собирает все импорты моделей для операции (без рендеринга)
    /// </summary>
    public HashSet<string> CollectModelImports(OperationTemplateData operation, string? modelsPath)
    {
        var dependencies = new HashSet<string>();
        
        // Добавляем зависимости из operation.Dependencies
        if (operation.Dependencies != null)
        {
            foreach (var dependency in operation.Dependencies)
            {
                if (!string.IsNullOrEmpty(dependency) && !IsBuiltInTypeScriptType(dependency))
                {
                    var typeName = ExtractTypeName(dependency);
                    if (!string.IsNullOrEmpty(typeName) && !IsBuiltInTypeScriptType(typeName))
                    {
                        dependencies.Add(typeName);
                    }
                }
            }
        }
        
        // Извлекаем типы из ReturnTypeInfo (response модели)
        if (operation.ReturnTypeInfo != null)
        {
            ExtractTypeNamesFromTypeInfo(operation.ReturnTypeInfo, dependencies);
        }
        
        // Извлекаем типы из RequestBodyTypeInfo (request модели)
        if (operation.RequestBodyTypeInfo != null)
        {
            ExtractTypeNamesFromTypeInfo(operation.RequestBodyTypeInfo, dependencies);
        }
        
        // Извлекаем типы из параметров (path, query, header)
        foreach (var param in operation.PathParameters)
        {
            if (param.TypeInfo != null)
            {
                ExtractTypeNamesFromTypeInfo(param.TypeInfo, dependencies);
            }
        }
        
        foreach (var param in operation.QueryParameters)
        {
            if (param.TypeInfo != null)
            {
                ExtractTypeNamesFromTypeInfo(param.TypeInfo, dependencies);
            }
        }
        
        foreach (var param in operation.HeaderParameters)
        {
            if (param.TypeInfo != null)
            {
                ExtractTypeNamesFromTypeInfo(param.TypeInfo, dependencies);
            }
        }
        
        // Также добавляем QueryParametersModelTypeName, если используется
        if (operation.UseQueryParametersModel && !string.IsNullOrEmpty(operation.QueryParametersModelTypeName))
        {
            dependencies.Add(operation.QueryParametersModelTypeName);
        }
        
        return dependencies;
    }
    
    /// <summary>
    /// Собирает имена методов клиента для операции
    /// </summary>
    public string? CollectClientMethodName(OperationTemplateData operation)
    {
        return _namingConverter.ToCamelCase(operation.MethodName);
    }
    
    /// <summary>
    /// Рендерит импорты TanStack Query типов для queries
    /// </summary>
    /// <returns>Строка с импортами для query операций</returns>
    public string RenderQueryImports()
    {
        var sb = new StringBuilder();
        CodeFormatter.AppendLine(sb, 0, "import {");
        CodeFormatter.AppendLine(sb, 1, "InfiniteData,");
        CodeFormatter.AppendLine(sb, 1, "MutationFunction,");
        CodeFormatter.AppendLine(sb, 1, "QueryClient,");
        CodeFormatter.AppendLine(sb, 1, "QueryFunction,");
        CodeFormatter.AppendLine(sb, 1, "QueryKey,");
        CodeFormatter.AppendLine(sb, 1, "useInfiniteQuery,");
        CodeFormatter.AppendLine(sb, 1, "UseInfiniteQueryOptions,");
        CodeFormatter.AppendLine(sb, 1, "UseInfiniteQueryResult,");
        CodeFormatter.AppendLine(sb, 1, "useMutation,");
        CodeFormatter.AppendLine(sb, 1, "UseMutationOptions,");
        CodeFormatter.AppendLine(sb, 1, "UseMutationResult,");
        CodeFormatter.AppendLine(sb, 1, "useQuery,");
        CodeFormatter.AppendLine(sb, 1, "UseQueryOptions,");
        CodeFormatter.AppendLine(sb, 1, "UseQueryResult,");
        CodeFormatter.AppendLine(sb, 0, "} from '@tanstack/react-query';");
        CodeFormatter.AppendEmptyLine(sb);
        return sb.ToString();
    }

    /// <summary>
    /// Рендерит импорты TanStack Query типов для mutations
    /// </summary>
    /// <returns>Строка с импортами для mutation операций (пустая, так как все импорты в RenderQueryImports)</returns>
    public string RenderMutationImports()
    {
        // Импорты для mutations уже включены в RenderQueryImports
        return string.Empty;
    }

    /// <summary>
    /// Рендерит импорты моделей для операции
    /// </summary>
    private void RenderModelImports(StringBuilder sb, OperationTemplateData operation, string? modelsPath)
    {
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
        
        // Извлекаем типы из ReturnTypeInfo (response модели)
        if (operation.ReturnTypeInfo != null)
        {
            ExtractTypeNamesFromTypeInfo(operation.ReturnTypeInfo, dependencies);
        }
        
        // Извлекаем типы из RequestBodyTypeInfo (request модели)
        if (operation.RequestBodyTypeInfo != null)
        {
            ExtractTypeNamesFromTypeInfo(operation.RequestBodyTypeInfo, dependencies);
        }
        
        // Извлекаем типы из параметров (path, query, header)
        foreach (var param in operation.PathParameters)
        {
            if (param.TypeInfo != null)
            {
                ExtractTypeNamesFromTypeInfo(param.TypeInfo, dependencies);
            }
        }
        
        foreach (var param in operation.QueryParameters)
        {
            if (param.TypeInfo != null)
            {
                ExtractTypeNamesFromTypeInfo(param.TypeInfo, dependencies);
            }
        }
        
        foreach (var param in operation.HeaderParameters)
        {
            if (param.TypeInfo != null)
            {
                ExtractTypeNamesFromTypeInfo(param.TypeInfo, dependencies);
            }
        }
        
        // Также добавляем QueryParametersModelTypeName, если используется
        if (operation.UseQueryParametersModel && !string.IsNullOrEmpty(operation.QueryParametersModelTypeName))
        {
            dependencies.Add(operation.QueryParametersModelTypeName);
        }
        
        // Генерируем импорты моделей
        if (!string.IsNullOrEmpty(modelsPath) && dependencies.Count > 0)
        {
            foreach (var dependency in dependencies.OrderBy(d => d))
            {
                var importFileName = GetModelFileName(dependency);
                CodeFormatter.AppendLine(sb, 0, $"import type {{ {dependency} }} from '{modelsPath}/{importFileName}.model';");
            }
            CodeFormatter.AppendEmptyLine(sb);
        }
    }
    
    /// <summary>
    /// Рекурсивно извлекает имена типов из TypeInfo и добавляет их в dependencies
    /// </summary>
    private void ExtractTypeNamesFromTypeInfo(TypeInfo typeInfo, HashSet<string> dependencies)
    {
        if (typeInfo == null)
            return;
        
        // Если это не примитивный тип и не встроенный тип, добавляем базовый тип
        if (!typeInfo.IsPrimitive && !IsBuiltInTypeScriptType(typeInfo.BaseType))
        {
            // Проверяем, что BaseType не пустой и не является встроенным типом
            if (!string.IsNullOrEmpty(typeInfo.BaseType) && 
                !IsBuiltInTypeScriptType(typeInfo.BaseType) &&
                typeInfo.BaseType != "object" &&
                typeInfo.BaseType != "any" &&
                typeInfo.BaseType != "unknown")
            {
                dependencies.Add(typeInfo.BaseType);
            }
        }
        
        // Рекурсивно обрабатываем generic аргументы
        if (typeInfo.GenericArguments != null && typeInfo.GenericArguments.Count > 0)
        {
            foreach (var genericArg in typeInfo.GenericArguments)
            {
                ExtractTypeNamesFromTypeInfo(genericArg, dependencies);
            }
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
    /// Рендерит импорты методов клиента из axios файла
    /// </summary>
    private void RenderClientImports(StringBuilder sb, OperationTemplateData operation, string clientImportPath)
    {
        if (string.IsNullOrEmpty(clientImportPath))
            return;
        
        // Получаем имя метода клиента (camelCase от MethodName)
        var clientMethodName = _namingConverter.ToCamelCase(operation.MethodName);
        
        // Генерируем импорт метода клиента
        CodeFormatter.AppendLine(sb, 0, $"import {{ {clientMethodName} }} from '{clientImportPath}';");
        CodeFormatter.AppendEmptyLine(sb);
    }
    
    /// <summary>
    /// Извлекает путь к моделям из пути к клиенту
    /// Например, из '../endpoints/buildingOnProjectPlans.axios' получаем '../models'
    /// </summary>
    private string? ExtractModelsPathFromClientImportPath(string? clientImportPath)
    {
        if (string.IsNullOrEmpty(clientImportPath))
            return null;
        
        // Если путь содержит '/endpoints/', заменяем на '/models'
        if (clientImportPath.Contains("/endpoints/"))
        {
            return clientImportPath.Replace("/endpoints/", "/models/").Replace(".axios", "");
        }
        
        // Если путь относительный, пытаемся определить путь к моделям
        // Например, из './buildingOnProjectPlans.axios' получаем '../models'
        if (clientImportPath.StartsWith("./"))
        {
            return "../models";
        }
        
        // По умолчанию используем '../models'
        return "../models";
    }
    
    /// <summary>
    /// Рендерит экспорты для ResultType и AwaitedResultType
    /// </summary>
    private void RenderTypeExports(StringBuilder sb, OperationTemplateData operation, string clientMethodName)
    {
        // Используем имя функции клиента для генерации типов
        var functionNamePascal = _namingConverter.ToPascalCase(clientMethodName);
        
        // Генерируем имена типов на основе имени функции клиента
        var returnTypeName = $"{functionNamePascal}ReturnType";
        var awaitedReturnTypeName = $"{functionNamePascal}AwaitedReturnType";
        
        // Определяем тип ответа из ReturnTypeInfo
        var responseType = operation.ReturnTypeInfo?.FullTypeName ?? "any";
        
        // Экспортируем ResultType как Promise<Type> (как в старой версии)
        CodeFormatter.AppendLine(sb, 0, $"export type {returnTypeName} = Promise<{responseType}>;");
        CodeFormatter.AppendEmptyLine(sb);
        
        // Экспортируем AwaitedResultType на основе ResultType
        CodeFormatter.AppendLine(sb, 0, $"export type {awaitedReturnTypeName} = Awaited<{returnTypeName}>;");
        CodeFormatter.AppendEmptyLine(sb);
    }

    #endregion
}
