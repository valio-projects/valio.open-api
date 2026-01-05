using System.Text;
using ValIO.Core.Interfaces;
using ValIO.Core.Models;
using ValIO.Core.Models.TemplateData;
using ValIO.Core.Utilities;
using ValIO.Generators.TypeScript.Models;

namespace ValIO.Generators.TypeScript;

/// <summary>
/// Рендерер для генерации TypeScript кода через StringBuilders
/// </summary>
public class TypeScriptCodeRenderer : ICodeRenderer
{
    private readonly INamingConverter _namingConverter;

    public TypeScriptCodeRenderer(INamingConverter? namingConverter = null)
    {
        _namingConverter = namingConverter ?? new Core.Services.NamingConverter();
    }

    public GenerationLanguage Language => GenerationLanguage.TypeScript;

    /// <summary>
    /// Рендерит модель (interface/type)
    /// </summary>
    public string RenderModel(IModelTemplateData data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        var sb = new StringBuilder();
        
        // Получаем типизированные Options через приведение через object
        TypeScriptModelTemplateDataOptions typedOptions;
        try
        {
            var typedData = (ModelTemplateData<TypeScriptModelTemplateDataOptions, TypeScriptPropertyTemplateDataOptions>)(object)data;
            typedOptions = typedData.Options;
        }
        catch
        {
            typedOptions = new TypeScriptModelTemplateDataOptions();
        }
        
        // Извлекаем TypeScript специфичные опции из типизированных Options
        var validationLibrary = typedOptions.ValidationLibrary;
        var useZod = validationLibrary == ValidationLibrary.Zod;
        
        // Если используется Zod, генерируем через z.infer
        if (useZod)
        {
            var schemaName = $"{data.Name}Schema";
            var schemaFileName = GetSchemaFileName(data.Name);
            
            sb.AppendLine($"import {{ {schemaName} }} from './{schemaFileName}.schema';");
            sb.AppendLine("import { z } from 'zod';");
            sb.AppendLine();
            
            CommentGenerator.GenerateJSDocComment(sb, 0, data.Description);
            
            sb.AppendLine($"export type {data.Name} = z.infer<typeof {schemaName}>;");
            return sb.ToString();
        }
        
        // Стандартная генерация интерфейсов
        // Генерируем импорты
        if (data.Dependencies.Count > 0 && !string.IsNullOrEmpty(data.ImportPath))
        {
            foreach (var dependency in data.Dependencies.Distinct().OrderBy(d => d))
            {
                var importFileName = GetModelFileName(dependency);
                sb.AppendLine($"import type {{ {dependency} }} from '{data.ImportPath}/{importFileName}.model';");
            }
            sb.AppendLine();
        }
        
        // Генерируем JSDoc комментарий
        CommentGenerator.GenerateJSDocComment(sb, 0, data.Description);
        
        // Определяем формат модели
        var useTypeAlias = !string.IsNullOrEmpty(data.BaseClassName) && data.OwnProperties.Count == 0;
        var useIntersectionType = !string.IsNullOrEmpty(data.BaseClassName) && data.OwnProperties.Count > 0;
        
        if (useTypeAlias)
        {
            // Просто type alias
            sb.AppendLine($"export type {data.Name} = {data.BaseClassName};");
        }
        else if (useIntersectionType)
        {
            // Intersection type
            sb.AppendLine($"export type {data.Name} = {data.BaseClassName} & {{");
            foreach (var property in data.OwnProperties)
            {
                var baseProperty = property as PropertyTemplateData ?? (PropertyTemplateData)(object)property;
                GenerateProperty(sb, baseProperty, indentLevel: 1);
            }
            CodeFormatter.AppendLine(sb, 0, "};");
        }
        else
        {
            // Обычный interface
            sb.AppendLine($"export interface {data.Name} {{");
            foreach (var property in data.Properties)
            {
                GenerateProperty(sb, property, indentLevel: 1);
            }
            CodeFormatter.AppendLine(sb, 0, "}");
        }
        
        return sb.ToString();
    }

    /// <summary>
    /// Рендерит enum (enum/union type)
    /// </summary>
    public string RenderEnum(EnumTemplateData data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        var sb = new StringBuilder();
        
        // Получаем типизированные Options через приведение через object
        var typedData = (EnumTemplateData<TypeScriptEnumTemplateDataOptions>)(object)data;
        var typedOptions = typedData.Options;
        
        // Извлекаем TypeScript специфичные опции из типизированных Options
        var validationLibrary = typedOptions.ValidationLibrary;
        var useZod = validationLibrary == ValidationLibrary.Zod;
        
        // Если используется Zod, генерируем через z.infer
        if (useZod)
        {
            var schemaName = $"{data.Name}Schema";
            var schemaFileName = GetSchemaFileName(data.Name);
            
            sb.AppendLine($"import {{ {schemaName} }} from './{schemaFileName}.schema';");
            sb.AppendLine("import { z } from 'zod';");
            sb.AppendLine();
            
            CommentGenerator.GenerateJSDocComment(sb, 0, data.Description);
            
            sb.AppendLine($"export type {data.Name} = z.infer<typeof {schemaName}>;");
            return sb.ToString();
        }
        
        // Стандартная генерация enum'ов
        CommentGenerator.GenerateJSDocComment(sb, 0, data.Description);
        
        if (data.Format == EnumFormat.Enum)
        {
            // TypeScript enum
            sb.AppendLine($"export enum {data.Name} {{");
            for (int i = 0; i < data.Values.Count; i++)
            {
                var value = data.Values[i];
                
                CommentGenerator.GenerateJSDocComment(sb, 1, value.Description);
                
                var comma = i < data.Values.Count - 1 ? "," : "";
                CodeFormatter.AppendLine(sb, 1, $"{value.Name} = {value.ValueString}{comma}");
            }
            sb.AppendLine("}");
        }
        else
        {
            // Union type
            sb.Append($"export type {data.Name} = ");
            for (int i = 0; i < data.Values.Count; i++)
            {
                var value = data.Values[i];
                if (i > 0)
                {
                    sb.Append(" | ");
                }
                sb.Append(value.ValueString);
            }
            sb.AppendLine(";");
        }
        
        return sb.ToString();
    }

    /// <summary>
    /// Рендерит клиент (HTTP клиент - Fetch или Axios)
    /// </summary>
    public string RenderClient(IClientTemplateData data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        var sb = new StringBuilder();
        
        // Определяем тип HTTP клиента
        var httpClientLibrary = data.HttpClientLibrary;
        var isAxios = httpClientLibrary == HttpClientLibrary.Axios;
        
        if (isAxios)
        {
            RenderAxiosClient(sb, data);
        }
        else
        {
            RenderFetchClient(sb, data);
        }
        
        return sb.ToString();
    }

    /// <summary>
    /// Рендерит операцию (метод HTTP клиента)
    /// </summary>
    public string RenderOperation(IOperationTemplateData data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        var sb = new StringBuilder();
        
        // Получаем типизированные Options через приведение через object
        var typedData = (OperationTemplateData<TypeScriptOperationTemplateDataOptions>)(object)data;
        var typedOptions = typedData.Options;
        
        // Определяем тип HTTP клиента из ClientTemplateData (операция не знает о типе клиента напрямую)
        // Для операций тип клиента передается через параметры метода или определяется в контексте клиента
        // Здесь используем Fetch по умолчанию, так как операции рендерятся в контексте клиента
        var isAxios = false; // Будет определено в контексте клиента
        
        if (isAxios)
        {
            RenderAxiosOperation(sb, data);
        }
        else
        {
            RenderFetchOperation(sb, data);
        }
        
        return sb.ToString();
    }

    /// <summary>
    /// Рендерит import директивы
    /// </summary>
    public string RenderImports(IReadOnlyList<string> imports, string? basePath = null)
    {
        if (imports == null || imports.Count == 0)
            return string.Empty;

        var sb = new StringBuilder();
        foreach (var import in imports.Distinct().OrderBy(i => i))
        {
            if (!string.IsNullOrEmpty(basePath))
            {
                sb.AppendLine($"import {{ {import} }} from '{basePath}/{import}';");
            }
            else
            {
                sb.AppendLine($"import {{ {import} }} from './{import}';");
            }
        }
        return sb.ToString();
    }

    #region Private Helper Methods

    private void RenderFetchClient(StringBuilder sb, IClientTemplateData data)
    {
        // Генерируем импорты моделей
        if (data.Dependencies.Count > 0 && !string.IsNullOrEmpty(data.ModelsPath))
        {
            foreach (var dependency in data.Dependencies.Distinct().OrderBy(d => d))
            {
                var importFileName = GetModelFileName(dependency);
                sb.AppendLine($"import type {{ {dependency} }} from '{data.ModelsPath}/{importFileName}.model';");
            }
            sb.AppendLine();
        }
        
        // Генерируем импорты query parameters моделей
        var queryParamsModelImports = new HashSet<(string TypeName, string FileName)>();
        foreach (var operation in data.Operations)
        {
            if (operation.UseQueryParametersModel && 
                !string.IsNullOrEmpty(operation.QueryParametersModelTypeName) && 
                !string.IsNullOrEmpty(operation.QueryParametersModelImportPath) &&
                !string.IsNullOrEmpty(data.ModelsPath))
            {
                var fileName = GetModelFileName(operation.QueryParametersModelTypeName);
                queryParamsModelImports.Add((operation.QueryParametersModelTypeName, fileName));
            }
        }
        
        if (queryParamsModelImports.Count > 0 && !string.IsNullOrEmpty(data.ModelsPath))
        {
            foreach (var modelImport in queryParamsModelImports.OrderBy(x => x.TypeName))
            {
                sb.AppendLine($"import type {{ {modelImport.TypeName} }} from '{data.ModelsPath}/{modelImport.FileName}.model';");
            }
            sb.AppendLine();
        }
        
        // Генерируем операции
        foreach (var operationBase in data.Operations)
        {
            sb.AppendLine();
            RenderFetchOperation(sb, operationBase);
        }
    }

    private void RenderAxiosClient(StringBuilder sb, IClientTemplateData data)
    {
        // Генерируем импорты Axios
        sb.AppendLine("import { AxiosInstance, AxiosRequestConfig } from 'axios';");
        
        // Получаем типизированные Options через приведение через object
        var typedData = (ClientTemplateData<TypeScriptClientTemplateDataOptions, TypeScriptOperationTemplateDataOptions>)(object)data;
        var options = typedData.Options;
        var serializeWithSchemas = options.SerializeWithSchemas;
        var deserializeWithSchemas = options.DeserializeWithSchemas;
        
        // Проверяем наличие кастомного экземпляра из типизированных Options
        var customInstancePath = options.AxiosOptions?.CustomInstancePath;
        var customInstanceName = options.AxiosOptions?.CustomInstanceName;
        var hasCustomInstance = !string.IsNullOrEmpty(customInstancePath) && !string.IsNullOrEmpty(customInstanceName);
        
        if (hasCustomInstance)
        {
            sb.AppendLine($"import {{ {customInstanceName} }} from '{customInstancePath}';");
        }
        
        // Генерируем импорты моделей
        if (data.Dependencies.Count > 0 && !string.IsNullOrEmpty(data.ModelsPath))
        {
            foreach (var dependency in data.Dependencies.Distinct().OrderBy(d => d))
            {
                var importFileName = GetModelFileName(dependency);
                sb.AppendLine($"import type {{ {dependency} }} from '{data.ModelsPath}/{importFileName}.model';");
            }
            sb.AppendLine();
        }
        
        // Генерируем импорты схем (Zod) - только если используются
        var schemaImports = new HashSet<(string SchemaName, string SchemaFileName)>();
        var queryParamsModelImports = new HashSet<(string TypeName, string FileName)>();
        
        foreach (var operation in data.Operations)
        {
            // Импортируем RequestSchema только если serializeWithSchemas = true
            if (serializeWithSchemas && 
                !string.IsNullOrEmpty(operation.RequestSchemaName) && 
                !string.IsNullOrEmpty(operation.RequestSchemaImportPath) &&
                !string.IsNullOrEmpty(data.ModelsPath))
            {
                var fileName = GetSchemaFileName(operation.RequestSchemaName);
                schemaImports.Add((operation.RequestSchemaName, fileName));
            }
            // Импортируем ResponseSchema только если deserializeWithSchemas = true
            if (deserializeWithSchemas && 
                !string.IsNullOrEmpty(operation.ResponseSchemaName) && 
                !string.IsNullOrEmpty(operation.ResponseSchemaImportPath) &&
                !string.IsNullOrEmpty(data.ModelsPath))
            {
                var fileName = GetSchemaFileName(operation.ResponseSchemaName);
                schemaImports.Add((operation.ResponseSchemaName, fileName));
            }
            if (operation.UseQueryParametersModel && 
                !string.IsNullOrEmpty(operation.QueryParametersModelTypeName) && 
                !string.IsNullOrEmpty(operation.QueryParametersModelImportPath) &&
                !string.IsNullOrEmpty(data.ModelsPath))
            {
                var fileName = GetModelFileName(operation.QueryParametersModelTypeName);
                queryParamsModelImports.Add((operation.QueryParametersModelTypeName, fileName));
            }
        }
        
        if (schemaImports.Count > 0 && !string.IsNullOrEmpty(data.ModelsPath))
        {
            foreach (var schemaImport in schemaImports.OrderBy(x => x.SchemaName))
            {
                sb.AppendLine($"import {{ {schemaImport.SchemaName} }} from '{data.ModelsPath}/{schemaImport.SchemaFileName}.schema';");
            }
            sb.AppendLine();
        }
        
        if (queryParamsModelImports.Count > 0 && !string.IsNullOrEmpty(data.ModelsPath))
        {
            foreach (var modelImport in queryParamsModelImports.OrderBy(x => x.TypeName))
            {
                sb.AppendLine($"import type {{ {modelImport.TypeName} }} from '{data.ModelsPath}/{modelImport.FileName}.model';");
            }
            sb.AppendLine();
        }
        
        // Генерируем операции
        foreach (var operationBase in data.Operations)
        {
            sb.AppendLine();
            RenderAxiosOperation(sb, operationBase, hasCustomInstance, customInstanceName, serializeWithSchemas, deserializeWithSchemas);
        }
    }

    private void RenderFetchOperation(StringBuilder sb, IOperationTemplateData operation)
    {
        // Генерируем JSDoc комментарий
        IReadOnlyList<(string Name, string? Description, string? Type)>? commentParams = !string.IsNullOrEmpty(operation.Description) 
            ? new[] { ("", (string?)operation.Description, (string?)null) } 
            : null;
        CommentGenerator.GenerateJSDocCommentWithParams(sb, 0, operation.Summary, commentParams);
        
        // Генерируем параметры
        var parameters = BuildParameterList(operation);
        var responseType = operation.ReturnTypeInfo.FullTypeName;
        
        // Имя метода в camelCase для соответствия TypeScript конвенции
        var methodName = _namingConverter.ToCamelCase(operation.MethodName);
        
        CodeFormatter.AppendLine(sb, 0, $"export async function {methodName}(");
        if (parameters.Count > 0)
        {
            var paramsStr = string.Join(",\n  ", parameters);
            CodeFormatter.AppendLine(sb, 1, paramsStr);
        }
        CodeFormatter.AppendLine(sb, 0, $"): Promise<{responseType}> {{");
        
        // Генерируем построение пути
        CodeFormatter.AppendLine(sb, 1, $"const path = `{BuildPath(operation.Path, operation.PathParameters)}`;");
        
        // Генерируем query параметры
        GenerateQueryParameters(sb, operation, indentLevel: 1);
        
        // Генерируем headers
        CodeFormatter.AppendLine(sb, 1, "const headers: Record<string, string> = {};");
        foreach (var headerParam in operation.HeaderParameters)
        {
            CodeFormatter.AppendLine(sb, 1, $"if ({headerParam.VariableName} !== undefined) {{");
            CodeFormatter.AppendLine(sb, 2, $"headers['{headerParam.Name}'] = {headerParam.VariableName};");
            CodeFormatter.AppendLine(sb, 1, "}");
        }
        
        // Генерируем request body
        GenerateRequestBody(sb, operation, indentLevel: 1, isAxios: false);
        
        // Генерируем fetch вызов
        if (operation.HasRequestBody)
        {
            CodeFormatter.AppendLine(sb, 1, "const response = await fetch(url, {");
            CodeFormatter.AppendLine(sb, 2, $"method: '{operation.HttpMethod}',");
            CodeFormatter.AppendLine(sb, 2, "headers,");
            if (operation.IsFormData && operation.FormDataProperties != null && operation.FormDataProperties.Count > 0)
            {
                CodeFormatter.AppendLine(sb, 2, "body: formData");
            }
            else
            {
                CodeFormatter.AppendLine(sb, 2, $"body: JSON.stringify({operation.RequestBodyParameter})");
            }
            CodeFormatter.AppendLine(sb, 1, "});");
        }
        else
        {
            CodeFormatter.AppendLine(sb, 1, "const response = await fetch(url, {");
            CodeFormatter.AppendLine(sb, 2, $"method: '{operation.HttpMethod}',");
            CodeFormatter.AppendLine(sb, 2, "headers");
            CodeFormatter.AppendLine(sb, 1, "});");
        }
        
        // Генерируем обработку ответа
        GenerateResponseHandling(sb, operation, indentLevel: 1, isAxios: false);
        
        CodeFormatter.AppendLine(sb, 0, "}");
    }

    private void RenderAxiosOperation(StringBuilder sb, IOperationTemplateData operation, bool hasCustomInstance = false, string? customInstanceName = null, bool serializeWithSchemas = true, bool deserializeWithSchemas = true)
    {
        // Генерируем JSDoc комментарий
        IReadOnlyList<(string Name, string? Description, string? Type)>? commentParams2 = !string.IsNullOrEmpty(operation.Description) 
            ? new[] { ("", (string?)operation.Description, (string?)null) } 
            : null;
        CommentGenerator.GenerateJSDocCommentWithParams(sb, 0, operation.Summary, commentParams2);
        
        // Разделяем параметры на required и optional
        // Используем Dictionary для отслеживания уже добавленных параметров по VariableName (избегаем дублирования)
        var seenParamNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var requiredParams = new List<string>();
        var optionalParams = new List<string>();
        
        // Path parameters всегда required
        foreach (var param in operation.PathParameters)
        {
            if (seenParamNames.Add(param.VariableName))
            {
                requiredParams.Add($"{param.VariableName}: {param.TypeInfo.FullTypeName}");
            }
        }
        
        // Query parameters
        if (operation.UseQueryParametersModel && !string.IsNullOrEmpty(operation.QueryParametersModelTypeName))
        {
            if (seenParamNames.Add("params"))
            {
                var hasRequiredParam = operation.QueryParameters.Any(p => p.IsRequired);
                var optional = hasRequiredParam ? "" : "?";
                var paramStr = $"params{optional}: {operation.QueryParametersModelTypeName}";
                if (hasRequiredParam)
                {
                    requiredParams.Add(paramStr);
                }
                else
                {
                    optionalParams.Add(paramStr);
                }
            }
        }
        else
        {
            foreach (var param in operation.QueryParameters)
            {
                if (seenParamNames.Add(param.VariableName))
                {
                    var optional = param.IsRequired ? "" : "?";
                    var paramStr = $"{param.VariableName}{optional}: {param.TypeInfo.FullTypeName}";
                    if (param.IsRequired)
                    {
                        requiredParams.Add(paramStr);
                    }
                    else
                    {
                        optionalParams.Add(paramStr);
                    }
                }
            }
        }
        
        // Header parameters
        foreach (var param in operation.HeaderParameters)
        {
            if (seenParamNames.Add(param.VariableName))
            {
                var optional = param.IsRequired ? "" : "?";
                var paramStr = $"{param.VariableName}{optional}: {param.TypeInfo.FullTypeName}";
                if (param.IsRequired)
                {
                    requiredParams.Add(paramStr);
                }
                else
                {
                    optionalParams.Add(paramStr);
                }
            }
        }
        
        // Request body
        if (operation.HasRequestBody)
        {
            if (operation.IsFormData && operation.FormDataProperties != null && operation.FormDataProperties.Count > 0)
            {
                if (!string.IsNullOrEmpty(operation.RequestSchemaName) && operation.RequestBodyTypeInfo != null)
                {
                    if (seenParamNames.Add(operation.RequestBodyParameter))
                    {
                        var optional = operation.RequestBodyTypeInfo.IsNullable ? "?" : "";
                        var paramStr = $"{operation.RequestBodyParameter}{optional}: {operation.RequestBodyTypeInfo.FullTypeName}";
                        if (operation.RequestBodyTypeInfo.IsNullable)
                        {
                            optionalParams.Add(paramStr);
                        }
                        else
                        {
                            requiredParams.Add(paramStr);
                        }
                    }
                }
                else
                {
                    foreach (var formProp in operation.FormDataProperties)
                    {
                        if (seenParamNames.Add(formProp.VariableName))
                        {
                            var optional = formProp.IsRequired ? "" : "?";
                            var paramStr = $"{formProp.VariableName}{optional}: {formProp.TypeInfo.FullTypeName}";
                            if (formProp.IsRequired)
                            {
                                requiredParams.Add(paramStr);
                            }
                            else
                            {
                                optionalParams.Add(paramStr);
                            }
                        }
                    }
                }
            }
            else
            {
                if (seenParamNames.Add(operation.RequestBodyParameter))
                {
                    var optional = operation.RequestBodyTypeInfo != null && !operation.RequestBodyTypeInfo.IsNullable ? "" : "?";
                    var typeStr = operation.RequestBodyTypeInfo?.FullTypeName ?? "any";
                    var paramStr = $"{operation.RequestBodyParameter}{optional}: {typeStr}";
                    if (operation.RequestBodyTypeInfo == null || operation.RequestBodyTypeInfo.IsNullable)
                    {
                        optionalParams.Add(paramStr);
                    }
                    else
                    {
                        requiredParams.Add(paramStr);
                    }
                }
            }
        }
        
        // Axios специфичные параметры
        optionalParams.Add("axiosInstance?: AxiosInstance");
        optionalParams.Add("signal?: AbortSignal");
        optionalParams.Add("timeout?: number");
        
        // Объединяем параметры
        var allParams = new List<string>();
        allParams.AddRange(requiredParams);
        allParams.AddRange(optionalParams);
        
        var responseType = operation.ReturnTypeInfo.FullTypeName;
        
        // Имя метода в camelCase для соответствия TypeScript конвенции
        var methodName = _namingConverter.ToCamelCase(operation.MethodName);
        sb.AppendLine($"export async function {methodName}(");
        if (allParams.Count > 0)
        {
            var paramsStr = string.Join(",\n  ", allParams);
            CodeFormatter.AppendLine(sb, 1, paramsStr);
        }
        CodeFormatter.AppendLine(sb, 0, $"): Promise<{responseType}> {{");
        
        // Генерируем построение пути
        CodeFormatter.AppendLine(sb, 1, $"const path = `{BuildPath(operation.Path, operation.PathParameters)}`;");
        
        // Генерируем config
        CodeFormatter.AppendLine(sb, 1, "const config: AxiosRequestConfig = {");
        CodeFormatter.AppendLine(sb, 2, $"method: '{operation.HttpMethod.ToLowerInvariant()}',");
        CodeFormatter.AppendLine(sb, 2, "url: path,");
        
        // Query parameters
        if (operation.QueryParameters.Count > 0)
        {
            if (operation.UseQueryParametersModel && !string.IsNullOrEmpty(operation.QueryParametersModelTypeName))
            {
                CodeFormatter.AppendLine(sb, 2, "params: params,");
            }
            else
            {
                CodeFormatter.AppendLine(sb, 2, "params: {");
                foreach (var queryParam in operation.QueryParameters)
                {
                    CodeFormatter.AppendLine(sb, 3, $"'{queryParam.Name}': {queryParam.VariableName},");
                }
                CodeFormatter.AppendLine(sb, 2, "},");
            }
        }
        
        // Headers
        CodeFormatter.AppendLine(sb, 2, "headers: {");
        foreach (var headerParam in operation.HeaderParameters)
        {
            CodeFormatter.AppendLine(sb, 3, $"...({headerParam.VariableName} !== undefined ? {{ '{headerParam.Name}': {headerParam.VariableName} }} : {{}}),");
        }
        if (operation.HasRequestBody)
        {
            if (operation.IsFormData && operation.FormDataProperties != null && operation.FormDataProperties.Count > 0)
            {
                CodeFormatter.AppendLine(sb, 3, "'Content-Type': 'multipart/form-data',");
            }
            else
            {
                CodeFormatter.AppendLine(sb, 3, $"'Content-Type': '{operation.RequestBodyContentType ?? "application/json"}',");
            }
        }
        CodeFormatter.AppendLine(sb, 2, "},");
        
        // Request body
        GenerateRequestBody(sb, operation, indentLevel: 2, isAxios: true, serializeWithSchemas);
        
        // Signal и timeout
        CodeFormatter.AppendLine(sb, 2, "...(signal !== undefined ? { signal } : {}),");
        CodeFormatter.AppendLine(sb, 2, "...(timeout !== undefined ? { timeout } : {}),");
        CodeFormatter.AppendLine(sb, 1, "};");
        CodeFormatter.AppendEmptyLine(sb);
        
        // Вызов Axios
        var instanceName = hasCustomInstance ? customInstanceName : "ValioAxiosClient";
        CodeFormatter.AppendLine(sb, 1, $"const instance = axiosInstance ?? {instanceName};");
        CodeFormatter.AppendLine(sb, 1, $"const response = await instance.request<{responseType}>(config);");
        
        // Обработка ответа
        GenerateResponseHandling(sb, operation, indentLevel: 1, isAxios: true, deserializeWithSchemas);
        
        CodeFormatter.AppendLine(sb, 0, "}");
    }

    private void GenerateProperty(StringBuilder sb, IPropertyTemplateData property, int indentLevel)
    {
        // Генерируем JSDoc комментарий
        CommentGenerator.GenerateJSDocComment(sb, indentLevel, property.Description);
        
        // Генерируем свойство
        var optional = property.IsOptional ? "?" : "";
        var typeString = property.TypeInfo.FullTypeName;
        CodeFormatter.AppendLine(sb, indentLevel, $"{property.Name}{optional}: {typeString};");
    }

    private void GenerateQueryParameters(StringBuilder sb, IOperationTemplateData operation, int indentLevel)
    {
        if (operation.QueryParameters.Count == 0)
        {
            CodeFormatter.AppendLine(sb, indentLevel, "const url = path;");
            return;
        }
        
        if (operation.UseQueryParametersModel && !string.IsNullOrEmpty(operation.QueryParametersModelTypeName))
        {
            CodeFormatter.AppendLine(sb, indentLevel, "const queryParams = params ? Object.entries(params)");
            CodeFormatter.AppendLine(sb, indentLevel + 1, ".filter(([_, value]) => value !== undefined && value !== null)");
            CodeFormatter.AppendLine(sb, indentLevel + 1, ".map(([key, value]) => `${key}=${encodeURIComponent(String(value))}`)");
            CodeFormatter.AppendLine(sb, indentLevel + 1, ".join('&') : '';");
            CodeFormatter.AppendLine(sb, indentLevel, "const url = queryParams ? `${path}?${queryParams}` : path;");
        }
        else
        {
            CodeFormatter.AppendLine(sb, indentLevel, "const queryParams = [");
            foreach (var queryParam in operation.QueryParameters)
            {
                if (queryParam.IsRequired)
                {
                    CodeFormatter.AppendLine(sb, indentLevel + 1, $"`{queryParam.Name}=${{{queryParam.VariableName}}}`,");
                }
                else
                {
                    CodeFormatter.AppendLine(sb, indentLevel + 1, $"{queryParam.VariableName} !== undefined ? `{queryParam.Name}=${{{queryParam.VariableName}}}` : null,");
                }
            }
            CodeFormatter.AppendLine(sb, indentLevel, "].filter(Boolean).join('&');");
            CodeFormatter.AppendLine(sb, indentLevel, "const url = queryParams ? `${path}?${queryParams}` : path;");
        }
    }

    private void GenerateRequestBody(StringBuilder sb, IOperationTemplateData operation, int indentLevel, bool isAxios, bool serializeWithSchemas = true)
    {
        if (!operation.HasRequestBody)
            return;
        
        if (operation.IsFormData && operation.FormDataProperties != null && operation.FormDataProperties.Count > 0)
        {
            if (isAxios)
            {
                if (serializeWithSchemas && !string.IsNullOrEmpty(operation.RequestSchemaName))
                {
                    CodeFormatter.AppendLine(sb, indentLevel, "data: (() => {");
                    CodeFormatter.AppendLine(sb, indentLevel + 1, "// Валидация и конвертация через Zod схему");
                    CodeFormatter.AppendLine(sb, indentLevel + 1, $"const parsedRequest = {operation.RequestSchemaName}.parse({operation.RequestBodyParameter});");
                    CodeFormatter.AppendLine(sb, indentLevel + 1, "const dto = parsedRequest;");
                    CodeFormatter.AppendLine(sb, indentLevel + 1, "const formData = new FormData();");
                    foreach (var formProp in operation.FormDataProperties)
                    {
                        CodeFormatter.AppendLine(sb, indentLevel + 1, $"if (dto.{formProp.VariableName} !== undefined && dto.{formProp.VariableName} !== null) {{");
                        CodeFormatter.AppendLine(sb, indentLevel + 2, $"formData.append('{formProp.Name}', dto.{formProp.VariableName});");
                        CodeFormatter.AppendLine(sb, indentLevel + 1, "}");
                    }
                    CodeFormatter.AppendLine(sb, indentLevel + 1, "return formData;");
                    CodeFormatter.AppendLine(sb, indentLevel, "})(),");
                }
                else
                {
                    CodeFormatter.AppendLine(sb, indentLevel, "data: (() => {");
                    CodeFormatter.AppendLine(sb, indentLevel + 1, "const formData = new FormData();");
                    foreach (var formProp in operation.FormDataProperties)
                    {
                        if (formProp.IsRequired)
                        {
                            CodeFormatter.AppendLine(sb, indentLevel + 1, $"formData.append('{formProp.Name}', {formProp.VariableName});");
                        }
                        else
                        {
                            CodeFormatter.AppendLine(sb, indentLevel + 1, $"if ({formProp.VariableName} !== undefined) {{");
                            CodeFormatter.AppendLine(sb, indentLevel + 2, $"formData.append('{formProp.Name}', {formProp.VariableName});");
                            CodeFormatter.AppendLine(sb, indentLevel + 1, "}");
                        }
                    }
                    CodeFormatter.AppendLine(sb, indentLevel + 1, "return formData;");
                    CodeFormatter.AppendLine(sb, indentLevel, "})(),");
                }
            }
            else
            {
                CodeFormatter.AppendLine(sb, indentLevel, "const formData = new FormData();");
                foreach (var formProp in operation.FormDataProperties)
                {
                    if (formProp.IsRequired)
                    {
                        CodeFormatter.AppendLine(sb, indentLevel, $"formData.append('{formProp.Name}', {formProp.VariableName});");
                    }
                    else
                    {
                        CodeFormatter.AppendLine(sb, indentLevel, $"if ({formProp.VariableName} !== undefined) {{");
                        CodeFormatter.AppendLine(sb, indentLevel + 1, $"formData.append('{formProp.Name}', {formProp.VariableName});");
                        CodeFormatter.AppendLine(sb, indentLevel, "}");
                    }
                }
            }
        }
        else
        {
            if (isAxios)
            {
                if (serializeWithSchemas && !string.IsNullOrEmpty(operation.RequestSchemaName))
                {
                    CodeFormatter.AppendLine(sb, indentLevel, "data: (() => {");
                    CodeFormatter.AppendLine(sb, indentLevel + 1, "// Валидация и конвертация через Zod схему");
                    CodeFormatter.AppendLine(sb, indentLevel + 1, $"const parsedRequest = {operation.RequestSchemaName}.parse({operation.RequestBodyParameter});");
                    CodeFormatter.AppendLine(sb, indentLevel + 1, "return parsedRequest;");
                    CodeFormatter.AppendLine(sb, indentLevel, "})(),");
                }
                else
                {
                    CodeFormatter.AppendLine(sb, indentLevel, $"data: {operation.RequestBodyParameter},");
                }
            }
            else
            {
                // Для Fetch это уже обрабатывается в RenderFetchOperation
            }
        }
    }

    private void GenerateResponseHandling(StringBuilder sb, IOperationTemplateData operation, int indentLevel, bool isAxios, bool deserializeWithSchemas = true)
    {
        var responseTypeInfo = operation.ReturnTypeInfo;
        var shouldReturnData = responseTypeInfo.BaseType != "void";
        
        if (!isAxios)
        {
            CodeFormatter.AppendLine(sb, indentLevel, "if (!response.ok) {");
            CodeFormatter.AppendLine(sb, indentLevel + 1, "throw new Error(`HTTP error! status: ${response.status}`);");
            CodeFormatter.AppendLine(sb, indentLevel, "}");
        }
        
        if (shouldReturnData)
        {
            if (isAxios)
            {
                if (deserializeWithSchemas && !string.IsNullOrEmpty(operation.ResponseSchemaName))
                {
                    CodeFormatter.AppendLine(sb, indentLevel, "// Валидация и конвертация через Zod схему");
                    CodeFormatter.AppendLine(sb, indentLevel, $"const parsedResponse = {operation.ResponseSchemaName}.parse(response.data);");
                    CodeFormatter.AppendLine(sb, indentLevel, "return parsedResponse;");
                }
                else
                {
                    CodeFormatter.AppendLine(sb, indentLevel, "return response.data;");
                }
            }
            else
            {
                var responseType = operation.ReturnTypeInfo.FullTypeName;
                CodeFormatter.AppendLine(sb, indentLevel, $"return await response.json() as {responseType};");
            }
        }
        else
        {
            if (!isAxios)
            {
                CodeFormatter.AppendLine(sb, indentLevel, "return;");
            }
        }
    }

    private List<string> BuildParameterList(IOperationTemplateData operation)
    {
        // Используем HashSet для отслеживания уже добавленных параметров по VariableName (избегаем дублирования)
        var seenParamNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var parameters = new List<string>();
        
        // Path parameters
        foreach (var param in operation.PathParameters)
        {
            if (seenParamNames.Add(param.VariableName))
            {
                parameters.Add($"{param.VariableName}: {param.TypeInfo.FullTypeName}");
            }
        }
        
        // Query parameters
        if (operation.UseQueryParametersModel && !string.IsNullOrEmpty(operation.QueryParametersModelTypeName))
        {
            if (seenParamNames.Add("params"))
            {
                var hasRequiredParam = operation.QueryParameters.Any(p => p.IsRequired);
                var optional = hasRequiredParam ? "" : "?";
                parameters.Add($"params{optional}: {operation.QueryParametersModelTypeName}");
            }
        }
        else
        {
            foreach (var param in operation.QueryParameters)
            {
                if (seenParamNames.Add(param.VariableName))
                {
                    var optional = param.IsRequired ? "" : "?";
                    parameters.Add($"{param.VariableName}{optional}: {param.TypeInfo.FullTypeName}");
                }
            }
        }
        
        // Header parameters
        foreach (var param in operation.HeaderParameters)
        {
            if (seenParamNames.Add(param.VariableName))
            {
                var optional = param.IsRequired ? "" : "?";
                parameters.Add($"{param.VariableName}{optional}: {param.TypeInfo.FullTypeName}");
            }
        }
        
        // Request body
        if (operation.HasRequestBody)
        {
            if (operation.IsFormData && operation.FormDataProperties != null && operation.FormDataProperties.Count > 0)
            {
                foreach (var formProp in operation.FormDataProperties)
                {
                    if (seenParamNames.Add(formProp.VariableName))
                    {
                        var optional = formProp.IsRequired ? "" : "?";
                        parameters.Add($"{formProp.VariableName}{optional}: {formProp.TypeInfo.FullTypeName}");
                    }
                }
            }
            else
            {
                if (seenParamNames.Add(operation.RequestBodyParameter))
                {
                    var optional = operation.RequestBodyTypeInfo != null && !operation.RequestBodyTypeInfo.IsNullable ? "" : "?";
                    var typeStr = operation.RequestBodyTypeInfo?.FullTypeName ?? "any";
                    parameters.Add($"{operation.RequestBodyParameter}{optional}: {typeStr}");
                }
            }
        }
        
        return parameters;
    }

    private string BuildPath(string path, IReadOnlyList<ParameterTemplateData> pathParams)
    {
        var result = path;
        foreach (var param in pathParams)
        {
            result = result.Replace($"{{{param.Name}}}", $"${{{param.VariableName}}}");
        }
        return result;
    }


    private string GetModelFileName(string typeName)
    {
        if (string.IsNullOrEmpty(typeName))
            return string.Empty;
        
        // Преобразуем PascalCase в camelCase для имени файла
        return char.ToLowerInvariant(typeName[0]) + typeName.Substring(1);
    }

    private string GetSchemaFileName(string schemaName)
    {
        if (string.IsNullOrEmpty(schemaName))
            return string.Empty;
        
        // Убираем "Schema" из конца, если есть
        if (schemaName.EndsWith("Schema"))
        {
            schemaName = schemaName.Substring(0, schemaName.Length - 6);
        }
        
        // Преобразуем PascalCase в camelCase для имени файла
        return char.ToLowerInvariant(schemaName[0]) + schemaName.Substring(1);
    }

    // Метод EscapeJSDocComment теперь в ValIO.Core.Utilities.CodeEscapeHelper

    // Метод GetOption удален - теперь используется типизированный доступ к Options через generic TemplateData типы

    #endregion
}
