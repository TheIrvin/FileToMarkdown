using MarkItDownWeb.Application.Interfaces;
using MarkItDownWeb.Application.Services;
using MarkItDownWeb.Domain.Entities;
using MarkItDownWeb.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Text;
using Xunit;

namespace MarkItDownWeb.Tests;

public class ConversionServiceTests
{
    [Fact]
    public async Task ConvertAsync_saves_markdown_and_exposes_it_in_history()
    {
        await using var database = await TestDatabase.CreateAsync();
        var storage = new FakeFileStorageService();
        var converter = new FakeMarkItDownService(_ => Task.FromResult("# Monthly report"));
        var service = new ConversionService(converter, storage, database.Repository);

        await using var input = new MemoryStream([0x61, 0x2C, 0x62]);
        var result = await service.ConvertAsync(input, "report.csv", input.Length);

        Assert.True(result.Success);
        Assert.Equal("# Monthly report", result.Markdown);
        Assert.Equal(1, converter.CallCount);
        Assert.Empty(storage.TemporaryFiles);

        var history = await service.GetHistoryAsync("report", null);
        var historyItem = Assert.Single(history);
        Assert.Equal(result.ConversionId, historyItem.Id);
        Assert.Equal("Completed", historyItem.Status);

        var reopened = await service.GetHistoryItemAsync(result.ConversionId);
        Assert.Equal("# Monthly report", reopened.Markdown);
    }

    [Theory]
    [InlineData("report.exe", 10L)]
    [InlineData("report.csv", 104_857_601L)]
    public async Task ConvertAsync_rejects_unsupported_or_oversized_files_before_saving(
        string fileName,
        long fileSize)
    {
        await using var database = await TestDatabase.CreateAsync();
        var storage = new FakeFileStorageService();
        var converter = new FakeMarkItDownService(_ => Task.FromResult("unused"));
        var service = new ConversionService(converter, storage, database.Repository);

        await using var input = new MemoryStream([0x61]);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ConvertAsync(input, fileName, fileSize));

        Assert.Equal(0, converter.CallCount);
        Assert.Empty(storage.TemporaryFiles);
        Assert.Empty(await service.GetHistoryAsync(null, null));
    }

    [Fact]
    public async Task ConvertAsync_records_failed_conversion_and_removes_temporary_file()
    {
        await using var database = await TestDatabase.CreateAsync();
        var storage = new FakeFileStorageService();
        var converter = new FakeMarkItDownService(
            _ => Task.FromException<string>(new InvalidOperationException("conversion failed")));
        var service = new ConversionService(converter, storage, database.Repository);

        await using var input = new MemoryStream([0x61]);
        var result = await service.ConvertAsync(input, "report.html", input.Length);

        Assert.False(result.Success);
        Assert.Equal("The file could not be converted.", result.ErrorMessage);
        Assert.Empty(storage.TemporaryFiles);

        var history = await service.GetHistoryAsync(null, null);
        var historyItem = Assert.Single(history);
        Assert.Equal("Failed", historyItem.Status);
    }

    [Fact]
    public async Task Search_returns_matching_excerpt_with_its_section_and_delete_removes_the_index()
    {
        await using var database = await TestDatabase.CreateAsync();
        var storage = new FakeFileStorageService();
        var converter = new FakeMarkItDownService(_ => Task.FromResult(
            "# Budget\n\nThe annual maintenance cost is 420 dollars.\n\n## Schedule\n\nRenew the plan in October."));
        var conversions = new ConversionService(converter, storage, database.Repository);

        await using var input = new MemoryStream([0x61, 0x2C, 0x62]);
        var conversion = await conversions.ConvertAsync(input, "budget.csv", input.Length);
        var library = new LibraryService(database.Repository, new LocalExtractiveAnswerGenerator());

        var answer = await library.AskAsync("What is the annual maintenance cost?");

        Assert.True(answer.HasEvidence);
        var citation = Assert.Single(answer.Citations);
        Assert.Equal("budget.csv", citation.FileName);
        Assert.Equal("Budget", citation.Section);
        Assert.Contains("420 dollars", citation.Content);

        await conversions.DeleteAsync(conversion.ConversionId);

        Assert.Empty(await library.SearchAsync("maintenance"));
        Assert.Empty(storage.MarkdownFiles);
    }

    [Fact]
    public async Task Ask_returns_clear_abstention_when_no_document_matches()
    {
        await using var database = await TestDatabase.CreateAsync();
        var library = new LibraryService(database.Repository, new LocalExtractiveAnswerGenerator());

        var answer = await library.AskAsync("What is the launch date?");

        Assert.False(answer.HasEvidence);
        Assert.Empty(answer.Citations);
        Assert.Contains("No encontré", answer.Answer);
    }

    [Fact]
    public async Task Search_uses_page_number_when_markdown_preserves_page_markers()
    {
        await using var database = await TestDatabase.CreateAsync();
        var storage = new FakeFileStorageService();
        var converter = new FakeMarkItDownService(_ => Task.FromResult(
            "<!-- PageNumber=\"4\" -->\nThe risk review is scheduled for next week."));
        var conversions = new ConversionService(converter, storage, database.Repository);
        await using var input = new MemoryStream([0x61, 0x2C, 0x62]);
        await conversions.ConvertAsync(input, "review.csv", input.Length);

        var library = new LibraryService(database.Repository, new LocalExtractiveAnswerGenerator());
        var hits = await library.SearchAsync("risk");

        Assert.Equal("Página 4", Assert.Single(hits).Section);
    }

    [Fact]
    public async Task Search_ignores_case_and_diacritics_in_document_content()
    {
        await using var database = await TestDatabase.CreateAsync();
        var storage = new FakeFileStorageService();
        var converter = new FakeMarkItDownService(_ => Task.FromResult("La CANCIÓN está en el CAFÉ."));
        var conversions = new ConversionService(converter, storage, database.Repository);
        await using var input = new MemoryStream([0x61, 0x2C, 0x62]);
        await conversions.ConvertAsync(input, "música.csv", input.Length);

        var library = new LibraryService(database.Repository, new LocalExtractiveAnswerGenerator());

        Assert.Single(await library.SearchAsync("canción"));
        Assert.Single(await library.SearchAsync("cafe"));
        Assert.Single(await library.SearchAsync("canción".Normalize(NormalizationForm.FormD)));
    }

    [Fact]
    public async Task Search_does_not_drop_whole_word_hits_behind_prefix_matches()
    {
        await using var database = await TestDatabase.CreateAsync();
        var exact = new Conversion
        {
            FileName = "budget.csv",
            FileType = FileType.Csv,
            Status = ConversionStatus.Completed,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            Chunks =
            [
                new DocumentChunk
                {
                    Section = "Contenido",
                    Content = "The annual budget is approved.",
                    SearchIndex = " the annual budget is approved "
                }
            ]
        };
        await database.Repository.AddAsync(exact);

        for (var index = 0; index < 80; index++)
        {
            var content = $"The budgetary note {index} was updated.";
            var similar = new Conversion
            {
                FileName = $"budgetary-{index}.csv",
                FileType = FileType.Csv,
                Status = ConversionStatus.Completed,
                CreatedAt = DateTime.UtcNow.AddMinutes(index),
                Chunks =
                [
                    new DocumentChunk
                    {
                        Section = "Contenido",
                        Content = content,
                        SearchIndex = SearchTextNormalizer.CreateIndex(content)
                    }
                ]
            };
            await database.Repository.AddAsync(similar);
        }

        await database.Repository.SaveChangesAsync();
        var library = new LibraryService(database.Repository, new LocalExtractiveAnswerGenerator());

        var hit = Assert.Single(await library.SearchAsync("budget"));

        Assert.Equal("budget.csv", hit.FileName);
        Assert.Contains("annual budget", hit.Content);
    }

    [Fact]
    public async Task Search_ranks_older_chunks_that_match_more_query_terms()
    {
        await using var database = await TestDatabase.CreateAsync();
        var completeMatch = new Conversion
        {
            FileName = "archive.csv",
            FileType = FileType.Csv,
            Status = ConversionStatus.Completed,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            Chunks =
            [
                new DocumentChunk
                {
                    Section = "Contenido",
                    Content = "Apollo migration completed in 2026.",
                    SearchIndex = SearchTextNormalizer.CreateIndex("Apollo migration completed in 2026.")
                }
            ]
        };
        await database.Repository.AddAsync(completeMatch);

        var terms = new[] { "apollo", "migration", "2026" };
        for (var termIndex = 0; termIndex < terms.Length; termIndex++)
        {
            for (var itemIndex = 0; itemIndex < 80; itemIndex++)
            {
                var content = $"{terms[termIndex]} note {itemIndex}";
                var conversion = new Conversion
                {
                    FileName = $"note-{termIndex}-{itemIndex}.csv",
                    FileType = FileType.Csv,
                    Status = ConversionStatus.Completed,
                    CreatedAt = DateTime.UtcNow.AddMinutes(termIndex * 80 + itemIndex),
                    Chunks =
                    [
                        new DocumentChunk
                        {
                            Section = "Contenido",
                            Content = content,
                            SearchIndex = SearchTextNormalizer.CreateIndex(content)
                        }
                    ]
                };
                await database.Repository.AddAsync(conversion);
            }
        }

        await database.Repository.SaveChangesAsync();
        var library = new LibraryService(database.Repository, new LocalExtractiveAnswerGenerator());

        var first = (await library.SearchAsync("Apollo migration 2026")).First();

        Assert.Equal("archive.csv", first.FileName);
        Assert.Equal(3, first.MatchCount);
    }

    [Fact]
    public async Task Startup_indexing_adds_searchable_chunks_to_existing_history()
    {
        await using var database = await TestDatabase.CreateAsync();
        var storage = new FakeFileStorageService();
        storage.SeedMarkdown("Markdown/legacy.md", "# Legacy report\n\nRetain this historical content.");
        var conversion = new Conversion
        {
            FileName = "legacy.csv",
            FileType = FileType.Csv,
            MarkdownFilePath = "Markdown/legacy.md",
            FileSizeBytes = 42,
            Status = ConversionStatus.Completed
        };
        await database.Repository.AddAsync(conversion);
        await database.Repository.SaveChangesAsync();

        var service = new ConversionService(
            new FakeMarkItDownService(_ => Task.FromResult("unused")), storage, database.Repository);
        database.ClearTracking();
        await service.IndexExistingAsync();
        var library = new LibraryService(database.Repository, new LocalExtractiveAnswerGenerator());

        var hit = Assert.Single(await library.SearchAsync("historical"));

        Assert.Equal(conversion.Id, hit.ConversionId);
        Assert.Equal("Legacy report", hit.Section);
        Assert.Contains("Retain this historical content", hit.Content);
    }

    private sealed class FakeMarkItDownService(Func<string, Task<string>> convert) : IMarkItDownService
    {
        public int CallCount { get; private set; }

        public Task<string> ConvertFileAsync(string absoluteFilePath, CancellationToken ct = default)
        {
            CallCount++;
            return convert(absoluteFilePath);
        }
    }

    private sealed class FakeFileStorageService : IFileStorageService
    {
        private readonly Dictionary<string, string> _markdownFiles = new();

        public HashSet<string> TemporaryFiles { get; } = new();
        public Dictionary<string, string> MarkdownFiles => _markdownFiles;

        public void SeedMarkdown(string path, string content) => _markdownFiles[path] = content;

        public async Task<string> SaveTemporaryAsync(
            Stream fileStream,
            string fileName,
            CancellationToken ct = default)
        {
            await fileStream.CopyToAsync(Stream.Null, ct);
            var path = Path.Combine("Uploads", $"{Guid.NewGuid()}{Path.GetExtension(fileName)}");
            TemporaryFiles.Add(path);
            return path;
        }

        public Task<string> SaveMarkdownAsync(Guid conversionId, string markdown, CancellationToken ct = default)
        {
            var path = Path.Combine("Markdown", $"{conversionId}.md");
            _markdownFiles[path] = markdown;
            return Task.FromResult(path);
        }

        public Task<string> ReadMarkdownAsync(string markdownFilePath, CancellationToken ct = default)
            => Task.FromResult(_markdownFiles[markdownFilePath]);

        public void DeleteTemporary(string filePath) => TemporaryFiles.Remove(filePath);
        public void DeleteMarkdown(string filePath) => _markdownFiles.Remove(filePath);
    }

    private sealed class TestDatabase : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly AppDbContext _context;

        private TestDatabase(SqliteConnection connection, AppDbContext context)
        {
            _connection = connection;
            _context = context;
            Repository = new ConversionRepository(context);
        }

        public IConversionRepository Repository { get; }
        public void ClearTracking() => _context.ChangeTracker.Clear();

        public static async Task<TestDatabase> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;
            var context = new AppDbContext(options);
            await context.Database.EnsureCreatedAsync();

            return new TestDatabase(connection, context);
        }

        public async ValueTask DisposeAsync()
        {
            await _context.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
