namespace MarkItDownWeb.Infrastructure.Security;

public static class FileSignatureValidator
{
    private static readonly Dictionary<string, byte[]> Signatures = new()
    {
        [".pdf"] = new byte[] { 0x25, 0x50, 0x44, 0x46 },
        [".docx"] = new byte[] { 0x50, 0x4B, 0x03, 0x04 },
        [".pptx"] = new byte[] { 0x50, 0x4B, 0x03, 0x04 },
        [".xlsx"] = new byte[] { 0x50, 0x4B, 0x03, 0x04 }
    };

    public static bool IsValid(string filePath, string extension)
    {
        if (!Signatures.TryGetValue(extension.ToLowerInvariant(), out var expected))
            return true;

        using var stream = File.OpenRead(filePath);
        var header = new byte[expected.Length];
        var read = stream.Read(header, 0, header.Length);

        return read == expected.Length && header.SequenceEqual(expected);
    }
}
