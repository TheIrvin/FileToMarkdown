namespace MarkItDownWeb.Application.Interfaces;

public interface IMarkItDownService
{
    Task<string> ConvertFileAsync(string absoluteFilePath, CancellationToken ct = default);
}
