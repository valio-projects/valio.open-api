using ValIO.Core.Interfaces;

namespace ValIO.Generators.TypeScript.Services;

/// <summary>
/// Стратегия именования для операций TypeScript
/// </summary>
public class TypeScriptOperationNamingStrategy : IOperationNamingStrategy
{
    public string ToVariableName(string parameterName, INamingConverter namingConverter)
    {
        return namingConverter.ToCamelCase(parameterName);
    }

    public string ToPropertyName(string parameterName, INamingConverter namingConverter)
    {
        return namingConverter.ToPascalCase(parameterName);
    }

    public string GetDefaultParameterType()
    {
        return "string";
    }

    public string GetDefaultRequestBodyType()
    {
        return "any";
    }
}
