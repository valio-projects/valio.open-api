namespace ValIO.Core.Interfaces;

/// <summary>
/// Стратегия именования для операций (параметры, переменные и т.д.)
/// </summary>
public interface IOperationNamingStrategy
{
    /// <summary>
    /// Преобразует имя параметра в имя переменной
    /// </summary>
    string ToVariableName(string parameterName, INamingConverter namingConverter);

    /// <summary>
    /// Преобразует имя параметра в имя свойства
    /// </summary>
    string ToPropertyName(string parameterName, INamingConverter namingConverter);

    /// <summary>
    /// Получает тип по умолчанию для параметра без схемы
    /// </summary>
    string GetDefaultParameterType();

    /// <summary>
    /// Получает тип по умолчанию для request body без схемы
    /// </summary>
    string GetDefaultRequestBodyType();
}
