using ValIO.Core.Models.TemplateData;

namespace ValIO.Generators.TypeScript.Models.CodeGeneration;

/// <summary>
/// Builder для создания TypeDefinition
/// </summary>
public static class TypeDefinitionBuilder
{
    /// <summary>
    /// Создает TypeDefinition из ParameterTemplateData
    /// </summary>
    public static TypeDefinition FromParameter(ParameterTemplateData parameter)
    {
        return new TypeDefinition
        {
            Name = parameter.VariableName,
            Type = parameter.TypeInfo.FullTypeName,
            IsOptional = !parameter.IsRequired,
            Description = parameter.Description
        };
    }
    
    /// <summary>
    /// Создает TypeObjectDefinition для mutation variables из OperationTemplateData
    /// </summary>
    public static TypeObjectDefinition BuildMutationVariablesType(OperationTemplateData operation)
    {
        var properties = new List<TypeDefinition>();
        
        // Path параметры
        foreach (var param in operation.PathParameters)
        {
            properties.Add(FromParameter(param));
        }
        
        // Query параметры
        if (operation.UseQueryParametersModel && !string.IsNullOrEmpty(operation.QueryParametersModelTypeName))
        {
            var hasRequiredParam = operation.QueryParameters.Any(p => p.IsRequired);
            properties.Add(new TypeDefinition
            {
                Name = "params",
                Type = operation.QueryParametersModelTypeName,
                IsOptional = !hasRequiredParam
            });
        }
        else
        {
            foreach (var param in operation.QueryParameters)
            {
                properties.Add(FromParameter(param));
            }
        }
        
        // Header параметры
        foreach (var param in operation.HeaderParameters)
        {
            properties.Add(FromParameter(param));
        }
        
        // Request body
        // Для FormData генерируем отдельные свойства, а не объект request
        if (operation.IsFormData && operation.FormDataProperties != null && operation.FormDataProperties.Count > 0)
        {
            foreach (var formProp in operation.FormDataProperties)
            {
                properties.Add(new TypeDefinition
                {
                    Name = formProp.VariableName,
                    Type = formProp.TypeInfo.FullTypeName,
                    IsOptional = !formProp.IsRequired,
                    Description = formProp.Description
                });
            }
        }
        else if (operation.HasRequestBody && !string.IsNullOrEmpty(operation.RequestBodyParameter))
        {
            var optional = operation.RequestBodyTypeInfo != null && !operation.RequestBodyTypeInfo.IsNullable ? "" : "?";
            var typeStr = operation.RequestBodyTypeInfo?.FullTypeName ?? "any";
            properties.Add(new TypeDefinition
            {
                Name = operation.RequestBodyParameter,
                Type = typeStr,
                IsOptional = operation.RequestBodyTypeInfo == null || operation.RequestBodyTypeInfo.IsNullable
            });
        }
        
        return new TypeObjectDefinition
        {
            Properties = properties
        };
    }
}
