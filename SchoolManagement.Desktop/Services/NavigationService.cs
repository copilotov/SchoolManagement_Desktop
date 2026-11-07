using Microsoft.Extensions.DependencyInjection;
using SchoolManagement.Desktop.ViewModels;

namespace SchoolManagement.Desktop.Services;

/// <summary>Stable identifiers for the shell destinations.</summary>
public enum ShellTab
{
    Dashboard = 0,
    Students = 1,
    Groups = 2,
    Academics = 3,
    Finance = 4,
    Settings = 5,
    Attendance = 6,

    // Phase D2 - appended so the Phase 1-7 tab numbers never move.
    Homework = 7,
    Notifications = 8,
    Messages = 9,
}

public sealed class NavigationChangedEventArgs : EventArgs
{
    public NavigationChangedEventArgs(ShellTab tab, PageViewModel viewModel)
    {
        Tab = tab;
        ViewModel = viewModel;
    }

    public ShellTab Tab { get; }

    public PageViewModel ViewModel { get; }
}

public interface INavigationService
{
    ShellTab CurrentTab { get; }

    PageViewModel? CurrentViewModel { get; }

    event EventHandler<NavigationChangedEventArgs>? Navigated;

    /// <summary>Switches to a shell destination, reusing its ViewModel instance.</summary>
    void NavigateTo(ShellTab tab);

    /// <summary>Equivalent of the Flutter HomePage.goToTab(context, tab).</summary>
    void NavigateToTab(ShellTab tab);

    /// <summary>Opens a sub-screen ViewModel while keeping the current tab selected.</summary>
    TViewModel NavigateTo<TViewModel>()
        where TViewModel : PageViewModel;
}

/// <summary>
/// One ViewModel per screen. Instances are cached for the lifetime of the
/// window so each tab keeps its state when the user navigates away and back.
/// </summary>
public sealed class NavigationService : INavigationService
{
    private readonly IServiceProvider _services;
    private readonly Dictionary<Type, PageViewModel> _cache = new();

    public NavigationService(IServiceProvider services)
    {
        _services = services;
    }

    public ShellTab CurrentTab { get; private set; } = ShellTab.Dashboard;

    public PageViewModel? CurrentViewModel { get; private set; }

    public event EventHandler<NavigationChangedEventArgs>? Navigated;

    public void NavigateTo(ShellTab tab)
    {
        CurrentTab = tab;
        SetCurrent(GetOrCreate(GetViewModelType(tab)));
    }

    public void NavigateToTab(ShellTab tab) => NavigateTo(tab);

    public TViewModel NavigateTo<TViewModel>()
        where TViewModel : PageViewModel
    {
        var viewModel = GetOrCreate(typeof(TViewModel));
        SetCurrent(viewModel);
        return (TViewModel)viewModel;
    }

    private void SetCurrent(PageViewModel viewModel)
    {
        CurrentViewModel = viewModel;
        Navigated?.Invoke(this, new NavigationChangedEventArgs(CurrentTab, viewModel));
    }

    private PageViewModel GetOrCreate(Type viewModelType)
    {
        if (_cache.TryGetValue(viewModelType, out var cached))
        {
            return cached;
        }

        var resolved = _services.GetService(viewModelType) as PageViewModel
            ?? throw new InvalidOperationException(
                $"The page ViewModel '{viewModelType.FullName}' is not registered in the desktop container.");

        _cache[viewModelType] = resolved;
        return resolved;
    }

    /// <summary>
    /// The destination ViewModel type of a tab. Exposed so the shell can tell a
    /// top-level page from a sub-page (details, card, report) without duplicating
    /// the mapping.
    /// </summary>
    public static Type ViewModelTypeFor(ShellTab tab) => GetViewModelType(tab);

    private static Type GetViewModelType(ShellTab tab) => tab switch
    {
        ShellTab.Dashboard => typeof(DashboardViewModel),
        ShellTab.Students => typeof(StudentsViewModel),
        ShellTab.Groups => typeof(GroupsViewModel),
        ShellTab.Academics => typeof(AcademicsViewModel),
        ShellTab.Finance => typeof(FinanceViewModel),
        ShellTab.Settings => typeof(SettingsViewModel),
        ShellTab.Attendance => typeof(AttendanceViewModel),
        ShellTab.Homework => typeof(HomeworkViewModel),
        ShellTab.Notifications => typeof(NotificationsViewModel),
        ShellTab.Messages => typeof(MessagesViewModel),
        _ => throw new ArgumentOutOfRangeException(nameof(tab), tab, "Unknown shell tab."),
    };
}
