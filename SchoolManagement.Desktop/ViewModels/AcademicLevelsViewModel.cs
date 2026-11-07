using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolManagement.Application.Academic;
using SchoolManagement.Application.Common;
using SchoolManagement.Application.Localization;
using SchoolManagement.Application.Repositories.Models;
using SchoolManagement.Application.UseCases;
using SchoolManagement.Desktop.Services;

namespace SchoolManagement.Desktop.ViewModels;

/// <summary>Academic levels tab: list, search/sort, add/edit dialog, guarded delete.</summary>
public partial class AcademicLevelsViewModel : AcademicListViewModelBase
{
    private readonly GetAcademicLevelsUseCase _getLevels;
    private readonly AddAcademicLevelUseCase _addLevel;
    private readonly UpdateAcademicLevelUseCase _updateLevel;
    private readonly DeleteAcademicLevelUseCase _deleteLevel;

    private int? _editingId;
    private string _editorTitleKey = TranslationKeys.Levels.Add;

    public AcademicLevelsViewModel(
        GetAcademicLevelsUseCase getLevels,
        AddAcademicLevelUseCase addLevel,
        UpdateAcademicLevelUseCase updateLevel,
        DeleteAcademicLevelUseCase deleteLevel,
        ILocalizationService localization,
        LicenseViewModel license,
        IUserDialogService dialogs)
        : base(localization, license, dialogs)
    {
        _getLevels = getLevels ?? throw new ArgumentNullException(nameof(getLevels));
        _addLevel = addLevel ?? throw new ArgumentNullException(nameof(addLevel));
        _updateLevel = updateLevel ?? throw new ArgumentNullException(nameof(updateLevel));
        _deleteLevel = deleteLevel ?? throw new ArgumentNullException(nameof(deleteLevel));
    }

    public ObservableCollection<AcademicLevelListItem> Items { get; } = new();

    [ObservableProperty]
    private AcademicLevelListItem? _selectedItem;

    [ObservableProperty]
    private string _editorName = string.Empty;

    [ObservableProperty]
    private string _editorOrder = "0";

    [ObservableProperty]
    private bool _editorIsActive = true;

    protected override async Task<Failure?> LoadCoreAsync(
        AcademicListQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _getLevels.ExecuteAsync(query, cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
        {
            return result.Failure;
        }

        Items.Clear();
        foreach (var item in result.Value)
        {
            Items.Add(item);
        }

        return null;
    }

    protected override object? GetSelectedItemForKeyboard() => SelectedItem;

    protected override Task EditSelectedCoreAsync()
    {
        OpenEditor(SelectedItem);
        return Task.CompletedTask;
    }

    protected override async Task DeleteSelectedCoreAsync()
    {
        var item = SelectedItem;
        if (item is null)
        {
            return;
        }

        var confirmed = await Dialogs.ConfirmAsync(
            Localization.Tr(TranslationKeys.Dialogs.ConfirmDelete),
            Localization.Tr(TranslationKeys.Levels.DeleteMessage, ("name", item.Name)),
            Localization.Tr(TranslationKeys.Common.Delete),
            Localization.Tr(TranslationKeys.Common.Cancel)).ConfigureAwait(false);

        if (!confirmed)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _deleteLevel.ExecuteAsync(item.Id).ConfigureAwait(false);
            if (result.IsFailure)
            {
                ShowFailure(result.Failure!);
                return;
            }

            SelectedItem = null;
            ShowSuccess(TranslationKeys.Messages.DeleteSuccess);
            await RefreshAsync().ConfigureAwait(false);
        }
        finally
        {
            IsBusy = false;
        }
    }

    protected override async Task SaveCoreAsync()
    {
        if (!TryParseOrder(EditorOrder, out var order))
        {
            return;
        }

        var input = new AcademicLevelInput(EditorName, order, EditorIsActive);
        IsBusy = true;
        try
        {
            if (_editingId is null)
            {
                var added = await _addLevel.ExecuteAsync(input).ConfigureAwait(false);
                if (added.IsFailure)
                {
                    ShowFailure(added.Failure!);
                    return;
                }

                ShowSuccess(TranslationKeys.Messages.AddSuccess);
            }
            else
            {
                var updated = await _updateLevel
                    .ExecuteAsync(new AcademicLevelUpdateRequest(_editingId.Value, input))
                    .ConfigureAwait(false);

                if (updated.IsFailure)
                {
                    ShowFailure(updated.Failure!);
                    return;
                }

                ShowSuccess(TranslationKeys.Messages.UpdateSuccess);
            }

            CloseEditor();
            await RefreshAsync().ConfigureAwait(false);
        }
        finally
        {
            IsBusy = false;
        }
    }

    protected override void CloseEditorCore() => CloseEditor();

    protected override void OnLanguageChangedCore()
    {
        if (IsEditorOpen)
        {
            EditorTitle = Localization.Tr(_editorTitleKey);
        }
    }

    [RelayCommand]
    private void Add()
    {
        _editingId = null;
        _editorTitleKey = TranslationKeys.Levels.Add;
        EditorName = string.Empty;
        EditorOrder = (Items.Count + 1).ToString(CultureInfo.InvariantCulture);
        EditorIsActive = true;
        OpenEditor(_editorTitleKey);
    }

    [RelayCommand]
    private void Edit(AcademicLevelListItem? item) => OpenEditor(item);

    [RelayCommand]
    private Task Delete(AcademicLevelListItem? item)
    {
        SelectedItem = item;
        return DeleteSelectedCoreAsync();
    }

    private void OpenEditor(AcademicLevelListItem? item)
    {
        if (item is null)
        {
            return;
        }

        _editingId = item.Id;
        _editorTitleKey = TranslationKeys.Levels.Edit;
        EditorName = item.Name;
        EditorOrder = item.OrderIndex.ToString(CultureInfo.InvariantCulture);
        EditorIsActive = item.IsActive;
        OpenEditor(_editorTitleKey);
    }

    private bool TryParseOrder(string? text, out int order)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            order = 0;
            return true;
        }

        if (int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out order))
        {
            return true;
        }

        ShowFailure(Failure.Validation(TranslationKeys.Validators.InvalidNumber));
        return false;
    }
}
