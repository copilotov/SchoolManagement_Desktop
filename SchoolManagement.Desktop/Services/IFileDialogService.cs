namespace SchoolManagement.Desktop.Services;

/// <summary>Bytes chosen by the user plus the original file name.</summary>
public sealed record PickedFile(string FileName, byte[] Content);

/// <summary>
/// MVVM seam over the WPF file dialogs. ViewModels pass already-localized text
/// and never reference <c>Microsoft.Win32</c> directly, so they stay testable
/// without a message loop.
/// </summary>
public interface IFileDialogService
{
    Task<PickedFile?> OpenAsync(string title, string filter);

    Task<string?> PickSavePathAsync(string title, string filter, string suggestedFileName);

    Task WriteAllBytesAsync(string path, byte[] content);
}
