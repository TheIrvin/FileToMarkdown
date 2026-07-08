using System.Diagnostics;
using System.Text;
using MarkItDownWeb.Application.Interfaces;
using Microsoft.Extensions.Configuration;

namespace MarkItDownWeb.Infrastructure.Services;

public class MarkItDownService : IMarkItDownService
{
    private readonly string _pythonPath;

    public MarkItDownService(IConfiguration configuration)
    {
        _pythonPath = configuration["MarkItDown:PythonPath"] ?? "python";
    }

    public async Task<string> ConvertFileAsync(string absoluteFilePath, CancellationToken ct = default)
    {
        if (!File.Exists(absoluteFilePath))
            throw new FileNotFoundException("File not found", absoluteFilePath);

        var psi = new ProcessStartInfo
        {
            FileName = _pythonPath,
            ArgumentList = { "-m", "markitdown", absoluteFilePath },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8
        };

        using var process = new Process { StartInfo = psi };
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        process.OutputDataReceived += (_, e) => { if (e.Data is not null) stdout.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) stderr.AppendLine(e.Data); };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0)
            throw new InvalidOperationException($"MarkItDown failed: {stderr}");

        return stdout.ToString();
    }
}
