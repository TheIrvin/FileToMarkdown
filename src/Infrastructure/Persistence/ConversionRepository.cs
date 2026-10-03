using MarkItDownWeb.Application.DTOs;
using MarkItDownWeb.Application.Interfaces;
using MarkItDownWeb.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

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

    public async Task<List<LibrarySearchHit>> SearchContentAsync(
        IReadOnlyCollection<string> terms,
        int limit,
        CancellationToken ct = default)
    {
        var candidates = new Dictionary<(Guid ConversionId, string Section, string Content), LibrarySearchHit>();
        foreach (var term in terms)
        {
            var pattern = $"%{term}%";
            var matches = await _context.DocumentChunks
                .AsNoTracking()
                .Where(chunk => chunk.Conversion.Status == ConversionStatus.Completed
                    && (EF.Functions.Like(chunk.Content, pattern)
                        || EF.Functions.Like(chunk.Conversion.FileName, pattern)))
                .OrderByDescending(chunk => chunk.Conversion.CreatedAt)
                .ThenBy(chunk => chunk.Ordinal)
                .Select(chunk => new LibrarySearchHit
                {
                    ConversionId = chunk.ConversionId,
                    FileName = chunk.Conversion.FileName,
                    Section = chunk.Section,
                    Content = chunk.Content
                })
                .Take(limit * 4)
                .ToListAsync(ct);

            foreach (var hit in matches)
            {
                candidates.TryAdd((hit.ConversionId, hit.Section, hit.Content), hit);
            }
        }

        return candidates.Values
            .Select(hit => new LibrarySearchHit
            {
                ConversionId = hit.ConversionId,
                FileName = hit.FileName,
                Section = hit.Section,
                Content = hit.Content,
                MatchCount = terms.Count(term => HasWholeTerm(hit.Content, term)
                    || HasWholeTerm(hit.FileName, term))
            })
            .Where(hit => hit.MatchCount > 0)
            .OrderByDescending(hit => hit.MatchCount)
            .ThenBy(hit => hit.FileName, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .ToList();
    }

    private static bool HasWholeTerm(string text, string term)
        => Regex.IsMatch(text, $@"(?<![\p{{L}}\p{{N}}]){Regex.Escape(term)}(?![\p{{L}}\p{{N}}])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _context.Conversions.FindAsync(new object[] { id }, ct);
        if (entity is not null)
            _context.Conversions.Remove(entity);
    }

    public Task SaveChangesAsync(CancellationToken ct = default) => _context.SaveChangesAsync(ct);
}
