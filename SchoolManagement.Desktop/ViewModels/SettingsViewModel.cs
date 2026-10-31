using System.Collections.ObjectModel;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using SchoolManagement.Application.Localization;
using SchoolManagement.Application.Settings;
using SchoolManagement.Desktop.Services;

namespace SchoolManagement.Desktop.ViewModels;

public partial class SettingsViewModel : PageViewModel, IDisposable
{
    private readonly ILocalizationService _localization;
    private readonly IThemeService _themeService;
    private readonly IAppSettingsStore _settingsStore;
    private bool _isInitializing;

    public SettingsViewModel(
        ILocalizationService localization,
        IThemeService themeService,
        IAppSettingsStore settingsStore)
    {
        _localization = localization;
        _themeService = themeService;
        _settingsStore = settingsStore;

        Languages = new ObservableCollection<LanguageOption>
        {
            new("ar", TranslationKeys.Settings.Arabic, localization.Tr(TranslationKeys.Settings.Arabic)),
            new("en", TranslationKeys.Settings.English, localization.Tr(TranslationKeys.Settings.English)),
            new("fr", TranslationKeys.Settings.French, localization.Tr(TranslationKeys.Settings.French)),
        };

        _isInitializing = true;
        _selectedLanguage = Languages.FirstOrDefault(
            option => string.Equals(option.Code, localization.CurrentLanguage, StringComparison.OrdinalIgnoreCase));
        _isDarkTheme = themeService.CurrentTheme == AppTheme.Dark;
        _isInitializing = false;

        AppVersion = Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "1.0.0";

        _localization.LanguageChanged += OnLanguageChanged;
    }

    public ObservableCollection<LanguageOption> Languages { get; }

    public string AppVersion { get; }

    [ObservableProperty]
    private LanguageOption? _selectedLanguage;

    [ObservableProperty]
    private bool _isDarkTheme;

    [ObservableProperty]
    private string? _statusMessage;

    partial void OnSelectedLanguageChanged(LanguageOption? value)
    {
        if (_isInitializing || value is null)
        {
            return;
        }

        _ = ChangeLanguageAsync(value.Code);
    }

    partial void OnIsDarkThemeChanged(bool value)
    {
        if (_isInitializing)
        {
            return;
        }

        _themeService.Apply(value ? AppTheme.Dark : AppTheme.Light);
        _ = SaveAsync();
    }

    private async Task ChangeLanguageAsync(string language)
    {
        var changed = await _localization.SetLanguageAsync(language);
        if (!changed)
        {
            StatusMessage = _localization.Tr(TranslationKeys.Errors.Unexpected);
            return;
        }

        await SaveAsync();
    }

    private async Task SaveAsync()
    {
        var saved = await _settingsStore.SaveAsync(
            new AppSettings(_localization.CurrentLanguage, _themeService.CurrentTheme));

        StatusMessage = _localization.Tr(
            saved ? TranslationKeys.Messages.SavedSuccessfully : TranslationKeys.Errors.SaveError);
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        foreach (var option in Languages)
        {
            option.Refresh(_localization.Tr(option.NameKey));
        }

        StatusMessage = null;
    }

    public void Dispose()
    {
        _localization.LanguageChanged -= OnLanguageChanged;
    }
}
