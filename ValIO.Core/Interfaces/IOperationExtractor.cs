using System.Net.Http;
using Microsoft.OpenApi;
using ValIO.Core.Models.TemplateData;

namespace ValIO.Core.Interfaces;

/// <summary>
/// Интерфейс для извлечения операций из OpenAPI схемы
/// </summary>
public interface IOperationExtractor
{
    /// <summary>
    /// Извлекает операции в виде TemplateData с уже подготовленными данными и зависимостями
    /// </summary>
    IReadOnlyList<OperationTemplateData> ExtractOperationTemplateData(OpenApiDocument document, ITypeMapper typeMapper, INamingConverter namingConverter, ITypeInfoBuilder typeInfoBuilder, IDependencyAnalyzer dependencyAnalyzer, IOperationNamingStrategy namingStrategy);
    
    /// <summary>
    /// Извлекает операцию в виде TemplateData
    /// </summary>
    OperationTemplateData ExtractOperationTemplateData(IOpenApiPathItem pathItem, HttpMethod operationType, string path, ITypeMapper typeMapper, INamingConverter namingConverter, ITypeInfoBuilder typeInfoBuilder, IDependencyAnalyzer dependencyAnalyzer, IOperationNamingStrategy namingStrategy);
}

