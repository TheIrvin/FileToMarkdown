namespace MarkItDownWeb.Application.Interfaces;

public interface IFileStorageService
{
    Task<string> SaveTemporaryAsync(Stream fileStream, string fileName, CancellationToken ct = default);
    Task<string> SaveMarkdownAsync(Guid conversionId, string markdown, CancellationToken ct = default);
    Task<string> ReadMarkdownAsync(string markdownFilePath, CancellationToken ct = default);
    void DeleteTemporary(string filePath);
}
