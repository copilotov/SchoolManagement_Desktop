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

/// <summary>A level choice in the grade editor. A null id means "not specified".</summary>
public sealed record AcademicLevelOption(int? Id, string Name);

/// <summary>One checkable subject row of the grade/subject linking panel.</summary>
public partial class GradeSubjectChoice : ObservableObject
{
    public GradeSubjectChoice(int id, string name, string? code, bool isSelected)
    {
        Id = id;
        Name = name;
        Code = code;
        IsSelected = isSelected;
    }

    public int Id { get; }

    public string Name { get; }

    public string? Code { get; }

    public string Display => string.IsNullOrWhiteSpace(Code) ? Name : $"{Name} ({Code})";

    [ObservableProperty]
    private bool _isSelected;
}

/// <summary>
/// Grades tab. Deleting a grade is blocked by groups but removes the grade's
/// own subject links, which the repository does in one transaction.
/// </summary>
public partial class AcademicGradesViewModel : AcademicListViewModelBase
{
    private readonly GetGradesUseCase _getGrades;
    private readonly GetAcademicLevelsUseCase _getLevels;
    private readonly GetSubjectsUseCase _getSubjects;
    private readonly GetGradeSubjectsUseCase _getGradeSubjects;
    private readonly AddGradeUseCase _addGrade;
    private readonly UpdateGradeUseCase _updateGrade;
    private readonly DeleteGradeUseCase _deleteGrade;
    private readonly SetGradeSubjectsUseCase _setGradeSubjects;

    private int? _editingId;
    private string _editorTitleKey = TranslationKeys.Grades.Add;
    private int _subjectGradeId;

    public AcademicGradesViewModel(
        GetGradesUseCase getGrades,
        GetAcademicLevelsUseCase getLevels,
        GetSubjectsUseCase getSubjects,
        GetGradeSubjectsUseCase getGradeSubjects,
        AddGradeUseCase addGrade,
        UpdateGradeUseCase updateGrade,
        DeleteGradeUseCase deleteGrade,
        SetGradeSubjectsUseCase setGradeSubjects,
        ILocalizationService localization,
        LicenseViewModel license,
        IUserDialogService dialogs)
        : base(localization, license, dialogs)
    {
        _getGrades = getGrades ?? throw new ArgumentNullException(nameof(getGrades));
        _getLevels = getLevels ?? throw new ArgumentNullException(nameof(getLevels));
        _getSubjects = getSubjects ?? throw new ArgumentNullException(nameof(getSubjects));
        _getGradeSubjects = getGradeSubjects ?? throw new ArgumentNullException(nameof(getGradeSubjects));
        _addGrade = addGrade ?? throw new ArgumentNullException(nameof(addGrade));
        _updateGrade = updateGrade ?? throw new ArgumentNullException(nameof(updateGrade));
        _deleteGrade = deleteGrade ?? throw new ArgumentNullException(nameof(deleteGrade));
        _setGradeSubjects = setGradeSubjects ?? throw new ArgumentNullException(nameof(setGradeSubjects));
    }

    public ObservableCollection<GradeListItem> Items { get; } = new();

    public ObservableCollection<AcademicLevelOption> LevelOptions { get; } = new();

    public ObservableCollection<GradeSubjectChoice> SubjectChoices { get; } = new();

    [ObservableProperty]
    private GradeListItem? _selectedItem;

    [ObservableProperty]
    private string _editorName = string.Empty;

    [ObservableProperty]
    private AcademicLevelOption? _selectedLevelOption;

    [ObservableProperty]
    private string _editorOrder = "0";

    [ObservableProperty]
    private bool _editorIsActive = true;

    [ObservableProperty]
    private bool _isSubjectPanelOpen;

    [ObservableProperty]
    private bool _isSubjectPanelBusy;

    [ObservableProperty]
    private string _subjectPanelTitle = string.Empty;

    protected override async Task<Failure?> LoadCoreAsync(
        AcademicListQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _getGrades.ExecuteAsync(query, cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
        {
            return result.Failure;
        }

        Items.Clear();
        foreach (var item in result.Value)
        {
            Items.Add(item);
        }

        var levels = await _getLevels
            .ExecuteAsync(new AcademicListQuery(null, IncludeInactive: true), cancellationToken)
            .ConfigureAwait(false);

        if (levels.IsFailure)
        {
            return levels.Failure;
        }

        var selectedLevelId = SelectedLevelOption?.Id;
        RebuildLevelOptions(levels.Value, selectedLevelId);
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
            Localization.Tr(TranslationKeys.Grades.DeleteMessage, ("name", item.Name)),
            Localization.Tr(TranslationKeys.Common.Delete),
            Localization.Tr(TranslationKeys.Common.Cancel)).ConfigureAwait(false);

        if (!confirmed)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _deleteGrade.ExecuteAsync(item.Id).ConfigureAwait(false);
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

        var input = new AcademicGradeInput(
            EditorName,
            SelectedLevelOption?.Id,
            order,
            EditorIsActive);

        IsBusy = true;
        try
        {
            if (_editingId is null)
            {
                var added = await _addGrade.ExecuteAsync(input).ConfigureAwait(false);
                if (added.IsFailure)
                {
                    ShowFailure(added.Failure!);
                    return;
                }

                ShowSuccess(TranslationKeys.Messages.AddSuccess);
            }
            else
            {
                var updated = await _updateGrade
                    .ExecuteAsync(new AcademicGradeUpdateRequest(_editingId.Value, input))
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

    protected override void CloseEditorCore()
    {
        if (IsSubjectPanelOpen)
        {
            CloseSubjects();
            return;
        }

        CloseEditor();
    }

    protected override void OnLanguageChangedCore()
    {
        if (IsEditorOpen)
        {
            EditorTitle = Localization.Tr(_editorTitleKey);
        }

        if (IsSubjectPanelOpen && SelectedItem is { } grade)
        {
            SubjectPanelTitle = Localization.Tr(
                TranslationKeys.Grades.ManageSubjects,
                ("grade_name", grade.Name));
        }

        if (IsLoaded)
        {
            _ = RefreshAsync();
        }
    }

    [RelayCommand]
    private void Add()
    {
        _editingId = null;
        _editorTitleKey = TranslationKeys.Grades.Add;
        EditorName = string.Empty;
        SelectedLevelOption = LevelOptions.FirstOrDefault();
        EditorOrder = (Items.Count + 1).ToString(CultureInfo.InvariantCulture);
        EditorIsActive = true;
        OpenEditor(_editorTitleKey);
    }

    [RelayCommand]
    private void Edit(GradeListItem? item) => OpenEditor(item);

    [RelayCommand]
    private Task Delete(GradeListItem? item)
    {
        SelectedItem = item;
        return DeleteSelectedCoreAsync();
    }

    [RelayCommand]
    private Task ManageSubjects(GradeListItem? item) => OpenSubjectsAsync(item);

    [RelayCommand]
    private async Task SaveSubjectsAsync()
    {
        if (!IsSubjectPanelOpen)
        {
            return;
        }

        var selectedIds = SubjectChoices
            .Where(choice => choice.IsSelected)
            .Select(choice => choice.Id)
            .ToArray();

        IsSubjectPanelBusy = true;
        try
        {
            var result = await _setGradeSubjects
                .ExecuteAsync(new GradeSubjectAssignment(_subjectGradeId, selectedIds))
                .ConfigureAwait(false);

            if (result.IsFailure)
            {
                ShowFailure(result.Failure!);
                return;
            }

            CloseSubjects();
            ShowSuccess(TranslationKeys.Messages.UpdateSuccess);
            await RefreshAsync().ConfigureAwait(false);
        }
        finally
        {
            IsSubjectPanelBusy = false;
        }
    }

    [RelayCommand]
    private void CloseSubjects()
    {
        IsSubjectPanelOpen = false;
        SubjectPanelTitle = string.Empty;
        SubjectChoices.Clear();
    }

    private void OpenEditor(GradeListItem? item)
    {
        if (item is null)
        {
            return;
        }

        _editingId = item.Id;
        _editorTitleKey = TranslationKeys.Grades.Edit;
        EditorName = item.Name;
        EditorOrder = item.OrderIndex.ToString(CultureInfo.InvariantCulture);
        EditorIsActive = item.IsActive;
        SelectedLevelOption = LevelOptions.FirstOrDefault(option => option.Id == item.AcademicLevelId)
            ?? LevelOptions.FirstOrDefault();
        OpenEditor(_editorTitleKey);
    }

    private async Task OpenSubjectsAsync(GradeListItem? item)
    {
        if (item is null)
        {
            return;
        }

        ClearMessages();
        var subjects = await _getSubjects
            .ExecuteAsync(new AcademicListQuery(null, IncludeInactive: false))
            .ConfigureAwait(false);

        if (subjects.IsFailure)
        {
            ShowFailure(subjects.Failure!);
            return;
        }

        var selected = await _getGradeSubjects.ExecuteAsync(item.Id).ConfigureAwait(false);
        if (selected.IsFailure)
        {
            ShowFailure(selected.Failure!);
            return;
        }

        var selectedIds = selected.Value.ToHashSet();
        SubjectChoices.Clear();
        foreach (var subject in subjects.Value)
        {
            SubjectChoices.Add(new GradeSubjectChoice(
                subject.Id,
                subject.Name,
                subject.Code,
                selectedIds.Contains(subject.Id)));
        }

        _subjectGradeId = item.Id;
        SubjectPanelTitle = Localization.Tr(
            TranslationKeys.Grades.ManageSubjects,
            ("grade_name", item.Name));
        IsSubjectPanelOpen = true;
    }

    private void RebuildLevelOptions(
        IEnumerable<AcademicLevelListItem> levels,
        int? selectedId)
    {
        LevelOptions.Clear();
        LevelOptions.Add(new AcademicLevelOption(
            null,
            Localization.Tr(TranslationKeys.Common.NotSpecified)));

        foreach (var level in levels)
        {
            LevelOptions.Add(new AcademicLevelOption(level.Id, level.Name));
        }

        RestoreSelectedLevel(selectedId);
    }

    private void RestoreSelectedLevel(int? selectedId)
    {
        if (LevelOptions.Count == 0)
        {
            return;
        }

        SelectedLevelOption = LevelOptions.FirstOrDefault(option => option.Id == selectedId)
            ?? LevelOptions[0];
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
