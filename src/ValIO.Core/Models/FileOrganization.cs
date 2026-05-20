namespace ValIO.Core.Models;

/// <summary>
/// Организация файлов при генерации
/// </summary>
public class FileOrganization
{
    public FileGroupingStrategy GroupingStrategy { get; set; } = FileGroupingStrategy.SingleFile;
    public bool SeparateModels { get; set; } = true;
    public bool SeparateClients { get; set; } = true;
    public string? ModelsPath { get; set; }
    public string? ClientsPath { get; set; }
}

