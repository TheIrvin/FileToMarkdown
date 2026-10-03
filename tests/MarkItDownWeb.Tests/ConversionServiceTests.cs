using MarkItDownWeb.Application.Interfaces;
using MarkItDownWeb.Application.Services;
using MarkItDownWeb.Domain.Entities;
using MarkItDownWeb.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
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
