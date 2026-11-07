using System.Diagnostics;
using System.IO;

namespace SchoolManagement.Desktop.Services;

/// <summary>
/// Hands a generated PDF to the operating system. The ViewModel never touches
/// the file system or process APIs, and tests substitute this seam.
/// </summary>
public interface IPdfPrintService
{
    Task PrintAsync(byte[] pdf, string fileName);

    Task OpenAsync(byte[] pdf, string fileName);
}

public sealed class PdfPrintService : IPdfPrintService
{
    public Task PrintAsync(byte[] pdf, string fileName) => LaunchAsync(pdf, fileName, verb: "print");

    public Task OpenAsync(byte[] pdf, string fileName) => LaunchAsync(pdf, fileName, verb: null);

    private static Task LaunchAsync(byte[] pdf, string fileName, string? verb)
    {
        ArgumentNullException.ThrowIfNull(pdf);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        var directory = Path.Combine(Path.GetTempPath(), "SchoolManagement", "prints");
        Directory.CreateDirectory(directory);

        var safeName = string.Concat(fileName.Select(character =>
            Path.GetInvalidFileNameChars().Contains(character) ? '-' : character));
        var path = Path.Combine(directory, safeName);
        File.WriteAllBytes(path, pdf);

        var startInfo = new ProcessStartInfo(path) { UseShellExecute = true };
        if (verb is not null)
        {
            startInfo.Verb = verb;
        }

        try
        {
            Process.Start(startInfo);
        }
        catch (Exception) when (verb is not null)
        {
            // Not every machine has a "print" verb registered: opening the
            // document still lets the user print it from the viewer.
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }

        return Task.CompletedTask;
    }
}
