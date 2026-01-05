using ValIO.Core.Models.TemplateData;

namespace ValIO.Generators.TypeScript.Models.CodeGeneration;

/// <summary>
/// Builder для создания FunctionParameter из ParameterTemplateData
/// </summary>
public static class FunctionParameterBuilder
{
    /// <summary>
    /// Создает FunctionParameter из ParameterTemplateData
    /// </summary>
    public static FunctionParameter FromParameter(ParameterTemplateData parameter)
    {
        return new FunctionParameter
        {
            Name = parameter.VariableName,
            Type = parameter.TypeInfo.FullTypeName,
            IsOptional = !parameter.IsRequired,
            Description = parameter.Description
        };
    }
    
    /// <summary>
    /// Создает список FunctionParameter из списка ParameterTemplateData
    /// </summary>
    public static IReadOnlyList<FunctionParameter> FromParameters(IReadOnlyList<ParameterTemplateData> parameters)
    {
        return parameters.Select(FromParameter).ToList();
    }
    
    /// <summary>
    /// Создает FunctionParameter для query parameters model
    /// </summary>
    public static FunctionParameter FromQueryParametersModel(
        string modelTypeName, 
        IReadOnlyList<ParameterTemplateData> queryParameters)
    {
        var hasRequiredParam = queryParameters.Any(p => p.IsRequired);
        return new FunctionParameter
        {
            Name = "params",
            Type = modelTypeName,
            IsOptional = !hasRequiredParam
        };
    }
    
    /// <summary>
    /// Создает FunctionParameter для FormData property
    /// </summary>
    public static FunctionParameter FromFormDataProperty(FormDataPropertyTemplateData formProp)
    {
        return new FunctionParameter
        {
            Name = formProp.VariableName,
            Type = formProp.TypeInfo.FullTypeName,
            IsOptional = !formProp.IsRequired,
            Description = formProp.Description
        };
    }
    
    /// <summary>
    /// Создает список FunctionParameter из FormData properties
    /// </summary>
    public static IReadOnlyList<FunctionParameter> FromFormDataProperties(
        IReadOnlyList<FormDataPropertyTemplateData> formProperties)
    {
        return formProperties.Select(FromFormDataProperty).ToList();
    }
    
    /// <summary>
    /// Создает FunctionParameter для RequestBody
    /// </summary>
    public static FunctionParameter FromRequestBody(
        string parameterName,
        string? typeName,
        bool isNullable)
    {
        return new FunctionParameter
        {
            Name = parameterName,
            Type = typeName ?? "any",
            IsOptional = isNullable
        };
    }
}
