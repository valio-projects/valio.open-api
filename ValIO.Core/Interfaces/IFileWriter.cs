namespace ValIO.Core.Interfaces;

/// <summary>
/// Интерфейс для записи сгенерированных файлов
/// </summary>
public interface IFileWriter
{
    Task WriteFileAsync(string path, string content);
    Task WriteFilesAsync(IReadOnlyDictionary<string, string> files);
    void EnsureDirectoryExists(string directoryPath);
}

