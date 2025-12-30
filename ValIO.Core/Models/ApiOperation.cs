namespace ValIO.Core.Models;

/// <summary>
/// Операция API
/// </summary>
public class ApiOperation
{
    public string OperationId { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public OperationType HttpMethod { get; set; }
    public string? Summary { get; set; }
    public string? Description { get; set; }
    public IReadOnlyList<string> Tags { get; set; } = Array.Empty<string>();
    public IReadOnlyList<ApiParameter> Parameters { get; set; } = Array.Empty<ApiParameter>();
    public ApiRequestBody? RequestBody { get; set; }
    public IReadOnlyDictionary<string, ApiResponse> Responses { get; set; } = new Dictionary<string, ApiResponse>();
}

