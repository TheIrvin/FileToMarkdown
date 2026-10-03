using MarkItDownWeb.Application.Interfaces;
using MarkItDownWeb.Domain.Entities;
using MarkItDownWeb.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MarkItDownWeb.Tests;

public class LocalLibrarySchemaTests
{
    [Fact]
    public async Task EnsureCreated_adds_the_index_to_an_existing_database_and_preserves_history()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        var id = Guid.NewGuid();

        await using (var legacy = new AppDbContext(options))
        {
            await legacy.Database.ExecuteSqlRawAsync("""
                CREATE TABLE "Conversions" (
                    "Id" TEXT NOT NULL PRIMARY KEY,
                    "FileName" TEXT NOT NULL,
                    "FileType" TEXT NOT NULL,
                    "OriginalFilePath" TEXT NOT NULL,
                    "MarkdownFilePath" TEXT NULL,
                    "FileSizeBytes" INTEGER NOT NULL,
                    "CreatedAt" TEXT NOT NULL,
                    "ConversionTimeSeconds" REAL NULL,
                    "Status" TEXT NOT NULL,
                    "ErrorMessage" TEXT NULL
                );
                """);
            await legacy.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "Conversions" VALUES
                ({id}, 'legacy.csv', 'Csv', '', 'legacy.md', 42, '2026-10-03 00:00:00', 0.1, 'Completed', NULL);
                """);
            await legacy.Database.ExecuteSqlRawAsync("""
                CREATE TABLE "DocumentChunks" (
                    "Id" TEXT NOT NULL CONSTRAINT "PK_DocumentChunks" PRIMARY KEY,
                    "ConversionId" TEXT NOT NULL,
                    "Ordinal" INTEGER NOT NULL,
                    "Section" TEXT NOT NULL,
                    "Content" TEXT NOT NULL,
                    CONSTRAINT "FK_DocumentChunks_Conversions_ConversionId"
                        FOREIGN KEY ("ConversionId") REFERENCES "Conversions" ("Id") ON DELETE CASCADE
                );
                """);
            var chunkId = Guid.NewGuid();
            await legacy.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "DocumentChunks" VALUES
                ({chunkId}, {id}, 0, 'Old document', 'Preserved searchable text');
                """);

            LocalLibrarySchema.EnsureCreated(legacy);

            var preserved = await legacy.Conversions.SingleAsync();
            Assert.Equal(id, preserved.Id);
            Assert.Equal("legacy.csv", preserved.FileName);
            IConversionRepository repository = new ConversionRepository(legacy);
            await repository.BackfillSearchIndexesAsync();
            await repository.SaveChangesAsync();
            var hits = await repository.SearchContentAsync(["searchable"], 5);
            Assert.Equal(id, Assert.Single(hits).ConversionId);

            await repository.DeleteAsync(id);
            await repository.SaveChangesAsync();
            Assert.Equal(0, await legacy.DocumentChunks.CountAsync());
            Assert.Equal(0, await legacy.Conversions.CountAsync());
        }
    }
}
