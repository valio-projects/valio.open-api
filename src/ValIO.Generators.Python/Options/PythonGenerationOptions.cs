namespace ValIO.Generators.Python.Options;

/// <summary>
/// Опции генерации Python кода.
/// </summary>
public sealed class PythonGenerationOptions
{
    /// <summary>Целевая версия Python (3.11+).</summary>
    public string PythonVersion { get; set; } = "3.11";

    /// <summary>Режим HTTPX клиента: "async" (default) или "sync".</summary>
    public string HttpxMode { get; set; } = PythonOptionIds.HttpxMode.Async;

    /// <summary>Валидатор моделей: "pydantic" (default).</summary>
    public string Validator { get; set; } = PythonOptionIds.Validator.Pydantic;

    /// <summary>Корневой Python-модуль для пакета generated кода.</summary>
    public string RootModule { get; set; } = "generated";
}
