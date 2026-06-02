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

    /// <summary>
    /// Output folder for generated validation schemas (model/enum schemas + the shared
    /// formats constant). Defaults to <c>"schemas"</c> at the output root (sibling of
    /// <see cref="ModelsPath"/> / <see cref="ClientsPath"/>).
    /// </summary>
    public string? SchemasPath { get; set; }
}

