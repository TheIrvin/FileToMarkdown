using MarkItDownWeb.Application.Interfaces;
using MarkItDownWeb.Domain.Entities;
using Microsoft.EntityFrameworkCore;

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

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _context.Conversions.FindAsync(new object[] { id }, ct);
        if (entity is not null)
            _context.Conversions.Remove(entity);
    }

    public Task SaveChangesAsync(CancellationToken ct = default) => _context.SaveChangesAsync(ct);
}
