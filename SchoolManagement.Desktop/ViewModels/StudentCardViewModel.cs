using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolManagement.Application.Common;
using SchoolManagement.Application.Localization;
using SchoolManagement.Application.UseCases;
using SchoolManagement.Desktop.Imaging;
using SchoolManagement.Desktop.Services;

namespace SchoolManagement.Desktop.ViewModels;

/// <summary>
/// Printable student card. The preview comes from the exact same QuestPDF
/// layout used by the PDF, so what the user sees is what is printed.
/// </summary>
public sealed partial class StudentCardViewModel : PageViewModel
{
    private const string PdfFilter = "PDF (*.pdf)|*.pdf";

    private readonly RenderStudentCardPreviewUseCase _preview;
    private readonly BuildStudentCardPdfUseCase _buildPdf;
    private readonly IFileDialogService _files;
    private readonly IPdfPrintService _print;
    private readonly ILocalizationService _localization;
    private readonly INavigationService _navigation;

    public StudentCardViewModel(
        RenderStudentCardPreviewUseCase preview,
        BuildStudentCardPdfUseCase buildPdf,
        IFileDialogService files,
        IPdfPrintService print,
        ILocalizationService localization,
        INavigationService navigation)
    {
        _preview = preview ?? throw new ArgumentNullException(nameof(preview));
        _buildPdf = buildPdf ?? throw new ArgumentNullException(nameof(buildPdf));
        _files = files ?? throw new ArgumentNullException(nameof(files));
        _print = print ?? throw new ArgumentNullException(nameof(print));
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
    }

    [ObservableProperty]
    private int _studentId;

    [ObservableProperty]
    private BitmapSource? _previewImage;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public string Title => _localization.Tr(TranslationKeys.Students.CardPreview);

    public async Task InitializeAsync(int studentId, CancellationToken cancellationToken = default)
    {
        StudentId = studentId;
        IsLoading = true;
        try
        {
            var result = await _preview.ExecuteAsync(studentId, cancellationToken).ConfigureAwait(false);
            if (result.IsFailure)
            {
                ShowFailure(result.Failure!);
                return;
            }

            PreviewImage = BitmapHelper.ToBitmapSource(result.Value);
            ErrorMessage = string.Empty;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task PrintAsync()
    {
        var pdf = await BuildPdfAsync().ConfigureAwait(false);
        if (pdf is null)
        {
            return;
        }

        await _print.PrintAsync(pdf, FileName()).ConfigureAwait(false);
    }

    [RelayCommand]
    private async Task SavePdfAsync()
    {
        var pdf = await BuildPdfAsync().ConfigureAwait(false);
        if (pdf is null)
        {
            return;
        }

        var path = await _files
            .PickSavePathAsync(_localization.Tr(TranslationKeys.Students.CardSavePdf), PdfFilter, FileName())
            .ConfigureAwait(false);

        if (path is null)
        {
            return;
        }

        await _files.WriteAllBytesAsync(path, pdf).ConfigureAwait(false);
    }

    [RelayCommand]
    private async Task CloseAsync()
    {
        var students = _navigation.NavigateTo<StudentsViewModel>();
        await students.EnsureLoadedAsync().ConfigureAwait(false);
    }

    private async Task<byte[]?> BuildPdfAsync()
    {
        var result = await _buildPdf.ExecuteAsync(StudentId).ConfigureAwait(false);
        if (result.IsFailure)
        {
            ShowFailure(result.Failure!);
            return null;
        }

        return result.Value;
    }

    private string FileName() => $"student-card-{StudentId}.pdf";

    private void ShowFailure(Failure failure) => ErrorMessage = failure.ToDisplayMessage(_localization);

    partial void OnErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasError));

    partial void OnPreviewImageChanged(BitmapSource? value) => OnPropertyChanged(nameof(HasPreview));

    public bool HasPreview => PreviewImage is not null;
}
