using System.Windows.Controls;

namespace SchoolManagement.Desktop.Views;

/// <summary>
/// Phase D2 - grouped side navigation. All state lives in
/// <c>ShellViewModel</c>; this control only renders it, so collapsing the rail
/// or switching language never recreates the page that is on screen.
/// </summary>
public partial class NavRailView : UserControl
{
    public NavRailView()
    {
        InitializeComponent();
    }
}
