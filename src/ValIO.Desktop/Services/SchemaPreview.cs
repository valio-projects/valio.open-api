namespace ValIO.Desktop.Services;

/// <summary>
/// Lightweight summary of an OpenAPI document, computed once after schema selection.
/// Used by the GenerationView preview pane (and useful in error toasts).
/// </summary>
public sealed record SchemaPreview(
    string Title,
    string Version,
    int Paths,
    int Operations,
    int Components,
    long FileSizeBytes)
{
    public string FileSizeDisplay => FileSizeBytes switch
    {
        < 1024              => $"{FileSizeBytes} B",
        < 1024 * 1024       => $"{FileSizeBytes / 1024.0:F1} KB",
        _                   => $"{FileSizeBytes / 1024.0 / 1024.0:F1} MB",
    };
}
