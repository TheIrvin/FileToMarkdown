namespace MarkItDownWeb.Domain.Entities;

public class DocumentChunk
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ConversionId { get; set; }
    public int Ordinal { get; set; }
    public string Section { get; set; } = "Contenido";
    public string Content { get; set; } = string.Empty;
    public Conversion Conversion { get; set; } = null!;
}
