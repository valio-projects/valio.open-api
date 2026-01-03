using ValIO.Core.Models;
using ValIO.Core.Models.TemplateData;
using ValIO.Core.Models.TemplateData.Options;

namespace ValIO.Core.Interfaces;

/// <summary>
/// Базовый интерфейс для данных шаблона клиента (без generic параметров для renderers)
/// </summary>
public interface IClientTemplateData
{
    string Name { get; set; }
    IReadOnlyList<IOperationTemplateData> Operations { get; }
    GenerationLanguage Language { get; set; }
    HttpClientLibrary HttpClientLibrary { get; set; }
    IReadOnlyList<string> Dependencies { get; set; }
    string? Namespace { get; set; }
    string? ModelsPath { get; set; }
}

/// <summary>
/// Базовый интерфейс для данных шаблона операции (без generic параметров для renderers)
/// </summary>
public interface IOperationTemplateData
{
    string MethodName { get; set; }
    string HttpMethod { get; set; }
    string Path { get; set; }
    string? Summary { get; set; }
    string? Description { get; set; }
    TypeInfo ReturnTypeInfo { get; set; }
    bool HasRequestBody { get; set; }
    string RequestBodyParameter { get; set; }
    TypeInfo? RequestBodyTypeInfo { get; set; }
    string? RequestBodyContentType { get; set; }
    bool HasResponseType { get; set; }
    IReadOnlyList<ParameterTemplateData> PathParameters { get; set; }
    IReadOnlyList<ParameterTemplateData> QueryParameters { get; set; }
    IReadOnlyList<ParameterTemplateData> HeaderParameters { get; set; }
    bool IsFormData { get; set; }
    IReadOnlyList<FormDataPropertyTemplateData>? FormDataProperties { get; set; }
    IReadOnlyList<ResponseTemplateData> Responses { get; set; }
    GenerationLanguage Language { get; set; }
    IReadOnlyList<string> Dependencies { get; set; }
    bool UseQueryParametersModel { get; set; }
    string? QueryParametersModelTypeName { get; set; }
    string? QueryParametersModelImportPath { get; set; }
    string? RequestSchemaName { get; set; }
    string? RequestSchemaImportPath { get; set; }
    string? ResponseSchemaName { get; set; }
    string? ResponseSchemaImportPath { get; set; }
    IReadOnlyList<string>? Tags { get; set; }
}
