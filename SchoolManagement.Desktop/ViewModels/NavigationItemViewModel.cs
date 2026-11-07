using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using SchoolManagement.Application.Localization;
using SchoolManagement.Desktop.Services;

namespace SchoolManagement.Desktop.ViewModels;

/// <summary>
/// One destination in the side rail. A destination belongs to exactly one
/// logical group (Home, Academic, Finance, Communication, System); the group is
/// rendered as a small caption in expanded mode and hidden in compact mode.
/// </summary>
public partial class NavigationItemViewModel : ObservableObject
{
    public NavigationItemViewModel(
        ShellTab tab,
        string groupKey,
        string labelKey,
        string glyph,
        string label,
        string groupLabel)
    {
        Tab = tab;
        GroupKey = groupKey;
        LabelKey = labelKey;
        Glyph = glyph;
        _label = label;
        _groupLabel = groupLabel;
    }

    public ShellTab Tab { get; }

    /// <summary>Translation key of the group caption.</summary>
    public string GroupKey { get; }

    public string LabelKey { get; }

    /// <summary>Segoe Fluent Icons glyph. Icons are not translatable text.</summary>
    public string Glyph { get; }

    [ObservableProperty]
    private string _label;

    /// <summary>Localized group caption; the rail groups on this value.</summary>
    [ObservableProperty]
    private string _groupLabel;

    [ObservableProperty]
    private int _badgeCount;

    public bool HasBadge => BadgeCount > 0;

    /// <summary>Two digits plus an ellipsis, so the pill never grows the rail.</summary>
    public string BadgeText => BadgeCount > 99
        ? "99+"
        : BadgeCount.ToString(CultureInfo.InvariantCulture);

    public void RefreshLabel(ILocalizationService localization)
    {
        Label = localization.Tr(LabelKey);
        GroupLabel = localization.Tr(GroupKey);
    }

    partial void OnBadgeCountChanged(int value)
    {
        OnPropertyChanged(nameof(HasBadge));
        OnPropertyChanged(nameof(BadgeText));
    }
}
