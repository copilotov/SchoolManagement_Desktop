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

/// <summary>Teachers tab: contact fields are optional but validated when filled.</summary>
public partial class AcademicTeachersViewModel : AcademicListViewModelBase
{
    private readonly GetTeachersUseCase _getTeachers;
    private readonly AddTeacherUseCase _addTeacher;
    private readonly UpdateTeacherUseCase _updateTeacher;
    private readonly DeleteTeacherUseCase _deleteTeacher;

    private int? _editingId;
    private string _editorTitleKey = TranslationKeys.Teachers.Add;

    public AcademicTeachersViewModel(
        GetTeachersUseCase getTeachers,
        AddTeacherUseCase addTeacher,
        UpdateTeacherUseCase updateTeacher,
        DeleteTeacherUseCase deleteTeacher,
        ILocalizationService localization,
        LicenseViewModel license,
        IUserDialogService dialogs)
        : base(localization, license, dialogs)
    {
        _getTeachers = getTeachers ?? throw new ArgumentNullException(nameof(getTeachers));
        _addTeacher = addTeacher ?? throw new ArgumentNullException(nameof(addTeacher));
        _updateTeacher = updateTeacher ?? throw new ArgumentNullException(nameof(updateTeacher));
        _deleteTeacher = deleteTeacher ?? throw new ArgumentNullException(nameof(deleteTeacher));
    }

    public ObservableCollection<TeacherListItem> Items { get; } = new();

    [ObservableProperty]
    private TeacherListItem? _selectedItem;

    [ObservableProperty]
    private string _editorFullName = string.Empty;

    [ObservableProperty]
    private string _editorPhone = string.Empty;

    [ObservableProperty]
    private string _editorEmail = string.Empty;

    [ObservableProperty]
    private string _editorAddress = string.Empty;

    [ObservableProperty]
    private string _editorSpecialization = string.Empty;

    [ObservableProperty]
    private DateTime? _editorHireDate;

    [ObservableProperty]
    private bool _editorIsActive = true;

    protected override async Task<Failure?> LoadCoreAsync(
        AcademicListQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _getTeachers.ExecuteAsync(query, cancellationToken).ConfigureAwait(false);
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
            Localization.Tr(TranslationKeys.Teachers.DeleteMessage, ("name", item.FullName)),
            Localization.Tr(TranslationKeys.Common.Delete),
            Localization.Tr(TranslationKeys.Common.Cancel)).ConfigureAwait(false);

        if (!confirmed)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _deleteTeacher.ExecuteAsync(item.Id).ConfigureAwait(false);
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
        var input = new AcademicTeacherInput(
            EditorFullName,
            EditorPhone,
            EditorEmail,
            EditorAddress,
            EditorSpecialization,
            EditorHireDate,
            EditorIsActive);

        IsBusy = true;
        try
        {
            if (_editingId is null)
            {
                var added = await _addTeacher.ExecuteAsync(input).ConfigureAwait(false);
                if (added.IsFailure)
                {
                    ShowFailure(added.Failure!);
                    return;
                }

                ShowSuccess(TranslationKeys.Messages.AddSuccess);
            }
            else
            {
                var updated = await _updateTeacher
                    .ExecuteAsync(new AcademicTeacherUpdateRequest(_editingId.Value, input))
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
        _editorTitleKey = TranslationKeys.Teachers.Add;
        EditorFullName = string.Empty;
        EditorPhone = string.Empty;
        EditorEmail = string.Empty;
        EditorAddress = string.Empty;
        EditorSpecialization = string.Empty;
        EditorHireDate = null;
        EditorIsActive = true;
        OpenEditor(_editorTitleKey);
    }

    [RelayCommand]
    private void Edit(TeacherListItem? item) => OpenEditor(item);

    [RelayCommand]
    private Task Delete(TeacherListItem? item)
    {
        SelectedItem = item;
        return DeleteSelectedCoreAsync();
    }

    private void OpenEditor(TeacherListItem? item)
    {
        if (item is null)
        {
            return;
        }

        _editingId = item.Id;
        _editorTitleKey = TranslationKeys.Teachers.Edit;
        EditorFullName = item.FullName;
        EditorPhone = item.Phone ?? string.Empty;
        EditorEmail = item.Email ?? string.Empty;
        EditorAddress = item.Address ?? string.Empty;
        EditorSpecialization = item.Specialization ?? string.Empty;
        EditorHireDate = item.HireDate;
        EditorIsActive = item.IsActive;
        OpenEditor(_editorTitleKey);
    }
}
