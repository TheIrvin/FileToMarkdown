using MarkItDownWeb.Application.DTOs;
using MarkItDownWeb.Application.Interfaces;
using MarkItDownWeb.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using MarkItDownWeb.Application.Services;
using Microsoft.Data.Sqlite;

namespace MarkItDownWeb.Infrastructure.Persistence;

public class ConversionRepository : IConversionRepository
{
    private readonly AppDbContext _context;

    public ConversionRepository(AppDbContext context) => _context = context;

    public async Task AddAsync(Conversion conversion, CancellationToken ct = default)
        => await _context.Conversions.AddAsync(conversion, ct);

    public Task<Conversion?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _context.Conversions.FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<List<Conversion>> GetHistoryAsync(string? search, DateTime? date, CancellationToken ct = default)
    {
        var query = _context.Conversions.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(c => c.FileName.Contains(search));

        if (date is not null)
            query = query.Where(c => c.CreatedAt.Date == date.Value.Date);

        return await query.OrderByDescending(c => c.CreatedAt).ToListAsync(ct);
    }

    public Task<List<Conversion>> GetCompletedWithoutIndexAsync(CancellationToken ct = default)
        => _context.Conversions
            .Include(conversion => conversion.Chunks)
            .Where(conversion => conversion.Status == ConversionStatus.Completed && !conversion.Chunks.Any())
            .ToListAsync(ct);

    public Task AddChunksAsync(IReadOnlyCollection<DocumentChunk> chunks, CancellationToken ct = default)
        => _context.DocumentChunks.AddRangeAsync(chunks, ct);

    public async Task BackfillSearchIndexesAsync(CancellationToken ct = default)
    {
        var chunks = await _context.DocumentChunks
            .Include(chunk => chunk.Conversion)
            .Where(chunk => chunk.SearchIndex == " ")
            .ToListAsync(ct);

        foreach (var chunk in chunks)
            chunk.SearchIndex = SearchTextNormalizer.CreateIndex(chunk.Conversion.FileName, chunk.Content);
    }

    public async Task<List<LibrarySearchHit>> SearchContentAsync(
        IReadOnlyCollection<string> terms,
        int limit,
        CancellationToken ct = default)
    {
        if (terms.Count == 0 || limit <= 0)
            return [];

        var matchTerms = terms
            .Select((_, index) => $"instr(chunk.\"SearchIndex\", $term{index}) > 0")
            .ToArray();
        var score = string.Join(" + ", matchTerms.Select(condition => $"CASE WHEN {condition} THEN 1 ELSE 0 END"));
        var where = string.Join(" OR ", matchTerms);
        var sql = $"""
            SELECT
                chunk."ConversionId" AS "ConversionId",
                conversion."FileName" AS "FileName",
                chunk."Section" AS "Section",
                chunk."Content" AS "Content",
                ({score}) AS "MatchCount"
            FROM "DocumentChunks" AS chunk
            INNER JOIN "Conversions" AS conversion ON conversion."Id" = chunk."ConversionId"
            WHERE conversion."Status" = 'Completed' AND ({where})
            ORDER BY "MatchCount" DESC, conversion."FileName" COLLATE NOCASE ASC, conversion."CreatedAt" DESC, chunk."Ordinal" ASC
            LIMIT $limit;
            """;
        var parameters = terms
            .Select((term, index) => (object)new SqliteParameter($"$term{index}", $" {term} "))
            .Append(new SqliteParameter("$limit", limit))
            .ToArray();

        return await _context.Database
            .SqlQueryRaw<LibrarySearchHit>(sql, parameters)
            .ToListAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _context.Conversions.FindAsync(new object[] { id }, ct);
        if (entity is not null)
            _context.Conversions.Remove(entity);
    }

    public Task SaveChangesAsync(CancellationToken ct = default) => _context.SaveChangesAsync(ct);
}
