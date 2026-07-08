using MarkItDownWeb.Domain.Entities;

namespace MarkItDownWeb.Application.Interfaces;

public interface IConversionRepository
{
    Task AddAsync(Conversion conversion, CancellationToken ct = default);
    Task<Conversion?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<Conversion>> GetHistoryAsync(string? search, DateTime? date, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
