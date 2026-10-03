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
                "SearchIndex" TEXT NOT NULL DEFAULT ' ',
                CONSTRAINT "FK_DocumentChunks_Conversions_ConversionId"
                    FOREIGN KEY ("ConversionId") REFERENCES "Conversions" ("Id") ON DELETE CASCADE
            );
            """);
        if (!HasColumn(context, "DocumentChunks", "SearchIndex"))
            context.Database.ExecuteSqlRaw("""ALTER TABLE "DocumentChunks" ADD COLUMN "SearchIndex" TEXT NOT NULL DEFAULT ' ';""");

        context.Database.ExecuteSqlRaw("""CREATE UNIQUE INDEX IF NOT EXISTS "IX_DocumentChunks_ConversionId_Ordinal" ON "DocumentChunks" ("ConversionId", "Ordinal");""");
    }

    private static bool HasColumn(AppDbContext context, string table, string column)
    {
        var connection = context.Database.GetDbConnection();
        var closeAfterRead = connection.State != System.Data.ConnectionState.Open;
        if (closeAfterRead)
            context.Database.OpenConnection();

        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = $"PRAGMA table_info(\"{table}\");";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                if (string.Equals(reader.GetString(1), column, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }
        finally
        {
            if (closeAfterRead)
                context.Database.CloseConnection();
        }
    }
}
