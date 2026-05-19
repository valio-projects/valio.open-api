namespace ValIO.Generators.Python.Options;

/// <summary>
/// Нормализованные идентификаторы для опций Python-плагина.
/// </summary>
public static class PythonOptionIds
{
    public static class HttpxMode
    {
        public const string Async = "async";
        public const string Sync = "sync";
    }

    public static class Validator
    {
        public const string Pydantic = "pydantic";
        public const string Msgspec = "msgspec"; // зарезервирован для будущего
    }
}
