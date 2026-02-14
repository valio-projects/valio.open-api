namespace ValIO.Generators.Cpp.Models;

/// <summary>
/// Настройки генерации C++ кода для Unreal Engine.
/// </summary>
public sealed class CppGenerationOptions
{
    /// <summary>
    /// Макрос API модуля, вставляемый в UCLASS (например WIREFRAME_API, MYPROJECT_API).
    /// </summary>
    public string ModuleApiMacro { get; init; } = "MODULE_API";

    /// <summary>
    /// Суффикс для generated header (UHT требует .generated.h)
    /// </summary>
    public string GeneratedHeaderSuffix { get; init; } = ".generated.h";

    /// <summary>
    /// Категория по умолчанию для UPROPERTY/UFUNCTION в Blueprint Editor
    /// </summary>
    public string BlueprintCategory { get; init; } = "ValIO";

    /// <summary>
    /// Директория для вывода сгенерированных файлов
    /// </summary>
    public string OutputDirectory { get; init; } = "Generated";

    /// <summary>
    /// Текст copyright в заголовке файлов (null = без copyright)
    /// </summary>
    public string? Copyright { get; init; }

    /// <summary>
    /// Имя базового класса для async actions (наследник UBlueprintAsyncActionBase)
    /// </summary>
    public string BaseAsyncActionClassName { get; init; } = "UValIOAsyncActionBase";

    /// <summary>
    /// Include path для базового async action класса
    /// </summary>
    public string BaseAsyncActionIncludePath { get; init; } = "Blueprint/ValIOTypedAsyncActionBase.h";

    /// <summary>
    /// Include path для ValIO client
    /// </summary>
    public string ValIOClientIncludePath { get; init; } = "Client/ValIOClient.h";

    /// <summary>
    /// Include path для ValIO request builder
    /// </summary>
    public string ValIORequestBuilderIncludePath { get; init; } = "Request/ValIORequestBuilder.h";

    /// <summary>
    /// Include path для ValIO multipart form data
    /// </summary>
    public string ValIOMultipartFormDataIncludePath { get; init; } = "Request/ValIOMultipartFormData.h";

    /// <summary>
    /// Include path для ValIO types
    /// </summary>
    public string ValIOTypesIncludePath { get; init; } = "ValIOTypes.h";

    /// <summary>
    /// Include path для ValIO response
    /// </summary>
    public string ValIOResponseIncludePath { get; init; } = "Response/ValIOResponse.h";
}
