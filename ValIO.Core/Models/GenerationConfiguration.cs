namespace ValIO.Core.Models;

/// <summary>
/// Конфигурация генерации кода
/// </summary>
public class GenerationConfiguration
{
    public string LanguageId { get; set; } = string.Empty;
    public object? PluginOptions { get; set; }
    public FileOrganization FileOrganization { get; set; } = new();
    public NamingConventions NamingConvention { get; set; } = new();
    public string OutputPath { get; set; } = string.Empty;
    public string? Namespace { get; set; }
    public bool GenerateEnums { get; set; } = true;
    public bool GenerateModels { get; set; } = true;
    public bool GenerateClients { get; set; } = true;
}

