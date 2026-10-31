using System.Collections.ObjectModel;
using System.Reflection;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using SchoolManagement.Application.Localization;
using SchoolManagement.Desktop.Services;

namespace SchoolManagement.Desktop.ViewModels;

public partial class ShellViewModel : ObservableObject, IDisposable
{
    private readonly INavigationService _navigation;
    private readonly ILocalizationService _localization;
    private bool _isSyncingSelection;

    public ShellViewModel(INavigationService navigation, ILocalizationService localization)
    {
        _navigation = navigation;
        _localization = localization;

        Items = new ObservableCollection<NavigationItemViewModel>
        {
            new(ShellTab.Dashboard, TranslationKeys.Menu.Dashboard, "\uE80F", localization.Tr(TranslationKeys.Menu.Dashboard)),
            new(ShellTab.Students, TranslationKeys.Menu.Students, "\uE716", localization.Tr(TranslationKeys.Menu.Students)),
            new(ShellTab.Groups, TranslationKeys.Menu.Groups, "\uE8F1", localization.Tr(TranslationKeys.Menu.Groups)),
            new(ShellTab.Finance, TranslationKeys.Menu.Finance, "\uE8C7", localization.Tr(TranslationKeys.Menu.Finance)),
            new(ShellTab.Settings, TranslationKeys.Menu.Settings, "\uE713", localization.Tr(TranslationKeys.Menu.Settings)),
        };

        FlowDirection = localization.IsRightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        AppVersion = Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "1.0.0";

        _navigation.Navigated += OnNavigated;
        _localization.LanguageChanged += OnLanguageChanged;

        _navigation.NavigateTo(ShellTab.Dashboard);
    }

    public ObservableCollection<NavigationItemViewModel> Items { get; }

    public string AppVersion { get; }

    [ObservableProperty]
    private NavigationItemViewModel? _selectedItem;

    [ObservableProperty]
    private PageViewModel? _currentViewModel;

    [ObservableProperty]
    private FlowDirection _flowDirection;

    partial void OnSelectedItemChanged(NavigationItemViewModel? value)
    {
        if (_isSyncingSelection || value is null)
        {
            return;
        }

        _navigation.NavigateTo(value.Tab);
    }

    private void OnNavigated(object? sender, NavigationChangedEventArgs e)
    {
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
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        foreach (var item in Items)
        {
            item.RefreshLabel(_localization);
        }

        FlowDirection = _localization.IsRightToLeft
            ? FlowDirection.RightToLeft
            : FlowDirection.LeftToRight;
    }

    public void Dispose()
    {
        _navigation.Navigated -= OnNavigated;
        _localization.LanguageChanged -= OnLanguageChanged;
    }
}
