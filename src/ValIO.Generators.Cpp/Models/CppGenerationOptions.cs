namespace ValIO.Generators.Cpp.Models;

/// <summary>
/// Настройки генерации C++ кода для Unreal Engine.
/// </summary>
public sealed class CppGenerationOptions
{
    /// <summary>
    /// Макрос API модуля, вставляемый в UCLASS (например WIREFRAME_API, MYPROJECT_API).
    /// </summary>
    public string ModuleApiMacro { get; set; } = "MODULE_API";

    /// <summary>
    /// Суффикс для generated header (UHT требует .generated.h)
    /// </summary>
    public string GeneratedHeaderSuffix { get; set; } = ".generated.h";

    /// <summary>
    /// Категория по умолчанию для UPROPERTY/UFUNCTION в Blueprint Editor
    /// </summary>
    public string BlueprintCategory { get; set; } = "ValIO";

    /// <summary>
    /// Директория для вывода сгенерированных файлов
    /// </summary>
    public string OutputDirectory { get; set; } = "Generated";

    /// <summary>
    /// Текст copyright в заголовке файлов (null = без copyright)
    /// </summary>
    public string? Copyright { get; set; }

    /// <summary>
    /// Имя базового класса для async actions (наследник UBlueprintAsyncActionBase)
    /// </summary>
    public string BaseAsyncActionClassName { get; set; } = "UValIOAsyncActionBase";

    /// <summary>
    /// Include path для базового async action класса
    /// </summary>
    public string BaseAsyncActionIncludePath { get; set; } = "Blueprint/ValIOTypedAsyncActionBase.h";

    /// <summary>
    /// Include path для ValIO client
    /// </summary>
    public string ValIOClientIncludePath { get; set; } = "Client/ValIOClient.h";

    /// <summary>
    /// Include path для ValIO request builder
    /// </summary>
    public string ValIORequestBuilderIncludePath { get; set; } = "Request/ValIORequestBuilder.h";

    /// <summary>
    /// Include path для ValIO multipart form data
    /// </summary>
    public string ValIOMultipartFormDataIncludePath { get; set; } = "Request/ValIOMultipartFormData.h";

    /// <summary>
    /// Include path для ValIO types
    /// </summary>
    public string ValIOTypesIncludePath { get; set; } = "ValIOTypes.h";

    /// <summary>
    /// Include path для ValIO response (FValIOResponse)
    /// </summary>
    public string ValIOResponseIncludePath { get; set; } = "Response/ValIOResponse.h";

    /// <summary>
    /// Добавлять UE-инклюды (CoreMinimal, Async/Future, ValIOResponse) в headers.
    /// Если true — добавляются в endpoint headers; CoreMinimal — в enums, models, params, result.
    /// Если false — не добавляются (предполагается PCH или явные инклюды в проекте).
    /// </summary>
    public bool AddUnrealEngineIncludesInHeaders { get; set; } = true;
}
