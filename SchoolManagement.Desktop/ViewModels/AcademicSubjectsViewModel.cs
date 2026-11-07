using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolManagement.Application.Academic;
using SchoolManagement.Application.Common;
using SchoolManagement.Application.Localization;
using SchoolManagement.Application.Repositories.Models;
using SchoolManagement.Application.UseCases;
using SchoolManagement.Desktop.Services;

namespace SchoolManagement.Desktop.ViewModels;

/// <summary>Subjects tab: code uniqueness is enforced by the guarded use cases.</summary>
public partial class AcademicSubjectsViewModel : AcademicListViewModelBase
{
    private readonly GetSubjectsUseCase _getSubjects;
    private readonly AddSubjectUseCase _addSubject;
    private readonly UpdateSubjectUseCase _updateSubject;
    private readonly DeleteSubjectUseCase _deleteSubject;

    private int? _editingId;
    private string _editorTitleKey = TranslationKeys.Subjects.Add;

    public AcademicSubjectsViewModel(
        GetSubjectsUseCase getSubjects,
        AddSubjectUseCase addSubject,
        UpdateSubjectUseCase updateSubject,
        DeleteSubjectUseCase deleteSubject,
        ILocalizationService localization,
        LicenseViewModel license,
        IUserDialogService dialogs)
        : base(localization, license, dialogs)
    {
        _getSubjects = getSubjects ?? throw new ArgumentNullException(nameof(getSubjects));
        _addSubject = addSubject ?? throw new ArgumentNullException(nameof(addSubject));
        _updateSubject = updateSubject ?? throw new ArgumentNullException(nameof(updateSubject));
        _deleteSubject = deleteSubject ?? throw new ArgumentNullException(nameof(deleteSubject));
    }

    public ObservableCollection<SubjectListItem> Items { get; } = new();

    [ObservableProperty]
    private SubjectListItem? _selectedItem;

    [ObservableProperty]
    private string _editorName = string.Empty;

    [ObservableProperty]
    private string _editorCode = string.Empty;

    [ObservableProperty]
    private string _editorDescription = string.Empty;

    [ObservableProperty]
    private bool _editorIsActive = true;

    protected override async Task<Failure?> LoadCoreAsync(
        AcademicListQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _getSubjects.ExecuteAsync(query, cancellationToken).ConfigureAwait(false);
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
            Localization.Tr(TranslationKeys.Subjects.DeleteMessage, ("name", item.Name)),
            Localization.Tr(TranslationKeys.Common.Delete),
            Localization.Tr(TranslationKeys.Common.Cancel)).ConfigureAwait(false);

        if (!confirmed)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _deleteSubject.ExecuteAsync(item.Id).ConfigureAwait(false);
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
        var input = new AcademicSubjectInput(
            EditorName,
            EditorCode,
            EditorDescription,
            EditorIsActive);

        IsBusy = true;
        try
        {
            if (_editingId is null)
            {
                var added = await _addSubject.ExecuteAsync(input).ConfigureAwait(false);
                if (added.IsFailure)
                {
                    ShowFailure(added.Failure!);
                    return;
                }

                ShowSuccess(TranslationKeys.Messages.AddSuccess);
            }
            else
            {
                var updated = await _updateSubject
                    .ExecuteAsync(new AcademicSubjectUpdateRequest(_editingId.Value, input))
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
        _editorTitleKey = TranslationKeys.Subjects.Add;
        EditorName = string.Empty;
        EditorCode = string.Empty;
        EditorDescription = string.Empty;
        EditorIsActive = true;
        OpenEditor(_editorTitleKey);
    }

    [RelayCommand]
    private void Edit(SubjectListItem? item) => OpenEditor(item);

    [RelayCommand]
    private Task Delete(SubjectListItem? item)
    {
        SelectedItem = item;
        return DeleteSelectedCoreAsync();
    }

    private void OpenEditor(SubjectListItem? item)
    {
        if (item is null)
        {
            return;
        }

        _editingId = item.Id;
        _editorTitleKey = TranslationKeys.Subjects.Edit;
        EditorName = item.Name;
        EditorCode = item.Code ?? string.Empty;
        EditorDescription = item.Description ?? string.Empty;
        EditorIsActive = item.IsActive;
        OpenEditor(_editorTitleKey);
    }
}
