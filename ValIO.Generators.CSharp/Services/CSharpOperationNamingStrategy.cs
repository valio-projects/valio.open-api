using ValIO.Core.Interfaces;

namespace ValIO.Generators.CSharp.Services;

/// <summary>
/// Стратегия именования для операций C#
/// </summary>
public class CSharpOperationNamingStrategy : IOperationNamingStrategy
{
    public string ToVariableName(string parameterName, INamingConverter namingConverter)
    {
        return namingConverter.ToPascalCase(parameterName);
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
        return "object";
    }
}
