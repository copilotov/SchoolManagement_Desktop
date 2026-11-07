using CommunityToolkit.Mvvm.ComponentModel;

namespace SchoolManagement.Desktop.ViewModels;

/// <summary>
/// Host for the four academic tabs. Each child ViewModel is a singleton so its
/// search, sort and editor state survives tab switches, exactly like the shell
/// keeps one instance per destination.
/// </summary>
public partial class AcademicsViewModel : PageViewModel, IDisposable
{
    public AcademicsViewModel(
        AcademicLevelsViewModel levels,
        AcademicGradesViewModel grades,
        AcademicSubjectsViewModel subjects,
        AcademicTeachersViewModel teachers)
    {
        Levels = levels ?? throw new ArgumentNullException(nameof(levels));
        Grades = grades ?? throw new ArgumentNullException(nameof(grades));
        Subjects = subjects ?? throw new ArgumentNullException(nameof(subjects));
        Teachers = teachers ?? throw new ArgumentNullException(nameof(teachers));
    }

    public AcademicLevelsViewModel Levels { get; }

    public AcademicGradesViewModel Grades { get; }

    public AcademicSubjectsViewModel Subjects { get; }

    public AcademicTeachersViewModel Teachers { get; }

    [ObservableProperty]
    private int _selectedTabIndex;

    /// <summary>Loads the selected tab once. Called by the page on first display.</summary>
    public Task EnsureLoadedAsync() => SelectTabAsync(SelectedTabIndex);

    /// <summary>Switches tab and lazily loads its data the first time.</summary>
    public Task SelectTabAsync(int index)
    {
        SelectedTabIndex = index;

        return index switch
        {
            1 => Grades.EnsureLoadedAsync(),
            2 => Subjects.EnsureLoadedAsync(),
            3 => Teachers.EnsureLoadedAsync(),
            _ => Levels.EnsureLoadedAsync(),
        };
    }

    public void Dispose()
    {
        Levels.Dispose();
        Grades.Dispose();
        Subjects.Dispose();
        Teachers.Dispose();
        GC.SuppressFinalize(this);
    }
}
