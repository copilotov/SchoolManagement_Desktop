using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolManagement.Application.Licensing;
using SchoolManagement.Application.Localization;
using SchoolManagement.Application.Repositories;
using SchoolManagement.Application.Settings;
using SchoolManagement.Desktop.Services;

namespace SchoolManagement.Desktop.ViewModels;

/// <summary>One logical block of the side rail, in display order.</summary>
public sealed record NavigationGroup(
    string Key,
    string Label,
    IReadOnlyList<NavigationItemViewModel> Items);

/// <summary>
/// Phase D2 - the shell ViewModel. It owns the grouped navigation model, the
/// collapse state, the top-bar breadcrumb, the Ctrl+K palette, the theme and
/// language toggles and the unread badge. It never writes to the database: the
/// palette navigates to existing screens, and every write still goes through
/// the page's own license-guarded use case.
/// </summary>
public partial class ShellViewModel : ObservableObject, IDisposable
{
    /// <summary>Below this width the rail collapses to icons automatically.</summary>
    public const double AutoCollapseWidth = 1200;

    public const double ExpandedNavWidth = 240;
    public const double CompactNavWidth = 64;

    private readonly INavigationService _navigation;
    private readonly ILocalizationService _localization;
    private readonly IThemeService _themeService;
    private readonly IAppSettingsStore _settingsStore;
    private readonly INotificationRepository _notifications;
    private readonly IReferenceDataRepository _referenceData;
    private readonly IToastService _toasts;

    private bool _isSyncingSelection;
    private bool _isAutoCollapsed;
    private double _lastWidth = AutoCollapseWidth;

    public ShellViewModel(
        INavigationService navigation,
        ILocalizationService localization,
        LicenseViewModel license,
        IThemeService themeService,
        IAppSettingsStore settingsStore,
        INotificationRepository notifications,
        IReferenceDataRepository referenceData,
        IToastService toasts,
        CommandPaletteViewModel commandPalette)
    {
        _navigation = navigation;
        _localization = localization;
        _themeService = themeService;
        _settingsStore = settingsStore;
        _notifications = notifications;
        _referenceData = referenceData;
        _toasts = toasts;
        CommandPalette = commandPalette;
        License = license;

        Items = new ObservableCollection<NavigationItemViewModel>
        {
            Item(ShellTab.Dashboard, TranslationKeys.Nav.GroupHome, TranslationKeys.Menu.Dashboard, "Icon.Dashboard"),
            Item(ShellTab.Students, TranslationKeys.Nav.GroupAcademic, TranslationKeys.Menu.Students, "Icon.Students"),
            Item(ShellTab.Groups, TranslationKeys.Nav.GroupAcademic, TranslationKeys.Menu.Groups, "Icon.Groups"),
            // Phase 4 keeps its own destination (levels, grades, subjects, teachers).
            // It sits in the Academic group so the Phase D2 grouping does not make
            // an existing screen unreachable.
            Item(ShellTab.Academics, TranslationKeys.Nav.GroupAcademic, TranslationKeys.Menu.Academics, "Icon.Academics"),
            Item(ShellTab.Attendance, TranslationKeys.Nav.GroupAcademic, TranslationKeys.Menu.Attendance, "Icon.Attendance"),
            Item(ShellTab.Homework, TranslationKeys.Nav.GroupAcademic, TranslationKeys.Menu.Homework, "Icon.Book"),
            Item(ShellTab.Finance, TranslationKeys.Nav.GroupFinance, TranslationKeys.Menu.Finance, "Icon.Finance"),
            Item(ShellTab.Notifications, TranslationKeys.Nav.GroupCommunication, TranslationKeys.Menu.Notifications, "Icon.Bell"),
            Item(ShellTab.Messages, TranslationKeys.Nav.GroupCommunication, TranslationKeys.Menu.Messages, "Icon.Mail"),
            Item(ShellTab.Settings, TranslationKeys.Nav.GroupSystem, TranslationKeys.Menu.Settings, "Icon.Settings"),
        };

        // The rail groups on the localized caption. Group order follows the item
        // order above, so Home -> Academic -> Finance -> Communication -> System
        // is stable in every language.
        ItemsView = new ListCollectionView(Items);
        ItemsView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(NavigationItemViewModel.GroupLabel)));
        Groups = BuildGroups();

        FlowDirection = localization.IsRightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        AppVersion = Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "1.0.0";
        SchoolName = localization.Tr(TranslationKeys.App.Name);
        IsDarkTheme = themeService.CurrentTheme == AppTheme.Dark;

        CommandPalette.ThemeToggleRequested += OnPaletteThemeToggleRequested;
        CommandPalette.ShortcutsRequested += OnPaletteShortcutsRequested;
        _navigation.Navigated += OnNavigated;
        _localization.LanguageChanged += OnLanguageChanged;
        _themeService.ThemeChanged += OnThemeChanged;

        _navigation.NavigateTo(ShellTab.Dashboard);

        _ = LoadSchoolNameAsync();
        _ = RefreshUnreadAsync();
    }

    public ObservableCollection<NavigationItemViewModel> Items { get; }

    /// <summary>Grouped view bound by the rail; the only place grouping is defined.</summary>
    public ICollectionView ItemsView { get; }

    /// <summary>Grouped model used by tests and by the compact rail header logic.</summary>
    [ObservableProperty]
    private IReadOnlyList<NavigationGroup> _groups;

    public LicenseViewModel License { get; }

    public CommandPaletteViewModel CommandPalette { get; }

    public IToastService Toasts => _toasts;

    public string AppVersion { get; }

    [ObservableProperty]
    private string _schoolName;

    [ObservableProperty]
    private int _unreadCount;

    public bool HasUnread => UnreadCount > 0;

    [ObservableProperty]
    private NavigationItemViewModel? _selectedItem;

    [ObservableProperty]
    private PageViewModel? _currentViewModel;

    [ObservableProperty]
    private FlowDirection _flowDirection;

    [ObservableProperty]
    private ShellTab _currentTab = ShellTab.Dashboard;

    /// <summary>True when the rail is showing icons only.</summary>
    [ObservableProperty]
    private bool _isNavCollapsed;

    /// <summary>User intent: the rail is expanded unless the window is narrow.</summary>
    [ObservableProperty]
    private bool _isNavExpanded = true;

    [ObservableProperty]
    private bool _isDarkTheme;

    public double NavWidth => IsNavCollapsed ? CompactNavWidth : ExpandedNavWidth;

    /// <summary>GridLength form for the rail column; a double cannot bind to Width.</summary>
    public GridLength NavColumnWidth => new(NavWidth);

    public string ThemeGlyph => IsDarkTheme ? "Icon.ThemeLight" : "Icon.ThemeDark";

    /// <summary>ISO code shown in the footer ("AR", "EN", "FR"), not translated text.</summary>
    public string LanguageBadge => _localization.CurrentLanguage.ToUpperInvariant();

    public string CurrentPageTitle =>
        Items.FirstOrDefault(item => item.Tab == CurrentTab)?.Label ?? string.Empty;

    public string CurrentGroupLabel =>
        Items.FirstOrDefault(item => item.Tab == CurrentTab)?.GroupLabel ?? string.Empty;

    /// <summary>True when a sub-page (details, card, report, …) is open over its tab.</summary>
    public bool CanGoBack => CurrentViewModel is { } viewModel
        && viewModel.GetType() != NavigationService.ViewModelTypeFor(CurrentTab);

    /// <summary>Called by the shell window whenever it regains focus.</summary>
    public Task RefreshLicenseAsync(CancellationToken cancellationToken = default) =>
        License.RefreshAsync(cancellationToken);

    /// <summary>Called by the shell window on activation and after navigation.</summary>
    public async Task RefreshUnreadAsync(CancellationToken cancellationToken = default)
    {
        var result = await _notifications.CountUnreadAsync(cancellationToken).ConfigureAwait(true);
        if (!result.IsSuccess)
        {
            return;
        }

        UnreadCount = result.Value;
        var notifications = Items.FirstOrDefault(item => item.Tab == ShellTab.Notifications);
        if (notifications is not null)
        {
            notifications.BadgeCount = result.Value;
        }
    }

    /// <summary>
    /// Applies the responsive rule: below <see cref="AutoCollapseWidth"/> the
    /// rail collapses to icons. It only reacts when the width crosses the
    /// threshold, so a manual expand is not undone by a one-pixel resize.
    /// </summary>
    public void UpdateWindowWidth(double width)
    {
        var wasNarrow = _lastWidth < AutoCollapseWidth;
        var isNarrow = width < AutoCollapseWidth;
        _lastWidth = width;

        if (wasNarrow == isNarrow)
        {
            return;
        }

        _isAutoCollapsed = isNarrow;
        ApplyNavState();
    }

    [RelayCommand]
    private void ToggleNav()
    {
        // Toggle what the user actually sees, not the remembered intent: after an
        // automatic collapse at a narrow width, the first click must expand.
        var expand = IsNavCollapsed;
        IsNavExpanded = expand;

        if (expand)
        {
            _isAutoCollapsed = false;
        }

        ApplyNavState();
    }

    [RelayCommand]
    private void GoBack()
    {
        if (CanGoBack)
        {
            _navigation.NavigateTo(CurrentTab);
        }
    }

    [RelayCommand]
    private void OpenCommandPalette() => CommandPalette.Open();

    [RelayCommand]
    private void OpenShortcuts() => ShortcutsRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>Top-bar "new" action; the focused page decides what "new" means.</summary>
    [RelayCommand]
    private void ExecuteNewShortcut() => ExecuteShortcut(ShortcutAction.New);

    [RelayCommand]
    private async Task ToggleThemeAsync()
    {
        var next = IsDarkTheme ? AppTheme.Light : AppTheme.Dark;
        _themeService.Apply(next);
        await SaveSettingsAsync();
        _toasts.Show(_localization.Tr(TranslationKeys.Toast.ThemeChanged));
    }

    [RelayCommand]
    private async Task CycleLanguageAsync()
    {
        string[] order = ["ar", "en", "fr"];
        var current = Array.FindIndex(
            order,
            code => string.Equals(code, _localization.CurrentLanguage, StringComparison.OrdinalIgnoreCase));
        var next = order[(current + 1 + order.Length) % order.Length];

        if (!await _localization.SetLanguageAsync(next))
        {
            return;
        }

        await SaveSettingsAsync();
        _toasts.Show(_localization.Tr(TranslationKeys.Toast.LanguageChanged));
    }

    [RelayCommand]
    private void NavigateToTab(ShellTab tab) => _navigation.NavigateTo(tab);

    /// <summary>Raised when the shortcuts window must open; the window owns the WPF dialog.</summary>
    public event EventHandler? ShortcutsRequested;

    /// <summary>Raised when the license banner asks to open the activation window.</summary>
    public Task RenewLicenseAsync() => License.RenewInteractiveAsync();

    /// <summary>
    /// Executes a keyboard shortcut. The window only translates a key gesture
    /// into an action, so the behaviour is unit-testable without a message loop.
    /// </summary>
    public void ExecuteShortcut(ShortcutAction action, ShellTab? tab = null)
    {
        switch (action)
        {
            case ShortcutAction.New:
                InvokeNewOnCurrentPage();
                break;

            case ShortcutAction.Search:
            case ShortcutAction.CommandPalette:
                CommandPalette.Open();
                break;

            case ShortcutAction.ToggleNav:
                ToggleNavCommand.Execute(null);
                break;

            case ShortcutAction.Back:
                GoBackCommand.Execute(null);
                break;

            case ShortcutAction.Help:
                OpenShortcutsCommand.Execute(null);
                break;

            case ShortcutAction.Close:
                CommandPalette.Close();
                break;

            case ShortcutAction.Navigate when tab is { } destination:
                _navigation.NavigateTo(destination);
                break;

            case ShortcutAction.Save:
            case ShortcutAction.Print:
                // Page-owned: the focused screen handles its own save/print so the
                // shell never runs a write behind the user's back.
                break;
        }
    }

    private void InvokeNewOnCurrentPage()
    {
        switch (CurrentViewModel)
        {
            case StudentsViewModel students:
                students.AddCommand.Execute(null);
                break;

            case GroupsViewModel groups:
                groups.AddCommand.Execute(null);
                break;

            case HomeworkViewModel homeworks:
                homeworks.AddCommand.Execute(null);
                break;

            case AttendanceViewModel attendance:
                attendance.OpenExtraSessionCommand.Execute(null);
                break;
        }
    }

    partial void OnSelectedItemChanged(NavigationItemViewModel? value)
    {
        if (_isSyncingSelection || value is null)
        {
            return;
        }

        _navigation.NavigateTo(value.Tab);
    }

    partial void OnIsNavCollapsedChanged(bool value)
    {
        OnPropertyChanged(nameof(NavWidth));
        OnPropertyChanged(nameof(NavColumnWidth));
    }

    partial void OnIsDarkThemeChanged(bool value) => OnPropertyChanged(nameof(ThemeGlyph));

    partial void OnUnreadCountChanged(int value) => OnPropertyChanged(nameof(HasUnread));

    partial void OnCurrentViewModelChanged(PageViewModel? value) => OnPropertyChanged(nameof(CanGoBack));

    private void OnNavigated(object? sender, NavigationChangedEventArgs e)
    {
        CurrentTab = e.Tab;
        CurrentViewModel = e.ViewModel;

        _isSyncingSelection = true;
        try
        {
            SelectedItem = Items.FirstOrDefault(item => item.Tab == e.Tab) ?? SelectedItem;
        }
        finally
        {
            _isSyncingSelection = false;
        }

        OnPropertyChanged(nameof(CurrentPageTitle));
        OnPropertyChanged(nameof(CurrentGroupLabel));
        OnPropertyChanged(nameof(CanGoBack));

        _ = RefreshUnreadAsync();
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        foreach (var item in Items)
        {
            item.RefreshLabel(_localization);
        }

        ItemsView.Refresh();
        Groups = BuildGroups();

        FlowDirection = _localization.IsRightToLeft
            ? FlowDirection.RightToLeft
            : FlowDirection.LeftToRight;

        OnPropertyChanged(nameof(CurrentPageTitle));
        OnPropertyChanged(nameof(CurrentGroupLabel));
        OnPropertyChanged(nameof(LanguageBadge));
    }

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        IsDarkTheme = _themeService.CurrentTheme == AppTheme.Dark;
    }

    private void OnPaletteThemeToggleRequested(object? sender, EventArgs e) =>
        ToggleThemeCommand.Execute(null);

    private void OnPaletteShortcutsRequested(object? sender, EventArgs e) =>
        OpenShortcutsCommand.Execute(null);

    private async Task SaveSettingsAsync()
    {
        await _settingsStore.SaveAsync(
            new AppSettings(_localization.CurrentLanguage, _themeService.CurrentTheme));
    }

    private async Task LoadSchoolNameAsync()
    {
        var result = await _referenceData.GetSchoolNameAsync().ConfigureAwait(true);
        if (result.IsSuccess && !string.IsNullOrWhiteSpace(result.Value))
        {
            SchoolName = result.Value;
        }
    }

    private void ApplyNavState()
    {
        var collapsed = _isAutoCollapsed || !IsNavExpanded;
        if (IsNavCollapsed != collapsed)
        {
            IsNavCollapsed = collapsed;
        }
    }

    private NavigationItemViewModel Item(ShellTab tab, string groupKey, string labelKey, string glyph) =>
        new(
            tab,
            groupKey,
            labelKey,
            Glyph(glyph),
            _localization.Tr(labelKey),
            _localization.Tr(groupKey));

    /// <summary>Icons are glyph strings from Icons.xaml, not translatable text.</summary>
    private static string Glyph(string key) =>
        System.Windows.Application.Current?.TryFindResource(key) as string ?? string.Empty;

    private IReadOnlyList<NavigationGroup> BuildGroups() =>
        Items
            .GroupBy(item => item.GroupKey, StringComparer.Ordinal)
            .Select(group => new NavigationGroup(
                group.Key,
                group.First().GroupLabel,
                group.ToArray()))
            .ToArray();

    public void Dispose()
    {
        CommandPalette.ThemeToggleRequested -= OnPaletteThemeToggleRequested;
        CommandPalette.ShortcutsRequested -= OnPaletteShortcutsRequested;
        _navigation.Navigated -= OnNavigated;
        _localization.LanguageChanged -= OnLanguageChanged;
        _themeService.ThemeChanged -= OnThemeChanged;
        CommandPalette.Dispose();
    }
}
