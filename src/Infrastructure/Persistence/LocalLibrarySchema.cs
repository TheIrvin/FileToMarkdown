using Microsoft.EntityFrameworkCore;

namespace MarkItDownWeb.Infrastructure.Persistence;

public static class LocalLibrarySchema
{
    public static void EnsureCreated(AppDbContext context)
    {
        context.Database.EnsureCreated();
        context.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS "DocumentChunks" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_DocumentChunks" PRIMARY KEY,
                "ConversionId" TEXT NOT NULL,
                "Ordinal" INTEGER NOT NULL,
                "Section" TEXT NOT NULL,
                "Content" TEXT NOT NULL,
                CONSTRAINT "FK_DocumentChunks_Conversions_ConversionId"
                    FOREIGN KEY ("ConversionId") REFERENCES "Conversions" ("Id") ON DELETE CASCADE
            );
            """);
        context.Database.ExecuteSqlRaw("""CREATE UNIQUE INDEX IF NOT EXISTS "IX_DocumentChunks_ConversionId_Ordinal" ON "DocumentChunks" ("ConversionId", "Ordinal");""");
    }
}
