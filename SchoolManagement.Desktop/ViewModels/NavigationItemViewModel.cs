using CommunityToolkit.Mvvm.ComponentModel;
using SchoolManagement.Application.Localization;
using SchoolManagement.Desktop.Services;

namespace SchoolManagement.Desktop.ViewModels;

public partial class NavigationItemViewModel : ObservableObject
{
    public NavigationItemViewModel(ShellTab tab, string labelKey, string glyph, string label)
    {
        Tab = tab;
        LabelKey = labelKey;
        Glyph = glyph;
        _label = label;
    }

    public ShellTab Tab { get; }

    public string LabelKey { get; }

    /// <summary>Segoe Fluent Icons glyph. Icons are not translatable text.</summary>
    public string Glyph { get; }

    [ObservableProperty]
    private string _label;

    public void RefreshLabel(ILocalizationService localization)
    {
        Label = localization.Tr(LabelKey);
    }
}
