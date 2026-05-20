namespace ValIO.Desktop.Services;

/// <summary>
/// Parses the OpenAPI schema at <paramref name="path"/> just enough to surface
/// title / version / counts in the UI. Errors are reported as null preview;
/// the caller decides whether to show a snackbar.
/// </summary>
public interface ISchemaPreviewer
{
    Task<SchemaPreview?> PreviewAsync(string path, CancellationToken ct = default);
}
