using System.Text;
using ValIO.Core.Interfaces;
using ValIO.Core.Models;
using ValIO.Core.Models.TemplateData;
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
        RenderComments(sb, operation);
        
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
        
        return sb.ToString();
    }

    public string RenderMutationHook(OperationTemplateData operation, string? clientMethodName = null, string? clientImportPath = null)
    {
        if (operation == null)
            throw new ArgumentNullException(nameof(operation));

        var sb = new StringBuilder();
        
        // Генерируем имя хука и функции
        var operationId = operation.MethodName;
        var hookName = GenerateHookName(operationId, isMutation: true);
        var functionName = _namingConverter.ToCamelCase(operationId);
        
        // Получаем опции из OperationTemplateData
        var options = GetTypeScriptOptions(operation);
        
        // Имя метода клиента
        var actualClientMethodName = clientMethodName ?? functionName;
        
        // Генерируем комментарии
        RenderComments(sb, operation);
        
        // Генерируем функцию клиента (если clientMethodName не указан)
        if (string.IsNullOrEmpty(clientMethodName))
        {
            RenderClientFunction(sb, operation, functionName, options);
        }
        
        // Генерируем mutation options helper
        RenderMutationOptionsHelper(sb, operation, functionName, actualClientMethodName, options);
        
        // Генерируем mutation hook
        RenderMutationHookFunction(sb, operation, hookName, functionName, actualClientMethodName, options);
        
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
        // Извлекаем опции из OperationTemplateData.Options
        var options = new TypeScriptGenerationOptions();
        
        if (operation.Options.TryGetValue("HttpClientLibrary", out var httpClientLib))
        {
            if (httpClientLib is HttpClientLibrary lib)
            {
                options.HttpClientLibrary = lib;
            }
        }
        
        if (operation.Options.TryGetValue("Axios", out var axios))
        {
            if (axios is AxiosOptions axiosOpts)
            {
                options.Axios = axiosOpts;
            }
        }
        
        return options;
    }

    private void RenderComments(StringBuilder sb, OperationTemplateData operation)
    {
        if (!string.IsNullOrEmpty(operation.Summary))
        {
            sb.AppendLine("/**");
            sb.AppendLine($" * {operation.Summary}");
            sb.AppendLine(" */");
        }
        
        if (!string.IsNullOrEmpty(operation.Description))
        {
            sb.AppendLine("/**");
            sb.AppendLine($" * {operation.Description}");
            sb.AppendLine(" */");
        }
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
            keyParams.Add($"{param.VariableName}: {FormatType(param.TypeInfo)}");
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
                keyParams.Add($"{param.VariableName}{optional}: {FormatType(param.TypeInfo)}");
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
        
        sb.AppendLine($"export const {queryKeyFunctionName} = ({functionParams}): QueryKey => [");
        sb.AppendLine($"  {keyArray}");
        sb.AppendLine("];");
        sb.AppendLine();
    }

    /// <summary>
    /// Рендерит query hook функцию
    /// </summary>
    private void RenderQueryHookFunction(StringBuilder sb, OperationTemplateData operation, string hookName, string queryKeyFunctionName, string clientMethodName, TypeScriptGenerationOptions options)
    {
        // Определяем тип ответа
        var responseType = GetResponseType(operation.ReturnTypeInfo);
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
        
        sb.AppendLine($"export function {hookName}<TData = {responseType}, TError = {errorType}>(");
        sb.AppendLine($"  {functionParams}");
        sb.AppendLine($") : UseQueryResult<TData, TError> {{");
        sb.AppendLine();
        sb.AppendLine($"  const queryKey = {queryKeyCall};");
        sb.AppendLine($"  const queryFn: QueryFunction<TData> = () => {clientMethodName}({queryFnParams});");
        sb.AppendLine();
        sb.AppendLine($"  return useQuery<TData, TError>({{");
        sb.AppendLine($"    queryKey,");
        sb.AppendLine($"    queryFn,");
        sb.AppendLine($"    ...options");
        sb.AppendLine($"  }});");
        sb.AppendLine("}");
        sb.AppendLine();
    }

    /// <summary>
    /// Рендерит query options helper функцию
    /// </summary>
    private void RenderQueryOptionsHelper(StringBuilder sb, OperationTemplateData operation, string functionName, string queryKeyFunctionName, string clientMethodName, TypeScriptGenerationOptions options)
    {
        var responseType = GetResponseType(operation.ReturnTypeInfo);
        var errorType = "Error";
        var helperName = $"get{_namingConverter.ToPascalCase(functionName)}QueryOptions";
        
        var functionParams = BuildQueryHookParameters(operation);
        var queryKeyParams = BuildQueryKeyParameters(operation);
        var queryKeyCall = queryKeyParams.Count > 0 
            ? $"{queryKeyFunctionName}({string.Join(", ", queryKeyParams)})"
            : $"{queryKeyFunctionName}()";
        
        var queryFnParams = BuildClientMethodParameters(operation, clientMethodName);
        
        sb.AppendLine($"export const {helperName} = <TData = {responseType}, TError = {errorType}>(");
        sb.AppendLine($"  {functionParams}");
        sb.AppendLine($") : UseQueryOptions<TData, TError> => {{");
        sb.AppendLine();
        sb.AppendLine($"  const queryKey = {queryKeyCall};");
        sb.AppendLine($"  const queryFn: QueryFunction<TData> = () => {clientMethodName}({queryFnParams});");
        sb.AppendLine();
        sb.AppendLine($"  return {{");
        sb.AppendLine($"    queryKey,");
        sb.AppendLine($"    queryFn,");
        sb.AppendLine($"    ...options");
        sb.AppendLine($"  }};");
        sb.AppendLine("};");
        sb.AppendLine();
    }

    /// <summary>
    /// Рендерит infinite query support
    /// </summary>
    private void RenderInfiniteQuerySupport(StringBuilder sb, OperationTemplateData operation, string functionName, string clientMethodName, TypeScriptGenerationOptions options)
    {
        var responseType = GetResponseType(operation.ReturnTypeInfo);
        var errorType = "Error";
        var infiniteHookName = $"use{_namingConverter.ToPascalCase(functionName)}Infinite";
        var queryKeyFunctionName = $"get{_namingConverter.ToPascalCase(functionName)}QueryKey";
        
        var functionParams = BuildQueryHookParameters(operation);
        
        sb.AppendLine($"export function {infiniteHookName}<TData = InfiniteData<{responseType}>, TError = {errorType}>(");
        sb.AppendLine($"  {functionParams}");
        sb.AppendLine($") : UseInfiniteQueryResult<TData, TError> {{");
        sb.AppendLine();
        sb.AppendLine($"  const queryKey = {queryKeyFunctionName}({string.Join(", ", BuildQueryKeyParameters(operation))});");
        sb.AppendLine($"  const queryFn: QueryFunction<TData> = ({BuildInfiniteQueryFnParams(operation, clientMethodName)}) => {clientMethodName}({BuildClientMethodParameters(operation, clientMethodName)});");
        sb.AppendLine();
        sb.AppendLine($"  return useInfiniteQuery<TData, TError>({{");
        sb.AppendLine($"    queryKey,");
        sb.AppendLine($"    queryFn,");
        sb.AppendLine($"    ...options");
        sb.AppendLine($"  }});");
        sb.AppendLine("}");
        sb.AppendLine();
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
        
        sb.AppendLine($"export const {prefetchFunctionName} = async (");
        sb.AppendLine($"  queryClient: QueryClient,");
        sb.AppendLine($"  {functionParams}");
        sb.AppendLine($") : Promise<void> => {{");
        sb.AppendLine();
        sb.AppendLine($"  const queryKey = {queryKeyCall};");
        sb.AppendLine($"  const queryFn: QueryFunction = () => {clientMethodName}({queryFnParams});");
        sb.AppendLine();
        sb.AppendLine($"  await queryClient.prefetchQuery({{");
        sb.AppendLine($"    queryKey,");
        sb.AppendLine($"    queryFn");
        sb.AppendLine($"  }});");
        sb.AppendLine("};");
        sb.AppendLine();
    }

    /// <summary>
    /// Рендерит mutation options helper
    /// </summary>
    private void RenderMutationOptionsHelper(StringBuilder sb, OperationTemplateData operation, string functionName, string clientMethodName, TypeScriptGenerationOptions options)
    {
        var responseType = GetResponseType(operation.ReturnTypeInfo);
        var errorType = "Error";
        var helperName = $"get{_namingConverter.ToPascalCase(functionName)}MutationOptions";
        
        var mutationParams = BuildMutationHookParameters(operation);
        
        sb.AppendLine($"export const {helperName} = <TError = {errorType}, TContext = unknown>(");
        sb.AppendLine($"  {mutationParams}");
        sb.AppendLine($") : UseMutationOptions<{responseType}, TError, void, TContext> => {{");
        sb.AppendLine();
        sb.AppendLine($"  const mutationFn: MutationFunction<{responseType}, void> = () => {{");
        sb.AppendLine($"    return {clientMethodName}({BuildClientMethodParameters(operation, clientMethodName)});");
        sb.AppendLine($"  }};");
        sb.AppendLine();
        sb.AppendLine($"  return {{");
        sb.AppendLine($"    mutationFn,");
        sb.AppendLine($"    ...options");
        sb.AppendLine($"  }};");
        sb.AppendLine("};");
        sb.AppendLine();
    }

    /// <summary>
    /// Рендерит mutation hook функцию
    /// </summary>
    private void RenderMutationHookFunction(StringBuilder sb, OperationTemplateData operation, string hookName, string functionName, string clientMethodName, TypeScriptGenerationOptions options)
    {
        var responseType = GetResponseType(operation.ReturnTypeInfo);
        var errorType = "Error";
        
        var mutationParams = BuildMutationHookParameters(operation);
        
        sb.AppendLine($"export function {hookName}<TError = {errorType}, TContext = unknown>(");
        sb.AppendLine($"  {mutationParams}");
        sb.AppendLine($") : UseMutationResult<{responseType}, TError, void, TContext> {{");
        sb.AppendLine();
        sb.AppendLine($"  const mutationFn: MutationFunction<{responseType}, void> = () => {{");
        sb.AppendLine($"    return {clientMethodName}({BuildClientMethodParameters(operation, clientMethodName)});");
        sb.AppendLine($"  }};");
        sb.AppendLine();
        sb.AppendLine($"  return useMutation<{responseType}, TError, void, TContext>({{");
        sb.AppendLine($"    mutationFn,");
        sb.AppendLine($"    ...options");
        sb.AppendLine($"  }});");
        sb.AppendLine("}");
        sb.AppendLine();
    }

    #region Private Helper Methods

    /// <summary>
    /// Форматирует тип из TypeInfo для TypeScript
    /// </summary>
    private string FormatType(TypeInfo typeInfo)
    {
        var typeName = typeInfo.FullTypeName;
        // Убираем "?" из типа, если он там есть - мы добавим его к имени параметра
        if (typeName.EndsWith("?"))
        {
            typeName = typeName.Substring(0, typeName.Length - 1).Trim();
        }
        return typeName;
    }

    /// <summary>
    /// Получает тип ответа из ReturnTypeInfo
    /// </summary>
    private string GetResponseType(TypeInfo returnTypeInfo)
    {
        if (returnTypeInfo.BaseType == "Task" && returnTypeInfo.GenericArguments.Count > 0)
        {
            return returnTypeInfo.GenericArguments[0].FullTypeName;
        }
        return returnTypeInfo.IsVoid ? "void" : returnTypeInfo.FullTypeName;
    }

    /// <summary>
    /// Строит параметры для query hook
    /// </summary>
    private string BuildQueryHookParameters(OperationTemplateData operation)
    {
        var parameters = new List<string>();
        
        // Path параметры
        foreach (var param in operation.PathParameters)
        {
            parameters.Add($"{param.VariableName}: {FormatType(param.TypeInfo)}");
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
                parameters.Add($"{param.VariableName}{optional}: {FormatType(param.TypeInfo)}");
            }
        }
        
        // Query options
        var responseType = GetResponseType(operation.ReturnTypeInfo);
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
            parameters.Add($"{param.VariableName}: {FormatType(param.TypeInfo)}");
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
                parameters.Add($"{param.VariableName}{optional}: {FormatType(param.TypeInfo)}");
            }
        }
        
        // Header параметры
        foreach (var param in operation.HeaderParameters)
        {
            var optional = param.IsRequired ? "" : "?";
            parameters.Add($"{param.VariableName}{optional}: {FormatType(param.TypeInfo)}");
        }
        
        // Request body
        if (operation.HasRequestBody && !string.IsNullOrEmpty(operation.RequestBodyParameter))
        {
            var optional = operation.RequestBodyTypeInfo != null && !operation.RequestBodyTypeInfo.IsNullable ? "" : "?";
            var typeStr = operation.RequestBodyTypeInfo != null ? FormatType(operation.RequestBodyTypeInfo) : "any";
            parameters.Add($"{operation.RequestBodyParameter}{optional}: {typeStr}");
        }
        else if (operation.IsFormData && operation.FormDataProperties != null && operation.FormDataProperties.Count > 0)
        {
            foreach (var formProp in operation.FormDataProperties)
            {
                var optional = formProp.IsRequired ? "" : "?";
                parameters.Add($"{formProp.VariableName}{optional}: {FormatType(formProp.TypeInfo)}");
            }
        }
        
        // Mutation options
        var responseType = GetResponseType(operation.ReturnTypeInfo);
        var errorType = "Error";
        parameters.Add($"options?: UseMutationOptions<{responseType}, {errorType}, void, unknown>");
        
        return string.Join(",\n  ", parameters);
    }

    #endregion
}
