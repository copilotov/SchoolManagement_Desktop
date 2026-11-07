using System.IO;
using Microsoft.Win32;

namespace SchoolManagement.Desktop.Services;

/// <summary>Thin wrapper around the two standard WPF dialogs.</summary>
public sealed class FileDialogService : IFileDialogService
{
    public Task<PickedFile?> OpenAsync(string title, string filter)
    {
        var dialog = new OpenFileDialog
        {
            Title = title,
            Filter = filter,
            CheckFileExists = true,
            Multiselect = false,
        };

        if (dialog.ShowDialog() != true)
        {
            return Task.FromResult<PickedFile?>(null);
        }

        var content = File.ReadAllBytes(dialog.FileName);
        return Task.FromResult<PickedFile?>(new PickedFile(dialog.FileName, content));
    }

    public Task<string?> PickSavePathAsync(string title, string filter, string suggestedFileName)
    {
        var dialog = new SaveFileDialog
        {
            Title = title,
            Filter = filter,
            FileName = suggestedFileName,
            AddExtension = true,
            OverwritePrompt = true,
        };

        return Task.FromResult(dialog.ShowDialog() == true ? dialog.FileName : null);
    }

    public async Task WriteAllBytesAsync(string path, byte[] content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(content);

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllBytesAsync(path, content).ConfigureAwait(false);
    }
}
