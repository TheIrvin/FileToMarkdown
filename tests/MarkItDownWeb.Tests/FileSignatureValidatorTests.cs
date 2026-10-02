using MarkItDownWeb.Infrastructure.Security;
using Xunit;

namespace MarkItDownWeb.Tests;

public class FileSignatureValidatorTests
{
    [Fact]
    public void AcceptsPdfWithPdfHeader()
        => AssertValid(".pdf", [0x25, 0x50, 0x44, 0x46, 0x2D]);

    [Theory]
    [InlineData(".docx")]
    [InlineData(".pptx")]
    [InlineData(".xlsx")]
    public void AcceptsOfficeFilesWithZipHeader(string extension)
        => AssertValid(extension, [0x50, 0x4B, 0x03, 0x04, 0x14, 0x00]);

    [Theory]
    [InlineData(".pdf")]
    [InlineData(".docx")]
    [InlineData(".pptx")]
    [InlineData(".xlsx")]
    public void RejectsContentThatDoesNotMatchExtension(string extension)
        => AssertInvalid(extension, [0x00, 0x01, 0x02, 0x03]);

    [Fact]
    public void RejectsFileShorterThanSignature()
        => AssertInvalid(".pdf", [0x25, 0x50]);

    [Theory]
    [InlineData(".html")]
    [InlineData(".csv")]
    public void TextFormatsDoNotRequireBinarySignature(string extension)
        => AssertValid(extension, [0x00]);

    private static void AssertValid(string extension, byte[] contents)
    {
        using var file = TemporaryFile.Create(extension, contents);
        Assert.True(FileSignatureValidator.IsValid(file.Path, extension));
    }

    private static void AssertInvalid(string extension, byte[] contents)
    {
        using var file = TemporaryFile.Create(extension, contents);
        Assert.False(FileSignatureValidator.IsValid(file.Path, extension));
    }

    private sealed class TemporaryFile : IDisposable
    {
        private TemporaryFile(string path) => Path = path;

        public string Path { get; }

        public static TemporaryFile Create(string extension, byte[] contents)
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"{Guid.NewGuid()}{extension}");
            File.WriteAllBytes(path, contents);
            return new TemporaryFile(path);
        }

        public void Dispose()
        {
            if (File.Exists(Path))
                File.Delete(Path);
        }
    }
}
