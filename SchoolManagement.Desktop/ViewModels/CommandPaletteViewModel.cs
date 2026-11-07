using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolManagement.Application.Common;
using SchoolManagement.Application.Groups;
using SchoolManagement.Application.Localization;
using SchoolManagement.Application.Repositories.Models;
using SchoolManagement.Application.Students;
using SchoolManagement.Application.UseCases;
using SchoolManagement.Desktop.Services;

namespace SchoolManagement.Desktop.ViewModels;

/// <summary>
/// One row of the Ctrl+K palette. <see cref="Group"/> is the localized section
/// caption, so the view can group the list without a second model.
/// </summary>
public sealed record PaletteResult(
    string Title,
    string Subtitle,
    string Glyph,
    string Group,
    Action Invoke);

/// <summary>
/// Phase D2 - the Ctrl+K command palette. It unifies two things in one list:
///  * commands (add student, new group, take attendance, …), always available;
///  * data results (students and groups) from the existing read use cases.
///
/// Payments have no cross-student search in the current repository contract, so
/// the palette offers the "record payment" command instead of pretending to
/// search a table it cannot query. The palette never writes: every command only
/// navigates and, when the destination exposes it, opens its existing form.
/// </summary>
public sealed partial class CommandPaletteViewModel : ObservableObject, IDisposable
{
    private const int MinSearchLength = 2;
    private const int MaxResultsPerKind = 5;

    private readonly INavigationService _navigation;
    private readonly ILocalizationService _localization;
    private readonly GetStudentsUseCase _students;
    private readonly GetGroupsUseCase _groups;

    private IReadOnlyList<PaletteResult> _commands = Array.Empty<PaletteResult>();
    private CancellationTokenSource? _searchCts;
    private Task _pendingSearch = Task.CompletedTask;
    private int _searchGeneration;

    public CommandPaletteViewModel(
        INavigationService navigation,
        ILocalizationService localization,
        GetStudentsUseCase students,
        GetGroupsUseCase groups)
    {
        _navigation = navigation;
        _localization = localization;
        _students = students;
        _groups = groups;

        Results.CollectionChanged += OnResultsChanged;
        _localization.LanguageChanged += OnLanguageChanged;
        RebuildCommands();
    }

    public ObservableCollection<PaletteResult> Results { get; } = new();

    /// <summary>Raised when the user runs the theme command; the shell owns the theme.</summary>
    public event EventHandler? ThemeToggleRequested;

    /// <summary>Raised when the user runs the shortcuts command; the shell owns the window.</summary>
    public event EventHandler? ShortcutsRequested;

    [ObservableProperty]
    private bool _isOpen;

    [ObservableProperty]
    private string? _query;

    [ObservableProperty]
    private PaletteResult? _selectedResult;

    public bool HasResults => Results.Count > 0;

    public bool ShowNoResults => IsOpen && !HasResults && !string.IsNullOrWhiteSpace(Query);

    public string Title => _localization.Tr(TranslationKeys.Palette.Title);

    public string Placeholder => _localization.Tr(TranslationKeys.Palette.Placeholder);

    public string NoResultsText => _localization.Tr(TranslationKeys.Palette.NoResults);

    public string HintText => _localization.Tr(TranslationKeys.Palette.Hint);

    /// <summary>Completes when the search started by the last query change has finished.</summary>
    public Task WhenSearchSettles() => _pendingSearch;

    public void Open()
    {
        if (IsOpen)
        {
            return;
        }

        RebuildCommands();
        IsOpen = true;
        Query = null;
        Populate(_commands);
    }

    public void Close()
    {
        if (!IsOpen)
        {
            return;
        }

        _searchCts?.Cancel();
        IsOpen = false;
        Query = null;
        Results.Clear();
        SelectedResult = null;
    }

    [RelayCommand]
    private void RunSelected()
    {
        if (SelectedResult is not { } result)
        {
            return;
        }

        Close();
        result.Invoke();
    }

    [RelayCommand]
    private void MoveUp() => MoveSelection(-1);

    [RelayCommand]
    private void MoveDown() => MoveSelection(1);

    [RelayCommand]
    private void ClosePalette() => Close();

    public void MoveSelection(int delta)
    {
        if (Results.Count == 0)
        {
            return;
        }

        var index = SelectedResult is null ? -1 : Results.IndexOf(SelectedResult);
        var next = (index + delta + Results.Count) % Results.Count;
        SelectedResult = Results[next];
    }

    partial void OnQueryChanged(string? value)
    {
        if (!IsOpen)
        {
            return;
        }

        _pendingSearch = SearchAsync();
    }

    partial void OnSelectedResultChanged(PaletteResult? value) => OnPropertyChanged(nameof(ShowNoResults));

    partial void OnIsOpenChanged(bool value) => OnPropertyChanged(nameof(ShowNoResults));

    /// <summary>
    /// Filters the commands and, from two characters, queries students and
    /// groups. A generation counter drops the result of a superseded keystroke so
    /// a slow query can never overwrite a newer one.
    /// </summary>
    public async Task SearchAsync(CancellationToken cancellationToken = default)
    {
        var generation = ++_searchGeneration;
        var term = Query?.Trim() ?? string.Empty;

        _searchCts?.Cancel();
        _searchCts?.Dispose();
        _searchCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var token = _searchCts.Token;

        var commands = FilterCommands(term);
        if (term.Length < MinSearchLength)
        {
            if (generation == _searchGeneration)
            {
                Populate(commands);
            }

            return;
        }

        var results = new List<PaletteResult>(commands);

        var students = await _students
            .ExecuteAsync(new StudentListQuery(SearchTerm: term, Take: MaxResultsPerKind), token)
            .ConfigureAwait(true);
        if (generation != _searchGeneration || token.IsCancellationRequested)
        {
            return;
        }

        if (students.IsSuccess)
        {
            results.AddRange(students.Value.Select(MapStudent));
        }

        var groups = await _groups
            .ExecuteAsync(new GroupListQuery(SearchTerm: term), token)
            .ConfigureAwait(true);
        if (generation != _searchGeneration || token.IsCancellationRequested)
        {
            return;
        }

        if (groups.IsSuccess)
        {
            results.AddRange(groups.Value.Take(MaxResultsPerKind).Select(MapGroup));
        }

        Populate(results);
    }

    private void Populate(IReadOnlyList<PaletteResult> results)
    {
        Results.Clear();
        foreach (var result in results)
        {
            Results.Add(result);
        }

        SelectedResult = Results.FirstOrDefault();
        OnPropertyChanged(nameof(HasResults));
        OnPropertyChanged(nameof(ShowNoResults));
    }

    private IReadOnlyList<PaletteResult> FilterCommands(string term)
    {
        if (term.Length == 0)
        {
            return _commands;
        }

        return _commands
            .Where(command => command.Title.Contains(term, StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    private void RebuildCommands()
    {
        var commandsGroup = _localization.Tr(TranslationKeys.Palette.Commands);

        _commands = new[]
        {
            Command(TranslationKeys.Palette.AddStudent, "Icon.Person", commandsGroup, OpenAddStudent),
            Command(TranslationKeys.Palette.NewGroup, "Icon.Groups", commandsGroup, OpenNewGroup),
            Command(TranslationKeys.Palette.NewHomework, "Icon.Book", commandsGroup, OpenNewHomework),
            Command(TranslationKeys.Palette.TakeAttendance, "Icon.Attendance", commandsGroup, OpenAttendance),
            Command(TranslationKeys.Palette.RecordPayment, "Icon.Receipt", commandsGroup, OpenFinance),
            Command(TranslationKeys.Palette.OpenSettings, "Icon.Settings", commandsGroup, OpenSettings),
            Command(TranslationKeys.Palette.ToggleTheme, "Icon.Lightbulb", commandsGroup, ToggleTheme),
            Command(TranslationKeys.Palette.ShowShortcuts, "Icon.Help", commandsGroup, ShowShortcuts),
        };
    }

    private PaletteResult Command(string titleKey, string glyphKey, string group, Action invoke) =>
        new(
            _localization.Tr(titleKey),
            string.Empty,
            Glyph(glyphKey),
            group,
            invoke);

    /// <summary>Icons are glyph strings from Icons.xaml, not translatable text.</summary>
    private static string Glyph(string key) =>
        System.Windows.Application.Current?.TryFindResource(key) as string ?? string.Empty;

    private PaletteResult MapStudent(StudentListItem student)
    {
        var subtitle = string.IsNullOrWhiteSpace(student.GroupNames)
            ? student.StudentCode
            : $"{student.StudentCode} · {student.GroupNames}";

        return new PaletteResult(
            $"{student.FirstName} {student.LastName}".Trim(),
            subtitle,
            Glyph("Icon.Person"),
            _localization.Tr(TranslationKeys.Palette.Students),
            () => OpenStudent(student));
    }

    private PaletteResult MapGroup(GroupSummary group)
    {
        var parts = new[] { group.SubjectName, group.GradeName }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();

        return new PaletteResult(
            group.Name,
            string.Join(" · ", parts),
            Glyph("Icon.Groups"),
            _localization.Tr(TranslationKeys.Palette.Groups),
            () => OpenGroup(group));
    }

    private void OpenStudent(StudentListItem student)
    {
        _navigation.NavigateTo(ShellTab.Students);
        if (_navigation.CurrentViewModel is StudentsViewModel students)
        {
            students.SearchText = student.StudentCode;
        }
    }

    private void OpenGroup(GroupSummary group)
    {
        _navigation.NavigateTo(ShellTab.Groups);
        if (_navigation.CurrentViewModel is GroupsViewModel groups)
        {
            groups.SearchText = group.Name;
        }
    }

    private void OpenAddStudent()
    {
        _navigation.NavigateTo(ShellTab.Students);
        if (_navigation.CurrentViewModel is StudentsViewModel students)
        {
            students.AddCommand.Execute(null);
        }
    }

    private void OpenNewGroup()
    {
        _navigation.NavigateTo(ShellTab.Groups);
        if (_navigation.CurrentViewModel is GroupsViewModel groups)
        {
            groups.AddCommand.Execute(null);
        }
    }

    private void OpenNewHomework()
    {
        _navigation.NavigateTo(ShellTab.Homework);
        if (_navigation.CurrentViewModel is HomeworkViewModel homeworks)
        {
            homeworks.AddCommand.Execute(null);
        }
    }

    private void OpenAttendance() => _navigation.NavigateTo(ShellTab.Attendance);

    private void OpenFinance() => _navigation.NavigateTo(ShellTab.Finance);

    private void OpenSettings() => _navigation.NavigateTo(ShellTab.Settings);

    private void ToggleTheme() => ThemeToggleRequested?.Invoke(this, EventArgs.Empty);

    private void ShowShortcuts() => ShortcutsRequested?.Invoke(this, EventArgs.Empty);

    private void OnResultsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasResults));
        OnPropertyChanged(nameof(ShowNoResults));
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        RebuildCommands();

        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Placeholder));
        OnPropertyChanged(nameof(NoResultsText));
        OnPropertyChanged(nameof(HintText));

        if (IsOpen)
        {
            _pendingSearch = SearchAsync();
        }
    }

    public void Dispose()
    {
        _searchCts?.Cancel();
        _searchCts?.Dispose();
        _searchCts = null;
        Results.CollectionChanged -= OnResultsChanged;
        _localization.LanguageChanged -= OnLanguageChanged;
    }
}
