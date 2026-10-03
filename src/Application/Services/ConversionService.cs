using System.Diagnostics;
using MarkItDownWeb.Application.DTOs;
using MarkItDownWeb.Application.Interfaces;
using MarkItDownWeb.Domain.Entities;

namespace MarkItDownWeb.Application.Services;

public class ConversionService
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".docx", ".pptx", ".xlsx", ".html", ".csv"
    };
    private const long MaxFileSizeBytes = 100 * 1024 * 1024; // 100 MB — local deployment

    private readonly IMarkItDownService _markItDownService;
    private readonly IFileStorageService _fileStorageService;
    private readonly IConversionRepository _repository;

    public ConversionService(
        IMarkItDownService markItDownService,
        IFileStorageService fileStorageService,
        IConversionRepository repository)
    {
        _markItDownService = markItDownService;
        _fileStorageService = fileStorageService;
        _repository = repository;
    }

    public async Task<ConversionResultDto> ConvertAsync(Stream fileStream, string fileName, long fileSize, CancellationToken ct = default)
    {
        ValidateFile(fileName, fileSize);

        var conversion = new Conversion
        {
            FileName = fileName,
            FileType = ResolveFileType(fileName),
            FileSizeBytes = fileSize,
            Status = ConversionStatus.Processing
        };

        var tempPath = await _fileStorageService.SaveTemporaryAsync(fileStream, fileName, ct);
        conversion.OriginalFilePath = tempPath;

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var markdown = await _markItDownService.ConvertFileAsync(tempPath, ct);
            stopwatch.Stop();

            var markdownPath = await _fileStorageService.SaveMarkdownAsync(conversion.Id, markdown, ct);
            conversion.MarkAsCompleted(markdownPath, stopwatch.Elapsed.TotalSeconds);
            conversion.Chunks.AddRange(MarkdownChunker.Create(conversion.Id, markdown, conversion.FileName));

            await _repository.AddAsync(conversion, ct);
            await _repository.SaveChangesAsync(ct);

            return new ConversionResultDto
            {
                ConversionId = conversion.Id,
                FileName = fileName,
                Markdown = markdown,
                ConversionTimeSeconds = stopwatch.Elapsed.TotalSeconds,
                Success = true
            };
        }
        catch (Exception ex)
        {
            conversion.MarkAsFailed(ex.Message);
            await _repository.AddAsync(conversion, ct);
            await _repository.SaveChangesAsync(ct);

            return new ConversionResultDto
            {
                ConversionId = conversion.Id,
                FileName = fileName,
                Success = false,
                ErrorMessage = "The file could not be converted."
            };
        }
        finally
        {
            _fileStorageService.DeleteTemporary(tempPath);
        }
    }

    public async Task<ConversionResultDto> GetHistoryItemAsync(Guid id, CancellationToken ct = default)
    {
        var conversion = await _repository.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException("Conversion not found.");

        var markdown = conversion.MarkdownFilePath is null
            ? string.Empty
            : await _fileStorageService.ReadMarkdownAsync(conversion.MarkdownFilePath, ct);

        return new ConversionResultDto
        {
            ConversionId = conversion.Id,
            FileName = conversion.FileName,
            Markdown = markdown,
            ConversionTimeSeconds = conversion.ConversionTimeSeconds ?? 0,
            Success = conversion.Status == ConversionStatus.Completed
        };
    }

    public Task<List<ConversionListItemDto>> GetHistoryAsync(string? search, DateTime? date, CancellationToken ct = default)
        => _repository.GetHistoryAsync(search, date, ct)
            .ContinueWith(t => t.Result.Select(c => new ConversionListItemDto
            {
                Id = c.Id,
                FileName = c.FileName,
                CreatedAt = c.CreatedAt,
                Status = c.Status.ToString()
            }).ToList(), ct);

    public async Task IndexExistingAsync(CancellationToken ct = default)
    {
        var conversions = await _repository.GetCompletedWithoutIndexAsync(ct);
        foreach (var conversion in conversions)
        {
            if (string.IsNullOrWhiteSpace(conversion.MarkdownFilePath))
                continue;

            try
            {
                var markdown = await _fileStorageService.ReadMarkdownAsync(conversion.MarkdownFilePath, ct);
                var chunks = MarkdownChunker.Create(conversion.Id, markdown, conversion.FileName);
                await _repository.AddChunksAsync(chunks, ct);
            }
            catch (FileNotFoundException)
            {
                // A missing Markdown file must not stop the local app from starting.
            }
        }

        await _repository.BackfillSearchIndexesAsync(ct);
        await _repository.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var conversion = await _repository.GetByIdAsync(id, ct);
        if (conversion is null)
            return;

        await _repository.DeleteAsync(id, ct);
        await _repository.SaveChangesAsync(ct);

        if (!string.IsNullOrWhiteSpace(conversion.MarkdownFilePath))
            _fileStorageService.DeleteMarkdown(conversion.MarkdownFilePath);
    }

    private static void ValidateFile(string fileName, long fileSize)
    {
        var extension = Path.GetExtension(fileName);
        if (!AllowedExtensions.Contains(extension))
            throw new InvalidOperationException($"Extension not allowed: {extension}");

        if (fileSize > MaxFileSizeBytes)
            throw new InvalidOperationException("File exceeds the 100 MB limit.");
    }

    private static FileType ResolveFileType(string fileName) => Path.GetExtension(fileName).ToLowerInvariant() switch
    {
        ".pdf" => FileType.Pdf,
        ".docx" => FileType.Docx,
        ".pptx" => FileType.Pptx,
        ".xlsx" => FileType.Xlsx,
        ".html" => FileType.Html,
        ".csv" => FileType.Csv,
        _ => throw new InvalidOperationException("Unsupported file type.")
    };
}
