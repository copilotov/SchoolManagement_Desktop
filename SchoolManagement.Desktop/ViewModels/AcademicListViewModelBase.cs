using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolManagement.Application.Academic;
using SchoolManagement.Application.Common;
using SchoolManagement.Application.Localization;
using SchoolManagement.Desktop.Services;

namespace SchoolManagement.Desktop.ViewModels;

/// <summary>One entry of the sort ComboBox. The label is reloaded on language switch.</summary>
public sealed record AcademicSortOption(AcademicSortField Field, string Label);

/// <summary>
/// Shared toolbar, state and command surface for the four academic list
/// screens. The derived ViewModel owns the entity-specific editor and delete
/// rules; this base never talks to a repository, only to the use cases supplied
/// by the derived class.
/// </summary>
public abstract partial class AcademicListViewModelBase : PageViewModel, IDisposable
{
    private bool _suppressRefresh;
    private bool _disposed;

    protected AcademicListViewModelBase(
        ILocalizationService localization,
        LicenseViewModel license,
        IUserDialogService dialogs)
    {
        Localization = localization ?? throw new ArgumentNullException(nameof(localization));
        License = license ?? throw new ArgumentNullException(nameof(license));
        Dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));

        SortOptions = new ObservableCollection<AcademicSortOption>();
        RebuildSortOptions();
        Localization.LanguageChanged += OnLanguageChanged;
    }

    protected ILocalizationService Localization { get; }

    protected IUserDialogService Dialogs { get; }

    /// <summary>The shared license state, so a blocked write can offer "Renew".</summary>
    public LicenseViewModel License { get; }

    public ObservableCollection<AcademicSortOption> SortOptions { get; }

    [ObservableProperty]
    private string? _searchText;

    [ObservableProperty]
    private bool _includeInactive;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isLoaded;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private string _editorTitle = string.Empty;

    [ObservableProperty]
    private bool _isEditorOpen;

    [ObservableProperty]
    private bool _isRenewVisible;

    [ObservableProperty]
    private AcademicSortOption? _selectedSort;

    [ObservableProperty]
    private bool _sortDescending;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public bool HasStatus => !string.IsNullOrEmpty(StatusMessage);

    public bool HasSelection => GetSelectedItemForKeyboard() is not null;

    /// <summary>Builds the query sent to the read use case.</summary>
    public AcademicListQuery CreateQuery() => new(
        SearchText,
        IncludeInactive,
        SelectedSort?.Field ?? AcademicSortField.Name,
        SortDescending);

    /// <summary>Loads the tab or screen. Called once by the host on first display.</summary>
    public Task EnsureLoadedAsync(CancellationToken cancellationToken = default) =>
        IsLoaded ? Task.CompletedTask : RefreshAsync(cancellationToken);

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (IsLoading)
        {
            return;
        }

        IsLoading = true;
        try
        {
            var failure = await LoadCoreAsync(CreateQuery(), cancellationToken).ConfigureAwait(false);
            if (failure is not null)
            {
                ShowFailure(failure);
                return;
            }

            ErrorMessage = string.Empty;
            IsRenewVisible = false;
            IsLoaded = true;
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Derived implementation returns null on success and the failure to show on
    /// error. It fills its own observable collection inside this call.
    /// </summary>
    protected abstract Task<Failure?> LoadCoreAsync(
        AcademicListQuery query,
        CancellationToken cancellationToken);

    /// <summary>Called by the shared Delete-key command.</summary>
    protected abstract object? GetSelectedItemForKeyboard();

    protected abstract Task EditSelectedCoreAsync();

    protected abstract Task DeleteSelectedCoreAsync();

    protected abstract Task SaveCoreAsync();

    protected abstract void CloseEditorCore();

    protected abstract void OnLanguageChangedCore();

    protected void ShowFailure(Failure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        ErrorMessage = failure.ToDisplayMessage(Localization);
        StatusMessage = string.Empty;
        IsRenewVisible = failure.Kind == FailureKind.License;
    }

    protected void ShowSuccess(string messageKey)
    {
        ErrorMessage = string.Empty;
        StatusMessage = Localization.Tr(messageKey);
        IsRenewVisible = false;
    }

    protected void ClearMessages()
    {
        ErrorMessage = string.Empty;
        StatusMessage = string.Empty;
        IsRenewVisible = false;
    }

    protected void OpenEditor(string titleKey)
    {
        ClearMessages();
        EditorTitle = Localization.Tr(titleKey);
        IsEditorOpen = true;
    }

    protected void CloseEditor()
    {
        IsEditorOpen = false;
        EditorTitle = string.Empty;
    }

    [RelayCommand]
    private Task SearchAsync() => RefreshAsync();

    [RelayCommand]
    private Task ReloadAsync() => RefreshAsync();

    [RelayCommand]
    private void ClearSearch()
    {
        if (string.IsNullOrEmpty(SearchText))
        {
            return;
        }

        SearchText = string.Empty;
        _ = RefreshAsync();
    }

    [RelayCommand]
    private async Task EditSelectedAsync()
    {
        if (GetSelectedItemForKeyboard() is null)
        {
            return;
        }

        await EditSelectedCoreAsync().ConfigureAwait(false);
    }

    [RelayCommand]
    private Task DeleteSelectedAsync()
    {
        var selected = GetSelectedItemForKeyboard();
        return selected is null ? Task.CompletedTask : DeleteSelectedCoreAsync();
    }

    [RelayCommand]
    private Task SaveEditorAsync() => IsEditorOpen ? SaveCoreAsync() : Task.CompletedTask;

    [RelayCommand]
    private void CancelEditor() => CloseEditorCore();

    [RelayCommand]
    private async Task RenewLicenseAsync()
    {
        await License.RenewInteractiveAsync().ConfigureAwait(false);
        await RefreshAsync().ConfigureAwait(false);
    }

    partial void OnIncludeInactiveChanged(bool value) => RefreshIfLoaded();

    partial void OnSortDescendingChanged(bool value) => RefreshIfLoaded();

    partial void OnSelectedSortChanged(AcademicSortOption? value) => RefreshIfLoaded();

    partial void OnErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasError));

    partial void OnStatusMessageChanged(string value) => OnPropertyChanged(nameof(HasStatus));

    private void RefreshIfLoaded()
    {
        if (_suppressRefresh || !IsLoaded)
        {
            return;
        }

        _ = RefreshAsync();
    }

    private void RebuildSortOptions()
    {
        _suppressRefresh = true;
        try
        {
            var current = SelectedSort?.Field ?? AcademicSortField.Name;
            SortOptions.Clear();
            SortOptions.Add(new AcademicSortOption(
                AcademicSortField.Name,
                Localization.Tr(TranslationKeys.Common.SortName)));
            SortOptions.Add(new AcademicSortOption(
                AcademicSortField.Order,
                Localization.Tr(TranslationKeys.Common.SortOrder)));
            SortOptions.Add(new AcademicSortOption(
                AcademicSortField.LinkedItems,
                Localization.Tr(TranslationKeys.Common.SortLinked)));
            SelectedSort = SortOptions.First(option => option.Field == current);
        }
        finally
        {
            _suppressRefresh = false;
        }
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        RebuildSortOptions();
        OnLanguageChangedCore();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Localization.LanguageChanged -= OnLanguageChanged;
        GC.SuppressFinalize(this);
    }
}
