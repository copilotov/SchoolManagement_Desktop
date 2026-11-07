using System.Windows;
using Wpf.Ui.Controls;
using SchoolManagement.Desktop.Localization;

namespace SchoolManagement.Desktop.Views;

/// <summary>
/// Phase D2 - F1 shortcut reference. It is a passive window: no ViewModel, no
/// state, only localized key/action pairs, so it always matches the shortcuts
/// the shell actually binds.
/// </summary>
public partial class ShortcutsWindow : FluentWindow
{
    public ShortcutsWindow()
    {
        InitializeComponent();

        FlowDirection = LocalizationProvider.Current?.IsRightToLeft == true
            ? FlowDirection.RightToLeft
            : FlowDirection.LeftToRight;
    }
}
