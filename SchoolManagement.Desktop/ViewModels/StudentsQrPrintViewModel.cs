using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolManagement.Application.Common;
using SchoolManagement.Application.Localization;
using SchoolManagement.Application.Students;
using SchoolManagement.Application.UseCases;
using SchoolManagement.Desktop.Imaging;
using SchoolManagement.Desktop.Services;

namespace SchoolManagement.Desktop.ViewModels;

/// <summary>One printable QR tile of the bulk page.</summary>
public sealed record StudentQrTile(BitmapSource? Image, string FullName, string StudentCode);

/// <summary>
/// Bulk QR sheet: derives its card size from the available space through a
/// WPF <c>Viewbox</c> + <c>UniformGrid</c>, and prints through the same A4 PDF
/// builder used by the export path.
/// </summary>
public sealed partial class StudentsQrPrintViewModel : PageViewModel
{
    private const string PdfFilter = "PDF (*.pdf)|*.pdf";

    private readonly GetStudentsUseCase _getStudents;
    private readonly RenderStudentsQrUseCase _renderQr;
    private readonly BuildStudentsQrSheetPdfUseCase _buildPdf;
    private readonly IPdfPrintService _print;
    private readonly IFileDialogService _files;
    private readonly ILocalizationService _localization;
    private readonly INavigationService _navigation;

    private readonly List<int> _studentIds = new();

    public StudentsQrPrintViewModel(
        GetStudentsUseCase getStudents,
        RenderStudentsQrUseCase renderQr,
        BuildStudentsQrSheetPdfUseCase buildPdf,
        IPdfPrintService print,
        IFileDialogService files,
        ILocalizationService localization,
        INavigationService navigation)
    {
        _getStudents = getStudents ?? throw new ArgumentNullException(nameof(getStudents));
        _renderQr = renderQr ?? throw new ArgumentNullException(nameof(renderQr));
        _buildPdf = buildPdf ?? throw new ArgumentNullException(nameof(buildPdf));
        _print = print ?? throw new ArgumentNullException(nameof(print));
        _files = files ?? throw new ArgumentNullException(nameof(files));
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));

        Tiles = new ObservableCollection<StudentQrTile>();
        ColumnOptions = new ObservableCollection<int> { 2, 3, 4, 5, 6 };
        SelectedColumns = 3;
    }

    public ObservableCollection<StudentQrTile> Tiles { get; }

    public ObservableCollection<int> ColumnOptions { get; }

    public string Title => _localization.Tr(TranslationKeys.Students.QrPageTitle);

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public bool IsEmpty => !IsLoading && Tiles.Count == 0;

    [ObservableProperty]
    private int _selectedColumns;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private string _summary = string.Empty;

    [ObservableProperty]
    private double _zoom = 1.0;

    public string ZoomText => $"{Math.Round(Zoom * 100):0} %";

    [RelayCommand]
    private void ZoomIn() => Zoom = Math.Min(2.0, Math.Round(Zoom + 0.1, 2));

    [RelayCommand]
    private void ZoomOut() => Zoom = Math.Max(0.4, Math.Round(Zoom - 0.1, 2));

    [RelayCommand]
    private void ResetZoom() => Zoom = 1.0;

    partial void OnZoomChanged(double value) => OnPropertyChanged(nameof(ZoomText));

    public async Task InitializeAsync(
        StudentListQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        IsLoading = true;
        try
        {
            var page = await _getStudents
                .ExecuteAsync(query with { Skip = 0, Take = 500 }, cancellationToken)
                .ConfigureAwait(false);

            if (page.IsFailure)
            {
                ShowFailure(page.Failure!);
                return;
            }

            await RenderAsync(page.Value.Select(item => item.Id).ToList(), cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(IsEmpty));
        }
    }

    /// <summary>Renders the QR sheet of an explicit selection from the list page.</summary>
    public async Task InitializeAsync(
        IReadOnlyCollection<int> studentIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(studentIds);

        IsLoading = true;
        try
        {
            await RenderAsync(studentIds.ToList(), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(IsEmpty));
        }
    }

    private async Task RenderAsync(List<int> ids, CancellationToken cancellationToken)
    {
        _studentIds.Clear();
        _studentIds.AddRange(ids);

        var rendered = await _renderQr.ExecuteAsync(_studentIds, cancellationToken).ConfigureAwait(false);
        if (rendered.IsFailure)
        {
            ShowFailure(rendered.Failure!);
            return;
        }

        Tiles.Clear();
        foreach (var item in rendered.Value)
        {
            Tiles.Add(new StudentQrTile(
                BitmapHelper.ToBitmapSource(item.Png),
                item.FullName,
                item.StudentCode));
        }

        Summary = _localization.Tr(
            TranslationKeys.Students.QrPageOf,
            ("current", Tiles.Count.ToString(CultureInfo.InvariantCulture)),
            ("total", Tiles.Count.ToString(CultureInfo.InvariantCulture)));
        ErrorMessage = string.Empty;
    }

    [RelayCommand]
    private async Task PrintAllAsync()
    {
        if (_studentIds.Count == 0)
        {
            ErrorMessage = _localization.Tr(TranslationKeys.Students.QrEmpty);
            return;
        }

        var pdf = await _buildPdf.ExecuteAsync(_studentIds).ConfigureAwait(false);
        if (pdf.IsFailure)
        {
            ShowFailure(pdf.Failure!);
            return;
        }

        await _print.PrintAsync(pdf.Value, "student-qr-codes.pdf").ConfigureAwait(false);
    }

    [RelayCommand]
    private async Task SavePdfAsync()
    {
        if (_studentIds.Count == 0)
        {
            ErrorMessage = _localization.Tr(TranslationKeys.Students.QrEmpty);
            return;
        }

        var pdf = await _buildPdf.ExecuteAsync(_studentIds).ConfigureAwait(false);
        if (pdf.IsFailure)
        {
            ShowFailure(pdf.Failure!);
            return;
        }

        var path = await _files
            .PickSavePathAsync(_localization.Tr(TranslationKeys.Students.QrPrintAll), PdfFilter, "student-qr-codes.pdf")
            .ConfigureAwait(false);

        if (path is not null)
        {
            await _files.WriteAllBytesAsync(path, pdf.Value).ConfigureAwait(false);
        }
    }

    [RelayCommand]
    private Task CloseAsync()
    {
        _navigation.NavigateToTab(ShellTab.Students);
        return Task.CompletedTask;
    }

    private void ShowFailure(Failure failure) => ErrorMessage = failure.ToDisplayMessage(_localization);

    partial void OnErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasError));

    partial void OnIsLoadingChanged(bool value) => OnPropertyChanged(nameof(IsEmpty));
}
