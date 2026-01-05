using ValIO.Core.Models;
using ValIO.Core.Models.TemplateData;
using ValIO.Generators.TypeScript.Models;

namespace ValIO.Generators.TypeScript.Models.CodeGeneration;

/// <summary>
/// Builder для создания OptionsDefinition
/// </summary>
public static class OptionsDefinitionBuilder
{
    /// <summary>
    /// Создает OptionsDefinition для query hooks
    /// </summary>
    public static OptionsDefinition BuildQueryOptions(
        string awaitedReturnTypeName,
        TypeScriptGenerationOptions options)
    {
        var properties = new List<OptionsProperty>
        {
            new()
            {
                Name = "query",
                Type = $"Partial<UseQueryOptions<{awaitedReturnTypeName}, TError, TData>>",
                IsOptional = true
            },
            new()
            {
                Name = "timeout",
                Type = "number",
                IsOptional = true
            }
        };
        
        if (options.HttpClientLibrary == HttpClientLibrary.Axios)
        {
            properties.Add(new OptionsProperty
            {
                Name = "axiosInstance",
                Type = "AxiosInstance",
                IsOptional = true
            });
        }
        
        return new OptionsDefinition
        {
            Properties = properties,
            IsOptional = true
        };
    }
    
    /// <summary>
    /// Создает OptionsDefinition для infinite query hooks
    /// </summary>
    public static OptionsDefinition BuildInfiniteQueryOptions(
        string awaitedReturnTypeName,
        TypeScriptGenerationOptions options)
    {
        var properties = new List<OptionsProperty>
        {
            new()
            {
                Name = "query",
                Type = $"Partial<UseInfiniteQueryOptions<{awaitedReturnTypeName}, TError, TData, QueryKey, unknown>>",
                IsOptional = true
            },
            new()
            {
                Name = "timeout",
                Type = "number",
                IsOptional = true
            }
        };
        
        if (options.HttpClientLibrary == HttpClientLibrary.Axios)
        {
            properties.Add(new OptionsProperty
            {
                Name = "axiosInstance",
                Type = "AxiosInstance",
                IsOptional = true
            });
        }
        
        return new OptionsDefinition
        {
            Properties = properties,
            IsOptional = true
        };
    }
    
    /// <summary>
    /// Создает OptionsDefinition для mutation hooks
    /// </summary>
    public static OptionsDefinition BuildMutationOptions(
        string awaitedReturnTypeName,
        TypeObjectDefinition variablesType,
        TypeScriptGenerationOptions options)
    {
        // Форматируем variables type в одну строку для использования в generic типе
        var variablesTypeStr = variablesType.Properties.Count > 0
            ? string.Join("; ", variablesType.Properties.Select(p => $"{p.Name}{(p.IsOptional ? "?" : "")}: {p.Type}"))
            : "";
        
        var properties = new List<OptionsProperty>
        {
            new()
            {
                Name = "mutation",
                Type = $"UseMutationOptions<{awaitedReturnTypeName}, TError, {{ {variablesTypeStr} }}, TContext>",
                IsOptional = true
            },
            new()
            {
                Name = "timeout",
                Type = "number",
                IsOptional = true
            }
        };
        
        if (options.HttpClientLibrary == HttpClientLibrary.Axios)
        {
            properties.Add(new OptionsProperty
            {
                Name = "axiosInstance",
                Type = "AxiosInstance",
                IsOptional = true
            });
        }
        
        return new OptionsDefinition
        {
            Properties = properties,
            IsOptional = true
        };
    }
}
