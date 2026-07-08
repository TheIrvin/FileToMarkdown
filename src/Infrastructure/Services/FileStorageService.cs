using MarkItDownWeb.Application.Interfaces;
using Microsoft.Extensions.Configuration;

namespace MarkItDownWeb.Infrastructure.Services;

public class FileStorageService : IFileStorageService
{
    private readonly string _uploadsPath;
    private readonly string _markdownPath;

    public FileStorageService(IConfiguration configuration)
    {
        _uploadsPath = configuration["Storage:UploadsFolder"] ?? "Uploads";
        _markdownPath = configuration["Storage:MarkdownFolder"] ?? "Storage/Markdown";
        Directory.CreateDirectory(_uploadsPath);
        Directory.CreateDirectory(_markdownPath);
    }

    public async Task<string> SaveTemporaryAsync(Stream fileStream, string fileName, CancellationToken ct = default)
    {
        var safeName = $"{Guid.NewGuid()}{Path.GetExtension(fileName)}";
        var fullPath = Path.Combine(_uploadsPath, safeName);

        await using var output = File.Create(fullPath);
        await fileStream.CopyToAsync(output, ct);

        return fullPath;
    }

    public async Task<string> SaveMarkdownAsync(Guid conversionId, string markdown, CancellationToken ct = default)
    {
        var fullPath = Path.Combine(_markdownPath, $"{conversionId}.md");
        await File.WriteAllTextAsync(fullPath, markdown, ct);
        return fullPath;
    }

    public Task<string> ReadMarkdownAsync(string markdownFilePath, CancellationToken ct = default)
        => File.ReadAllTextAsync(markdownFilePath, ct);

    public void DeleteTemporary(string filePath)
    {
        if (File.Exists(filePath))
            File.Delete(filePath);
    }
}
