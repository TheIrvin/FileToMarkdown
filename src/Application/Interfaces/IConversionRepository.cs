using MarkItDownWeb.Application.DTOs;
using MarkItDownWeb.Domain.Entities;

namespace MarkItDownWeb.Application.Interfaces;

public interface IConversionRepository
{
    Task AddAsync(Conversion conversion, CancellationToken ct = default);
    Task<Conversion?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<Conversion>> GetHistoryAsync(string? search, DateTime? date, CancellationToken ct = default);
    Task<List<Conversion>> GetCompletedWithoutIndexAsync(CancellationToken ct = default);
    Task AddChunksAsync(IReadOnlyCollection<DocumentChunk> chunks, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<List<LibrarySearchHit>> SearchContentAsync(IReadOnlyCollection<string> terms, int limit, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
