using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolManagement.Application.Common;
using SchoolManagement.Application.Localization;
using SchoolManagement.Application.UseCases;
using SchoolManagement.Desktop.Services;

namespace SchoolManagement.Desktop.ViewModels;

/// <summary>
/// Printable full group report. The PDF is built by the read use case and the
/// same bytes feed print, save and open, so the printed page cannot drift from
/// the saved file.
/// </summary>
public sealed partial class GroupFullReportViewModel : PageViewModel, IDisposable
{
    private const string PdfFilter = "PDF (*.pdf)|*.pdf";

    private readonly BuildGroupFullReportUseCase _buildReport;
    private readonly IFileDialogService _files;
    private readonly IPdfPrintService _print;
    private readonly ILocalizationService _localization;
    private readonly INavigationService _navigation;

    private byte[]? _pdf;
    private bool _disposed;

    public GroupFullReportViewModel(
        BuildGroupFullReportUseCase buildReport,
        IFileDialogService files,
        IPdfPrintService print,
        ILocalizationService localization,
        INavigationService navigation)
    {
        _buildReport = buildReport ?? throw new ArgumentNullException(nameof(buildReport));
        _files = files ?? throw new ArgumentNullException(nameof(files));
        _print = print ?? throw new ArgumentNullException(nameof(print));
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));

        _localization.LanguageChanged += OnLanguageChanged;
    }

    public string Title => _localization.Tr(TranslationKeys.Groups.FullReport);

    [ObservableProperty]
    private int _groupId;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isReady;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public async Task InitializeAsync(int groupId, CancellationToken cancellationToken = default)
    {
        GroupId = groupId;
        await LoadAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        IsLoading = true;
        try
        {
            var result = await _buildReport.ExecuteAsync(GroupId, cancellationToken).ConfigureAwait(false);
            if (result.IsFailure)
            {
                _pdf = null;
                IsReady = false;
                ShowFailure(result.Failure!);
                return;
            }

            _pdf = result.Value;
            IsReady = true;
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
        if (_pdf is null)
        {
            return;
        }

        await _print.PrintAsync(_pdf, FileName()).ConfigureAwait(false);
    }

    [RelayCommand]
    private async Task OpenAsync()
    {
        if (_pdf is null)
        {
            return;
        }

        await _print.OpenAsync(_pdf, FileName()).ConfigureAwait(false);
    }

    [RelayCommand]
    private async Task SavePdfAsync()
    {
        if (_pdf is null)
        {
            return;
        }

        var path = await _files
            .PickSavePathAsync(Title, PdfFilter, FileName())
            .ConfigureAwait(false);

        if (path is null)
        {
            return;
        }

        await _files.WriteAllBytesAsync(path, _pdf).ConfigureAwait(false);
    }

    [RelayCommand]
    private async Task ReloadAsync() => await LoadAsync().ConfigureAwait(false);

    [RelayCommand]
    private void Back() => _navigation.NavigateTo<GroupsViewModel>();

    [RelayCommand]
    private void Close() => _navigation.NavigateTo<GroupsViewModel>();

    private string FileName() => $"group-report-{GroupId}.pdf";

    private void ShowFailure(Failure failure) => ErrorMessage = failure.ToDisplayMessage(_localization);

    partial void OnErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasError));

    private void OnLanguageChanged(object? sender, EventArgs e) => OnPropertyChanged(nameof(Title));

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _localization.LanguageChanged -= OnLanguageChanged;
        GC.SuppressFinalize(this);
    }
}
