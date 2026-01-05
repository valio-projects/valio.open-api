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
        
        // Генерируем имена типов на основе имени функции клиента
        var functionNamePascal = _namingConverter.ToPascalCase(clientMethodName);
        var awaitedReturnTypeName = $"{functionNamePascal}AwaitedReturnType";
        
        // Собираем параметры функции
        var functionParams = BuildQueryHookParameters(operation, awaitedReturnTypeName, options);
        
        // Генерируем query key вызов
        var queryKeyParams = BuildQueryKeyParameters(operation);
        var queryKeyCall = queryKeyParams.Count > 0 
            ? $"{queryKeyFunctionName}({string.Join(", ", queryKeyParams)})"
            : $"{queryKeyFunctionName}()";
        
        // Генерируем query function с signal
        var queryFnParams = BuildQueryFnClientMethodParameters(operation, clientMethodName, options);
        
        CodeFormatter.AppendLine(sb, 0, $"export function {hookName}<TData = {awaitedReturnTypeName}, TError = {errorType}>(");
        CodeFormatter.AppendLine(sb, 1, functionParams);
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
        CodeFormatter.AppendLine(sb, 1, $"const queryKey = {queryKeyCall};");
        CodeFormatter.AppendLine(sb, 1, $"const queryFn: QueryFunction<{awaitedReturnTypeName}> = ({{ signal }}) => {{");
        CodeFormatter.AppendLine(sb, 2, $"return {clientMethodName}({queryFnParams});");
        CodeFormatter.AppendLine(sb, 1, "};");
        CodeFormatter.AppendEmptyLine(sb);
        CodeFormatter.AppendLine(sb, 1, $"return useQuery<{awaitedReturnTypeName}, TError, TData>({{");
        CodeFormatter.AppendLine(sb, 2, "queryKey,");
        CodeFormatter.AppendLine(sb, 2, "queryFn,");
        CodeFormatter.AppendLine(sb, 2, "...queryOptions");
        CodeFormatter.AppendLine(sb, 1, "});");
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
        var queryKeyParams = BuildQueryKeyParameters(operation);
        var queryKeyCall = queryKeyParams.Count > 0 
            ? $"{queryKeyFunctionName}({string.Join(", ", queryKeyParams)})"
            : $"{queryKeyFunctionName}()";
        
        var queryFnParams = BuildQueryFnClientMethodParameters(operation, clientMethodName, options);
        
        CodeFormatter.AppendLine(sb, 0, $"export const {helperName} = <TData = {awaitedReturnTypeName}, TError = {errorType}>(");
        CodeFormatter.AppendLine(sb, 1, functionParams);
        CodeFormatter.AppendLine(sb, 0, $") : UseQueryOptions<{awaitedReturnTypeName}, TError, TData> & {{ queryKey: QueryKey }} => {{");
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
        CodeFormatter.AppendLine(sb, 1, $"const queryKey = {queryKeyCall};");
        CodeFormatter.AppendLine(sb, 1, $"const queryFn: QueryFunction<{awaitedReturnTypeName}> = ({{ signal }}) => {{");
        CodeFormatter.AppendLine(sb, 2, $"return {clientMethodName}({queryFnParams});");
        CodeFormatter.AppendLine(sb, 1, "};");
        CodeFormatter.AppendEmptyLine(sb);
        CodeFormatter.AppendLine(sb, 1, "return {");
        CodeFormatter.AppendLine(sb, 2, "queryKey,");
        CodeFormatter.AppendLine(sb, 2, "queryFn,");
        CodeFormatter.AppendLine(sb, 2, "enabled: true,");
        CodeFormatter.AppendLine(sb, 2, "...queryOptions");
        CodeFormatter.AppendLine(sb, 1, "};");
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
        var queryKeyFunctionName = $"get{_namingConverter.ToPascalCase(functionName)}QueryKey";
        
        // Генерируем имена типов на основе имени функции клиента
        var functionNamePascal = _namingConverter.ToPascalCase(clientMethodName);
        var awaitedReturnTypeName = $"{functionNamePascal}AwaitedReturnType";
        
        var functionParams = BuildQueryHookParameters(operation, awaitedReturnTypeName, options);
        var queryKeyParamsStr = string.Join(", ", BuildQueryKeyParameters(operation));
        var infiniteQueryFnParams = BuildQueryFnClientMethodParameters(operation, clientMethodName, options);
        
        CodeFormatter.AppendLine(sb, 0, $"export function {infiniteHookName}<TData = InfiniteData<{awaitedReturnTypeName}, unknown>, TError = {errorType}>(");
        CodeFormatter.AppendLine(sb, 1, functionParams);
        CodeFormatter.AppendLine(sb, 0, $") : UseInfiniteQueryResult<TData, TError> {{");
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
        CodeFormatter.AppendLine(sb, 1, $"const queryKey = {queryKeyFunctionName}({queryKeyParamsStr});");
        CodeFormatter.AppendLine(sb, 1, $"const queryFn: QueryFunction<{awaitedReturnTypeName}, QueryKey, unknown> = ({{ signal }}) => {{");
        CodeFormatter.AppendLine(sb, 2, $"return {clientMethodName}({infiniteQueryFnParams});");
        CodeFormatter.AppendLine(sb, 1, "};");
        CodeFormatter.AppendEmptyLine(sb);
        CodeFormatter.AppendLine(sb, 1, $"return useInfiniteQuery<{awaitedReturnTypeName}, TError, TData, QueryKey, unknown>({{");
        CodeFormatter.AppendLine(sb, 2, "queryKey,");
        CodeFormatter.AppendLine(sb, 2, "queryFn,");
        CodeFormatter.AppendLine(sb, 2, "initialPageParam: 0,");
        CodeFormatter.AppendLine(sb, 2, "getNextPageParam: (lastPage) => lastPage.nextCursor,");
        CodeFormatter.AppendLine(sb, 2, "enabled: true,");
        CodeFormatter.AppendLine(sb, 2, "...queryOptions");
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
        
        // Генерируем имена типов на основе имени функции клиента
        var functionNamePascal = _namingConverter.ToPascalCase(clientMethodName);
        var awaitedReturnTypeName = $"{functionNamePascal}AwaitedReturnType";
        
        var functionParams = BuildQueryHookParameters(operation, awaitedReturnTypeName, options);
        var queryKeyParams = BuildQueryKeyParameters(operation);
        var queryKeyCall = queryKeyParams.Count > 0 
            ? $"{queryKeyFunctionName}({string.Join(", ", queryKeyParams)})"
            : $"{queryKeyFunctionName}()";
        
        var queryFnParams = BuildQueryFnClientMethodParameters(operation, clientMethodName, options);
        
        CodeFormatter.AppendLine(sb, 0, $"export const {prefetchFunctionName} = async <");
        CodeFormatter.AppendLine(sb, 1, $"  TData = {awaitedReturnTypeName},");
        CodeFormatter.AppendLine(sb, 1, "  TError = Error,");
        CodeFormatter.AppendLine(sb, 0, ">(");
        CodeFormatter.AppendLine(sb, 1, "  queryClient: QueryClient,");
        CodeFormatter.AppendLine(sb, 1, functionParams);
        CodeFormatter.AppendLine(sb, 0, $") : Promise<QueryClient> => {{");
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
        CodeFormatter.AppendLine(sb, 1, $"const queryKey = {queryKeyCall};");
        CodeFormatter.AppendLine(sb, 1, $"const queryFn: QueryFunction<{awaitedReturnTypeName}> = ({{ signal }}) => {{");
        CodeFormatter.AppendLine(sb, 2, $"return {clientMethodName}({queryFnParams});");
        CodeFormatter.AppendLine(sb, 1, "};");
        CodeFormatter.AppendEmptyLine(sb);
        CodeFormatter.AppendLine(sb, 1, $"await queryClient.prefetchQuery<{awaitedReturnTypeName}, TError>({{");
        CodeFormatter.AppendLine(sb, 2, "queryKey,");
        CodeFormatter.AppendLine(sb, 2, "queryFn");
        CodeFormatter.AppendLine(sb, 1, "});");
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
        var variablesTypeStr = BuildMutationVariablesType(operation);
        var mutationParams = BuildMutationHookParameters(operation, awaitedReturnTypeName, variablesTypeStr, options);
        
        // Формируем деструктуризацию variables
        var variablesDestructuring = BuildMutationVariablesDestructuring(operation);
        
        // Формируем аргументы для вызова клиентского метода
        var clientMethodParams = BuildClientMethodParameters(operation, clientMethodName, options);
        
        CodeFormatter.AppendLine(sb, 0, $"export const {helperName} = <TError = {errorType}, TContext = unknown>(");
        CodeFormatter.AppendLine(sb, 1, mutationParams);
        CodeFormatter.AppendLine(sb, 0, $") : UseMutationOptions<{awaitedReturnTypeName}, TError, {{\n{variablesTypeStr}\n  }}, TContext> => {{");
        CodeFormatter.AppendEmptyLine(sb);
        CodeFormatter.AppendLine(sb, 1, $"const mutationKey = [\"{clientMethodName}\"];");
        if (options.HttpClientLibrary == HttpClientLibrary.Axios)
        {
            CodeFormatter.AppendLine(sb, 1, "const { mutation: mutationOptions, timeout, axiosInstance } = options");
        }
        else
        {
            CodeFormatter.AppendLine(sb, 1, "const { mutation: mutationOptions, timeout } = options");
        }
        CodeFormatter.AppendLine(sb, 1, "  ? options.mutation &&");
        CodeFormatter.AppendLine(sb, 1, "    \"mutationKey\" in options.mutation &&");
        CodeFormatter.AppendLine(sb, 1, "    options.mutation.mutationKey");
        CodeFormatter.AppendLine(sb, 1, "    ? options");
        CodeFormatter.AppendLine(sb, 1, "    : { ...options, mutation: { ...options.mutation, mutationKey } }");
        if (options.HttpClientLibrary == HttpClientLibrary.Axios)
        {
            CodeFormatter.AppendLine(sb, 1, "  : { mutation: { mutationKey }, timeout: undefined, axiosInstance: undefined };");
        }
        else
        {
            CodeFormatter.AppendLine(sb, 1, "  : { mutation: { mutationKey }, timeout: undefined };");
        }
        CodeFormatter.AppendEmptyLine(sb);
        CodeFormatter.AppendLine(sb, 1, $"const mutationFn: MutationFunction<{awaitedReturnTypeName}, {{");
        CodeFormatter.AppendLine(sb, 1, variablesTypeStr);
        CodeFormatter.AppendLine(sb, 1, "}> = async (props) => {");
        CodeFormatter.AppendLine(sb, 2, $"const {{ {variablesDestructuring} }} = props ?? {{}};");
        CodeFormatter.AppendEmptyLine(sb);
        // Собираем аргументы для вызова клиентского метода
        // Порядок должен соответствовать порядку параметров в axios методах: axiosInstance, signal, timeout
        var methodArgs = new List<string>();
        if (!string.IsNullOrEmpty(clientMethodParams))
        {
            methodArgs.Add(clientMethodParams);
        }
        if (options.HttpClientLibrary == HttpClientLibrary.Axios)
        {
            methodArgs.Add("axiosInstance");
        }
        methodArgs.Add("undefined"); // signal для mutations обычно не используется
        methodArgs.Add("timeout");
        var methodArgsStr = string.Join(", ", methodArgs);
        CodeFormatter.AppendLine(sb, 2, $"return {clientMethodName}({methodArgsStr});");
        CodeFormatter.AppendLine(sb, 1, "};");
        CodeFormatter.AppendEmptyLine(sb);
        CodeFormatter.AppendLine(sb, 1, "return { mutationFn, ...mutationOptions };");
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
        var variablesTypeStr = BuildMutationVariablesType(operation);
        var mutationParams = BuildMutationHookParameters(operation, awaitedReturnTypeName, variablesTypeStr, options);
        
        CodeFormatter.AppendLine(sb, 0, $"export function {hookName}<TError = {errorType}, TContext = unknown>(");
        CodeFormatter.AppendLine(sb, 1, mutationParams);
        CodeFormatter.AppendLine(sb, 0, $") : UseMutationResult<{awaitedReturnTypeName}, TError, {{\n{variablesTypeStr}\n  }}, TContext> {{");
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
    private string BuildQueryHookParameters(OperationTemplateData operation, string awaitedReturnTypeName, TypeScriptGenerationOptions options)
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
        var errorType = "Error";
        var optionsLines = new List<string> { $"    query?: UseQueryOptions<{awaitedReturnTypeName}, {errorType}, TData>;" };
        optionsLines.Add("    timeout?: number;");
        if (options.HttpClientLibrary == HttpClientLibrary.Axios)
        {
            optionsLines.Add("    axiosInstance?: AxiosInstance;");
        }
        var optionsStr = string.Join("\n", optionsLines);
        parameters.Add($"options?: {{\n{optionsStr}\n  }}");
        
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
    /// Строит параметры для вызова метода клиента (базовый набор + signal/timeout/axiosInstance для mutations)
    /// </summary>
    private string BuildClientMethodParameters(OperationTemplateData operation, string clientMethodName, TypeScriptGenerationOptions options)
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
    /// Строит параметры для вызова метода клиента в queryFn (включает signal, timeout, axiosInstance если нужно)
    /// </summary>
    private string BuildQueryFnClientMethodParameters(OperationTemplateData operation, string clientMethodName, TypeScriptGenerationOptions options)
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
    /// </summary>
    private string BuildMutationVariablesType(OperationTemplateData operation)
    {
        var variables = new List<string>();
        
        // Path параметры
        foreach (var param in operation.PathParameters)
        {
            variables.Add($"  {param.VariableName}: {param.TypeInfo.FullTypeName};");
        }
        
        // Query параметры
        if (operation.UseQueryParametersModel && !string.IsNullOrEmpty(operation.QueryParametersModelTypeName))
        {
            var hasRequiredParam = operation.QueryParameters.Any(p => p.IsRequired);
            var optional = hasRequiredParam ? "" : "?";
            variables.Add($"  params{optional}: {operation.QueryParametersModelTypeName};");
        }
        else
        {
            foreach (var param in operation.QueryParameters)
            {
                var optional = param.IsRequired ? "" : "?";
                variables.Add($"  {param.VariableName}{optional}: {param.TypeInfo.FullTypeName};");
            }
        }
        
        // Header параметры
        foreach (var param in operation.HeaderParameters)
        {
            var optional = param.IsRequired ? "" : "?";
            variables.Add($"  {param.VariableName}{optional}: {param.TypeInfo.FullTypeName};");
        }
        
        // Request body
        if (operation.HasRequestBody && !string.IsNullOrEmpty(operation.RequestBodyParameter))
        {
            var optional = operation.RequestBodyTypeInfo != null && !operation.RequestBodyTypeInfo.IsNullable ? "" : "?";
            var typeStr = operation.RequestBodyTypeInfo?.FullTypeName ?? "any";
            variables.Add($"  {operation.RequestBodyParameter}{optional}: {typeStr};");
        }
        else if (operation.IsFormData && operation.FormDataProperties != null && operation.FormDataProperties.Count > 0)
        {
            foreach (var formProp in operation.FormDataProperties)
            {
                var optional = formProp.IsRequired ? "" : "?";
                variables.Add($"  {formProp.VariableName}{optional}: {formProp.TypeInfo.FullTypeName};");
            }
        }
        
        return variables.Count > 0 ? string.Join("\n", variables) : "";
    }
    
    /// <summary>
    /// Строит строку деструктуризации variables для mutationFn
    /// </summary>
    private string BuildMutationVariablesDestructuring(OperationTemplateData operation)
    {
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
        if (operation.HasRequestBody && !string.IsNullOrEmpty(operation.RequestBodyParameter))
        {
            properties.Add(operation.RequestBodyParameter);
        }
        else if (operation.IsFormData && operation.FormDataProperties != null && operation.FormDataProperties.Count > 0)
        {
            foreach (var formProp in operation.FormDataProperties)
            {
                properties.Add(formProp.VariableName);
            }
        }
        
        return properties.Count > 0 ? string.Join(", ", properties) : "";
    }
    
    /// <summary>
    /// Строит параметры для mutation hook (только options и queryClient)
    /// </summary>
    private string BuildMutationHookParameters(OperationTemplateData operation, string awaitedReturnTypeName, string variablesTypeStr, TypeScriptGenerationOptions options)
    {
        var errorType = "Error";
        var optionsLines = new List<string> { $"    mutation?: UseMutationOptions<{awaitedReturnTypeName}, {errorType}, {{\n{variablesTypeStr}\n    }}, unknown>;" };
        optionsLines.Add("    timeout?: number;");
        if (options.HttpClientLibrary == HttpClientLibrary.Axios)
        {
            optionsLines.Add("    axiosInstance?: AxiosInstance;");
        }
        var optionsStr = string.Join("\n", optionsLines);
        var parameters = new List<string>
        {
            $"options?: {{\n{optionsStr}\n  }}",
            "queryClient?: QueryClient"
        };
        
        return string.Join(",\n  ", parameters);
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
        CodeFormatter.AppendLine(sb, 1, "useQuery,");
        CodeFormatter.AppendLine(sb, 1, "useInfiniteQuery,");
        CodeFormatter.AppendLine(sb, 1, "useQueryClient,");
        CodeFormatter.AppendLine(sb, 1, "type UseQueryOptions,");
        CodeFormatter.AppendLine(sb, 1, "type UseQueryResult,");
        CodeFormatter.AppendLine(sb, 1, "type QueryFunction,");
        CodeFormatter.AppendLine(sb, 1, "type QueryKey,");
        CodeFormatter.AppendLine(sb, 1, "type InfiniteData,");
        CodeFormatter.AppendLine(sb, 1, "type UseInfiniteQueryResult,");
        CodeFormatter.AppendLine(sb, 1, "type QueryClient,");
        CodeFormatter.AppendLine(sb, 0, "} from '@tanstack/react-query';");
        CodeFormatter.AppendEmptyLine(sb);
        return sb.ToString();
    }

    /// <summary>
    /// Рендерит импорты TanStack Query типов для mutations
    /// </summary>
    /// <returns>Строка с импортами для mutation операций</returns>
    public string RenderMutationImports()
    {
        var sb = new StringBuilder();
        CodeFormatter.AppendLine(sb, 0, "import {");
        CodeFormatter.AppendLine(sb, 1, "useMutation,");
        CodeFormatter.AppendLine(sb, 1, "type UseMutationOptions,");
        CodeFormatter.AppendLine(sb, 1, "type UseMutationResult,");
        CodeFormatter.AppendLine(sb, 1, "type MutationFunction,");
        CodeFormatter.AppendLine(sb, 0, "} from '@tanstack/react-query';");
        CodeFormatter.AppendEmptyLine(sb);
        return sb.ToString();
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
        var functionName = _namingConverter.ToCamelCase(clientMethodName);
        var functionNamePascal = _namingConverter.ToPascalCase(clientMethodName);
        
        // Генерируем имена типов на основе имени функции клиента
        var returnTypeName = $"{functionNamePascal}ReturnType";
        var awaitedReturnTypeName = $"{functionNamePascal}AwaitedReturnType";
        
        // Экспортируем ResultType на основе функции клиента
        CodeFormatter.AppendLine(sb, 0, $"export type {returnTypeName} = ReturnType<typeof {functionName}>;");
        CodeFormatter.AppendEmptyLine(sb);
        
        // Экспортируем AwaitedResultType на основе ResultType
        CodeFormatter.AppendLine(sb, 0, $"export type {awaitedReturnTypeName} = Awaited<{returnTypeName}>;");
        CodeFormatter.AppendEmptyLine(sb);
    }

    #endregion
}
