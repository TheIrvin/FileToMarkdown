using MarkItDownWeb.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MarkItDownWeb.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Conversion> Conversions => Set<Conversion>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Conversion>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.FileName).IsRequired().HasMaxLength(255);
            entity.Property(c => c.OriginalFilePath).HasMaxLength(500);
            entity.Property(c => c.MarkdownFilePath).HasMaxLength(500);
            entity.Property(c => c.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(c => c.FileType).HasConversion<string>().HasMaxLength(20);
            entity.HasIndex(c => c.CreatedAt);
        });
    }
}
