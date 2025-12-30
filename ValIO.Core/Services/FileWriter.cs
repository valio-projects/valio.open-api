using System.Text;
using ValIO.Core.Interfaces;

namespace ValIO.Core.Services;

/// <summary>
/// Реализация записи файлов
/// </summary>
public class FileWriter : IFileWriter
{
    public async Task WriteFileAsync(string path, string content)
    {
        EnsureDirectoryExists(Path.GetDirectoryName(path) ?? string.Empty);
        
        await File.WriteAllTextAsync(path, content, Encoding.UTF8);
    }

    public async Task WriteFilesAsync(IReadOnlyDictionary<string, string> files)
    {
        foreach (var file in files)
        {
            await WriteFileAsync(file.Key, file.Value);
        }
    }

    public void EnsureDirectoryExists(string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
            return;

        if (!Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }
    }
}

