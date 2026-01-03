using System.Text;
using ValIO.Core.Interfaces;
using ValIO.Core.Models;
using ValIO.Core.Models.TemplateData;

namespace ValIO.Generators.TypeScript;

/// <summary>
/// Рендерер для генерации TypeScript кода через StringBuilders
/// </summary>
public class TypeScriptCodeRenderer : ICodeRenderer
{
    public GenerationLanguage Language => GenerationLanguage.TypeScript;

    /// <summary>
    /// Рендерит модель (interface/type)
    /// </summary>
    public string RenderModel(ModelTemplateData data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        var sb = new StringBuilder();
        
        // Извлекаем TypeScript специфичные опции из Options
        var useZodInfer = GetOption<bool>(data.Options, "UseZodInfer", false);
        var useStrictNullChecks = GetOption<bool>(data.Options, "UseStrictNullChecks", true);
        var validationLibrary = GetOption<ValidationLibrary?>(data.Options, "ValidationLibrary", null);
        var useZod = validationLibrary == ValidationLibrary.Zod;
        
        // Если используется Zod, генерируем через z.infer
        if (useZodInfer || useZod)
        {
            var schemaName = $"{data.Name}Schema";
            var schemaFileName = GetSchemaFileName(data.Name);
            
            sb.AppendLine($"import {{ {schemaName} }} from './{schemaFileName}.schema';");
            sb.AppendLine("import { z } from 'zod';");
            sb.AppendLine();
            
            if (!string.IsNullOrEmpty(data.Description))
            {
                sb.AppendLine("/**");
                sb.AppendLine($" * {EscapeJSDocComment(data.Description)}");
                sb.AppendLine(" */");
            }
            
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
        if (!string.IsNullOrEmpty(data.Description))
        {
            sb.AppendLine("/**");
            sb.AppendLine($" * {EscapeJSDocComment(data.Description)}");
            sb.AppendLine(" */");
        }
        
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
                GenerateProperty(sb, property, indent: "  ");
            }
            sb.AppendLine("};");
        }
        else
        {
            // Обычный interface
            sb.AppendLine($"export interface {data.Name} {{");
            foreach (var property in data.Properties)
            {
                GenerateProperty(sb, property, indent: "  ");
            }
            sb.AppendLine("}");
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
        
        // Извлекаем TypeScript специфичные опции
        var useZodInfer = GetOption<bool>(data.Options, "UseZodInfer", false);
        var validationLibrary = GetOption<ValidationLibrary?>(data.Options, "ValidationLibrary", null);
        var useZod = validationLibrary == ValidationLibrary.Zod;
        
        // Если используется Zod, генерируем через z.infer
        if (useZodInfer || useZod)
        {
            var schemaName = $"{data.Name}Schema";
            var schemaFileName = GetSchemaFileName(data.Name);
            
            sb.AppendLine($"import {{ {schemaName} }} from './{schemaFileName}.schema';");
            sb.AppendLine("import { z } from 'zod';");
            sb.AppendLine();
            
            if (!string.IsNullOrEmpty(data.Description))
            {
                sb.AppendLine("/**");
                sb.AppendLine($" * {EscapeJSDocComment(data.Description)}");
                sb.AppendLine(" */");
            }
            
            sb.AppendLine($"export type {data.Name} = z.infer<typeof {schemaName}>;");
            return sb.ToString();
        }
        
        // Стандартная генерация enum'ов
        if (!string.IsNullOrEmpty(data.Description))
        {
            sb.AppendLine("/**");
            sb.AppendLine($" * {EscapeJSDocComment(data.Description)}");
            sb.AppendLine(" */");
        }
        
        if (data.Format == EnumFormat.Enum)
        {
            // TypeScript enum
            sb.AppendLine($"export enum {data.Name} {{");
            for (int i = 0; i < data.Values.Count; i++)
            {
                var value = data.Values[i];
                
                if (!string.IsNullOrEmpty(value.Description))
                {
                    sb.AppendLine("  /**");
                    sb.AppendLine($"   * {EscapeJSDocComment(value.Description)}");
                    sb.AppendLine("   */");
                }
                
                var comma = i < data.Values.Count - 1 ? "," : "";
                sb.AppendLine($"  {value.Name} = {value.ValueString}{comma}");
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
    public string RenderClient(ClientTemplateData data)
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
    public string RenderOperation(OperationTemplateData data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        var sb = new StringBuilder();
        
        // Определяем тип HTTP клиента из Options
        var httpClientLibrary = GetOption<HttpClientLibrary?>(data.Options, "HttpClientLibrary", HttpClientLibrary.Fetch);
        var isAxios = httpClientLibrary == HttpClientLibrary.Axios;
        
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

    private void RenderFetchClient(StringBuilder sb, ClientTemplateData data)
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
        foreach (var operation in data.Operations)
        {
            sb.AppendLine();
            RenderFetchOperation(sb, operation);
        }
    }

    private void RenderAxiosClient(StringBuilder sb, ClientTemplateData data)
    {
        // Генерируем импорты Axios
        sb.AppendLine("import { AxiosInstance, AxiosRequestConfig } from 'axios';");
        
        // Проверяем наличие кастомного экземпляра
        var customInstancePath = GetOption<string>(data.Options, "CustomInstancePath", null);
        var customInstanceName = GetOption<string>(data.Options, "CustomInstanceName", null);
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
        
        // Генерируем импорты схем (Zod)
        var schemaImports = new HashSet<(string SchemaName, string SchemaFileName)>();
        var queryParamsModelImports = new HashSet<(string TypeName, string FileName)>();
        
        foreach (var operation in data.Operations)
        {
            if (!string.IsNullOrEmpty(operation.RequestSchemaName) && 
                !string.IsNullOrEmpty(operation.RequestSchemaImportPath) &&
                !string.IsNullOrEmpty(data.ModelsPath))
            {
                var fileName = GetSchemaFileName(operation.RequestSchemaName);
                schemaImports.Add((operation.RequestSchemaName, fileName));
            }
            if (!string.IsNullOrEmpty(operation.ResponseSchemaName) && 
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
        foreach (var operation in data.Operations)
        {
            sb.AppendLine();
            RenderAxiosOperation(sb, operation, hasCustomInstance, customInstanceName);
        }
    }

    private void RenderFetchOperation(StringBuilder sb, OperationTemplateData operation)
    {
        // Генерируем JSDoc комментарий
        if (!string.IsNullOrEmpty(operation.Summary))
        {
            sb.AppendLine("/**");
            sb.AppendLine($" * {EscapeJSDocComment(operation.Summary)}");
            if (!string.IsNullOrEmpty(operation.Description))
            {
                sb.AppendLine($" * {EscapeJSDocComment(operation.Description)}");
            }
            sb.AppendLine(" */");
        }
        
        // Генерируем параметры
        var parameters = BuildParameterList(operation);
        var responseType = GetResponseType(operation.ReturnTypeInfo);
        
        sb.AppendLine($"export async function {operation.MethodName}(");
        if (parameters.Count > 0)
        {
            sb.AppendLine("  " + string.Join(",\n  ", parameters));
        }
        sb.AppendLine($"): Promise<{responseType}> {{");
        
        // Генерируем построение пути
        sb.AppendLine($"  const path = `{BuildPath(operation.Path, operation.PathParameters)}`;");
        
        // Генерируем query параметры
        GenerateQueryParameters(sb, operation, indent: "  ");
        
        // Генерируем headers
        sb.AppendLine("  const headers: Record<string, string> = {};");
        foreach (var headerParam in operation.HeaderParameters)
        {
            sb.AppendLine($"  if ({headerParam.VariableName} !== undefined) {{");
            sb.AppendLine($"    headers['{headerParam.Name}'] = {headerParam.VariableName};");
            sb.AppendLine("  }");
        }
        
        // Генерируем request body
        GenerateRequestBody(sb, operation, indent: "  ", isAxios: false);
        
        // Генерируем fetch вызов
        if (operation.HasRequestBody)
        {
            sb.AppendLine("  const response = await fetch(url, {");
            sb.AppendLine($"    method: '{operation.HttpMethod}',");
            sb.AppendLine("    headers,");
            if (operation.IsFormData && operation.FormDataProperties != null && operation.FormDataProperties.Count > 0)
            {
                sb.AppendLine("    body: formData");
            }
            else
            {
                sb.AppendLine($"    body: JSON.stringify({operation.RequestBodyParameter})");
            }
            sb.AppendLine("  });");
        }
        else
        {
            sb.AppendLine("  const response = await fetch(url, {");
            sb.AppendLine($"    method: '{operation.HttpMethod}',");
            sb.AppendLine("    headers");
            sb.AppendLine("  });");
        }
        
        // Генерируем обработку ответа
        GenerateResponseHandling(sb, operation, indent: "  ", isAxios: false);
        
        sb.AppendLine("}");
    }

    private void RenderAxiosOperation(StringBuilder sb, OperationTemplateData operation, bool hasCustomInstance = false, string? customInstanceName = null)
    {
        // Генерируем JSDoc комментарий
        if (!string.IsNullOrEmpty(operation.Summary))
        {
            sb.AppendLine("/**");
            sb.AppendLine($" * {EscapeJSDocComment(operation.Summary)}");
            if (!string.IsNullOrEmpty(operation.Description))
            {
                sb.AppendLine($" * {EscapeJSDocComment(operation.Description)}");
            }
            sb.AppendLine(" */");
        }
        
        // Разделяем параметры на required и optional
        var requiredParams = new List<string>();
        var optionalParams = new List<string>();
        
        // Path parameters всегда required
        foreach (var param in operation.PathParameters)
        {
            requiredParams.Add($"{param.VariableName}: {FormatType(param.TypeInfo)}");
        }
        
        // Query parameters
        if (operation.UseQueryParametersModel && !string.IsNullOrEmpty(operation.QueryParametersModelTypeName))
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
        else
        {
            foreach (var param in operation.QueryParameters)
            {
                var optional = param.IsRequired ? "" : "?";
                var paramStr = $"{param.VariableName}{optional}: {FormatType(param.TypeInfo)}";
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
        
        // Header parameters
        foreach (var param in operation.HeaderParameters)
        {
            var optional = param.IsRequired ? "" : "?";
            var paramStr = $"{param.VariableName}{optional}: {FormatType(param.TypeInfo)}";
            if (param.IsRequired)
            {
                requiredParams.Add(paramStr);
            }
            else
            {
                optionalParams.Add(paramStr);
            }
        }
        
        // Request body
        if (operation.HasRequestBody)
        {
            if (operation.IsFormData && operation.FormDataProperties != null && operation.FormDataProperties.Count > 0)
            {
                if (!string.IsNullOrEmpty(operation.RequestSchemaName) && operation.RequestBodyTypeInfo != null)
                {
                    var optional = operation.RequestBodyTypeInfo.IsNullable ? "?" : "";
                    var paramStr = $"{operation.RequestBodyParameter}{optional}: {FormatType(operation.RequestBodyTypeInfo)}";
                    if (operation.RequestBodyTypeInfo.IsNullable)
                    {
                        optionalParams.Add(paramStr);
                    }
                    else
                    {
                        requiredParams.Add(paramStr);
                    }
                }
                else
                {
                    foreach (var formProp in operation.FormDataProperties)
                    {
                        var optional = formProp.IsRequired ? "" : "?";
                        var paramStr = $"{formProp.VariableName}{optional}: {FormatType(formProp.TypeInfo)}";
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
            else
            {
                var optional = operation.RequestBodyTypeInfo != null && !operation.RequestBodyTypeInfo.IsNullable ? "" : "?";
                var typeStr = operation.RequestBodyTypeInfo != null ? FormatType(operation.RequestBodyTypeInfo) : "any";
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
        
        // Axios специфичные параметры
        optionalParams.Add("axiosInstance?: AxiosInstance");
        optionalParams.Add("signal?: AbortSignal");
        optionalParams.Add("timeout?: number");
        
        // Объединяем параметры
        var allParams = new List<string>();
        allParams.AddRange(requiredParams);
        allParams.AddRange(optionalParams);
        
        var responseType = GetResponseType(operation.ReturnTypeInfo);
        
        sb.AppendLine($"export async function {operation.MethodName}(");
        if (allParams.Count > 0)
        {
            sb.AppendLine("  " + string.Join(",\n  ", allParams));
        }
        sb.AppendLine($"): Promise<{responseType}> {{");
        
        // Генерируем построение пути
        sb.AppendLine($"  const path = `{BuildPath(operation.Path, operation.PathParameters)}`;");
        
        // Генерируем config
        sb.AppendLine("  const config: AxiosRequestConfig = {");
        sb.AppendLine($"    method: '{operation.HttpMethod.ToLowerInvariant()}',");
        sb.AppendLine("    url: path,");
        
        // Query parameters
        if (operation.QueryParameters.Count > 0)
        {
            if (operation.UseQueryParametersModel && !string.IsNullOrEmpty(operation.QueryParametersModelTypeName))
            {
                sb.AppendLine("    params: params,");
            }
            else
            {
                sb.AppendLine("    params: {");
                foreach (var queryParam in operation.QueryParameters)
                {
                    sb.AppendLine($"      '{queryParam.Name}': {queryParam.VariableName},");
                }
                sb.AppendLine("    },");
            }
        }
        
        // Headers
        sb.AppendLine("    headers: {");
        foreach (var headerParam in operation.HeaderParameters)
        {
            sb.AppendLine($"      ...({headerParam.VariableName} !== undefined ? {{ '{headerParam.Name}': {headerParam.VariableName} }} : {{}}),");
        }
        if (operation.HasRequestBody)
        {
            if (operation.IsFormData && operation.FormDataProperties != null && operation.FormDataProperties.Count > 0)
            {
                sb.AppendLine("      'Content-Type': 'multipart/form-data',");
            }
            else
            {
                sb.AppendLine($"      'Content-Type': '{operation.RequestBodyContentType ?? "application/json"}',");
            }
        }
        sb.AppendLine("    },");
        
        // Request body
        GenerateRequestBody(sb, operation, indent: "    ", isAxios: true);
        
        // Signal и timeout
        sb.AppendLine("    ...(signal !== undefined ? { signal } : {}),");
        sb.AppendLine("    ...(timeout !== undefined ? { timeout } : {}),");
        sb.AppendLine("  };");
        sb.AppendLine();
        
        // Вызов Axios
        var instanceName = hasCustomInstance ? customInstanceName : "ValioAxiosClient";
        sb.AppendLine($"  const instance = axiosInstance ?? {instanceName};");
        sb.AppendLine($"  const response = await instance.request<{responseType}>(config);");
        
        // Обработка ответа
        GenerateResponseHandling(sb, operation, indent: "  ", isAxios: true);
        
        sb.AppendLine("}");
    }

    private void GenerateProperty(StringBuilder sb, PropertyTemplateData property, string indent)
    {
        // Генерируем JSDoc комментарий
        if (!string.IsNullOrEmpty(property.Description))
        {
            sb.AppendLine($"{indent}/**");
            sb.AppendLine($"{indent} * {EscapeJSDocComment(property.Description)}");
            sb.AppendLine($"{indent} */");
        }
        
        // Генерируем свойство
        var optional = property.IsOptional ? "?" : "";
        var typeString = FormatType(property.TypeInfo);
        sb.AppendLine($"{indent}{property.Name}{optional}: {typeString};");
    }

    private void GenerateQueryParameters(StringBuilder sb, OperationTemplateData operation, string indent)
    {
        if (operation.QueryParameters.Count == 0)
        {
            sb.AppendLine($"{indent}const url = path;");
            return;
        }
        
        if (operation.UseQueryParametersModel && !string.IsNullOrEmpty(operation.QueryParametersModelTypeName))
        {
            sb.AppendLine($"{indent}const queryParams = params ? Object.entries(params)");
            sb.AppendLine($"{indent}  .filter(([_, value]) => value !== undefined && value !== null)");
            sb.AppendLine($"{indent}  .map(([key, value]) => `${{key}}=${{encodeURIComponent(String(value))}}`)");
            sb.AppendLine($"{indent}  .join('&') : '';");
            sb.AppendLine($"{indent}const url = queryParams ? `${{path}}?${{queryParams}}` : path;");
        }
        else
        {
            sb.AppendLine($"{indent}const queryParams = [");
            foreach (var queryParam in operation.QueryParameters)
            {
                if (queryParam.IsRequired)
                {
                    sb.AppendLine($"{indent}  `{queryParam.Name}=${{{queryParam.VariableName}}}`,");
                }
                else
                {
                    sb.AppendLine($"{indent}  {queryParam.VariableName} !== undefined ? `{queryParam.Name}=${{{queryParam.VariableName}}}` : null,");
                }
            }
            sb.AppendLine($"{indent}].filter(Boolean).join('&');");
            sb.AppendLine($"{indent}const url = queryParams ? `${{path}}?${{queryParams}}` : path;");
        }
    }

    private void GenerateRequestBody(StringBuilder sb, OperationTemplateData operation, string indent, bool isAxios)
    {
        if (!operation.HasRequestBody)
            return;
        
        if (operation.IsFormData && operation.FormDataProperties != null && operation.FormDataProperties.Count > 0)
        {
            if (isAxios)
            {
                if (!string.IsNullOrEmpty(operation.RequestSchemaName))
                {
                    sb.AppendLine($"{indent}data: (() => {{");
                    sb.AppendLine($"{indent}  // Валидация и конвертация через Zod схему");
                    sb.AppendLine($"{indent}  const dto = {operation.RequestSchemaName}.parse({operation.RequestBodyParameter});");
                    sb.AppendLine($"{indent}  const formData = new FormData();");
                    foreach (var formProp in operation.FormDataProperties)
                    {
                        sb.AppendLine($"{indent}  if (dto.{formProp.VariableName} !== undefined && dto.{formProp.VariableName} !== null) {{");
                        sb.AppendLine($"{indent}    formData.append('{formProp.Name}', dto.{formProp.VariableName});");
                        sb.AppendLine($"{indent}  }}");
                    }
                    sb.AppendLine($"{indent}  return formData;");
                    sb.AppendLine($"{indent}}})(),");
                }
                else
                {
                    sb.AppendLine($"{indent}data: (() => {{");
                    sb.AppendLine($"{indent}  const formData = new FormData();");
                    foreach (var formProp in operation.FormDataProperties)
                    {
                        if (formProp.IsRequired)
                        {
                            sb.AppendLine($"{indent}  formData.append('{formProp.Name}', {formProp.VariableName});");
                        }
                        else
                        {
                            sb.AppendLine($"{indent}  if ({formProp.VariableName} !== undefined) {{");
                            sb.AppendLine($"{indent}    formData.append('{formProp.Name}', {formProp.VariableName});");
                            sb.AppendLine($"{indent}  }}");
                        }
                    }
                    sb.AppendLine($"{indent}  return formData;");
                    sb.AppendLine($"{indent}}})(),");
                }
            }
            else
            {
                sb.AppendLine($"{indent}const formData = new FormData();");
                foreach (var formProp in operation.FormDataProperties)
                {
                    if (formProp.IsRequired)
                    {
                        sb.AppendLine($"{indent}formData.append('{formProp.Name}', {formProp.VariableName});");
                    }
                    else
                    {
                        sb.AppendLine($"{indent}if ({formProp.VariableName} !== undefined) {{");
                        sb.AppendLine($"{indent}  formData.append('{formProp.Name}', {formProp.VariableName});");
                        sb.AppendLine($"{indent}}}");
                    }
                }
            }
        }
        else
        {
            if (isAxios)
            {
                if (!string.IsNullOrEmpty(operation.RequestSchemaName))
                {
                    sb.AppendLine($"{indent}data: (() => {{");
                    sb.AppendLine($"{indent}  // Валидация и конвертация через Zod схему");
                    sb.AppendLine($"{indent}  return {operation.RequestSchemaName}.parse({operation.RequestBodyParameter});");
                    sb.AppendLine($"{indent}}})(),");
                }
                else
                {
                    sb.AppendLine($"{indent}data: {operation.RequestBodyParameter},");
                }
            }
            else
            {
                // Для Fetch это уже обрабатывается в RenderFetchOperation
            }
        }
    }

    private void GenerateResponseHandling(StringBuilder sb, OperationTemplateData operation, string indent, bool isAxios)
    {
        var responseTypeInfo = operation.ReturnTypeInfo.BaseType == "Task" && operation.ReturnTypeInfo.GenericArguments.Count > 0
            ? operation.ReturnTypeInfo.GenericArguments[0]
            : operation.ReturnTypeInfo;
        var shouldReturnData = !responseTypeInfo.IsVoid;
        
        if (!isAxios)
        {
            sb.AppendLine($"{indent}if (!response.ok) {{");
            sb.AppendLine($"{indent}  throw new Error(`HTTP error! status: ${{response.status}}`);");
            sb.AppendLine($"{indent}}}");
        }
        
        if (shouldReturnData)
        {
            if (isAxios)
            {
                if (!string.IsNullOrEmpty(operation.ResponseSchemaName))
                {
                    sb.AppendLine($"{indent}// Валидация и конвертация через Zod схему");
                    sb.AppendLine($"{indent}return {operation.ResponseSchemaName}.parse(response.data);");
                }
                else
                {
                    sb.AppendLine($"{indent}return response.data;");
                }
            }
            else
            {
                var responseType = GetResponseType(operation.ReturnTypeInfo);
                sb.AppendLine($"{indent}return await response.json() as {responseType};");
            }
        }
        else
        {
            if (!isAxios)
            {
                sb.AppendLine($"{indent}return;");
            }
        }
    }

    private List<string> BuildParameterList(OperationTemplateData operation)
    {
        var parameters = new List<string>();
        
        // Path parameters
        foreach (var param in operation.PathParameters)
        {
            parameters.Add($"{param.VariableName}: {FormatType(param.TypeInfo)}");
        }
        
        // Query parameters
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
        
        // Header parameters
        foreach (var param in operation.HeaderParameters)
        {
            var optional = param.IsRequired ? "" : "?";
            parameters.Add($"{param.VariableName}{optional}: {FormatType(param.TypeInfo)}");
        }
        
        // Request body
        if (operation.HasRequestBody)
        {
            if (operation.IsFormData && operation.FormDataProperties != null && operation.FormDataProperties.Count > 0)
            {
                foreach (var formProp in operation.FormDataProperties)
                {
                    var optional = formProp.IsRequired ? "" : "?";
                    parameters.Add($"{formProp.VariableName}{optional}: {FormatType(formProp.TypeInfo)}");
                }
            }
            else
            {
                var optional = operation.RequestBodyTypeInfo != null && !operation.RequestBodyTypeInfo.IsNullable ? "" : "?";
                var typeStr = operation.RequestBodyTypeInfo != null ? FormatType(operation.RequestBodyTypeInfo) : "any";
                parameters.Add($"{operation.RequestBodyParameter}{optional}: {typeStr}");
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

    private string FormatType(TypeInfo typeInfo)
    {
        var typeName = typeInfo.FullTypeName;
        // Убираем "?" из типа, если он там есть - мы добавим его к имени свойства
        if (typeName.EndsWith("?"))
        {
            typeName = typeName.Substring(0, typeName.Length - 1).Trim();
        }
        return typeName;
    }

    private string GetResponseType(TypeInfo returnTypeInfo)
    {
        if (returnTypeInfo.BaseType == "Task" && returnTypeInfo.GenericArguments.Count > 0)
        {
            return returnTypeInfo.GenericArguments[0].FullTypeName;
        }
        return returnTypeInfo.FullTypeName;
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

    private string EscapeJSDocComment(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;
        
        return text
            .Replace("*/", "* /")
            .Replace("/*", "/ *");
    }

    private T GetOption<T>(Dictionary<string, object> options, string key, T defaultValue)
    {
        if (options == null || !options.TryGetValue(key, out var value))
            return defaultValue;
        
        if (value is T directValue)
            return directValue;
        
        if (value is IConvertible convertible)
        {
            try
            {
                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch
            {
                return defaultValue;
            }
        }
        
        return defaultValue;
    }

    #endregion
}
