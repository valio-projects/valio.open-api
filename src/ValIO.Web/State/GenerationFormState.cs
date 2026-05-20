using System.ComponentModel;
using System.Runtime.CompilerServices;
using ValIO.Core.Models;

namespace ValIO.Web.State;

/// <summary>
/// Scoped per-circuit form state. The Generate page binds to this; the endpoint
/// reads its snapshot via <see cref="BuildRequest"/>.
/// </summary>
public sealed class GenerationFormState : INotifyPropertyChanged
{
    // Schema input
    private SchemaSourceKind _schemaKind = SchemaSourceKind.Url;
    private string? _schemaUrl;
    private byte[]? _uploadedSchemaBytes;
    private string? _uploadedSchemaName;

    // Language / plugin options
    private string? _languageId;
    private readonly Dictionary<string, object?> _pluginOptions = new(StringComparer.Ordinal);

    // Generation flags
    private bool _generateModels = true;
    private bool _generateClients = true;
    private bool _generateEnums = true;
    private string? _namespaceName;

    // Output format (UI only — endpoint streams ZipStream always)
    private OutputFormatKind _output = OutputFormatKind.Zip;

    public SchemaSourceKind SchemaKind { get => _schemaKind; set => Set(ref _schemaKind, value); }
    public string? SchemaUrl { get => _schemaUrl; set => Set(ref _schemaUrl, value); }
    public byte[]? UploadedSchemaBytes { get => _uploadedSchemaBytes; set => Set(ref _uploadedSchemaBytes, value); }
    public string? UploadedSchemaName { get => _uploadedSchemaName; set => Set(ref _uploadedSchemaName, value); }
    public string? LanguageId { get => _languageId; set => Set(ref _languageId, value); }
    public bool GenerateModels { get => _generateModels; set => Set(ref _generateModels, value); }
    public bool GenerateClients { get => _generateClients; set => Set(ref _generateClients, value); }
    public bool GenerateEnums { get => _generateEnums; set => Set(ref _generateEnums, value); }
    public string? NamespaceName { get => _namespaceName; set => Set(ref _namespaceName, value); }
    public OutputFormatKind Output { get => _output; set => Set(ref _output, value); }

    public IDictionary<string, object?> PluginOptions => _pluginOptions;

    public void ResetPluginOptions() => _pluginOptions.Clear();

    public bool HasSchema => SchemaKind == SchemaSourceKind.Url
        ? !string.IsNullOrWhiteSpace(SchemaUrl)
        : UploadedSchemaBytes is { Length: > 0 };

    public bool IsReady =>
        !string.IsNullOrWhiteSpace(LanguageId) && HasSchema;

    public GenerationConfiguration ToConfiguration() => new()
    {
        LanguageId = LanguageId ?? "",
        OutputPath = "/",
        Namespace = NamespaceName,
        GenerateModels = GenerateModels,
        GenerateClients = GenerateClients,
        GenerateEnums = GenerateEnums,
    };

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

public enum SchemaSourceKind { Url, Upload }
public enum OutputFormatKind { Zip, Folder }
