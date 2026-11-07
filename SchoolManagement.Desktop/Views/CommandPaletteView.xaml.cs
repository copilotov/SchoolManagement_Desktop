using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace SchoolManagement.Desktop.Views;

/// <summary>
/// Phase D2 - the Ctrl+K overlay. The only code-behind is focus management:
/// opening the palette must put the caret in the search box so the user can type
/// immediately, and that is a view concern, not a ViewModel one.
/// </summary>
public partial class CommandPaletteView : UserControl
{
    public CommandPaletteView()
    {
        InitializeComponent();
        IsVisibleChanged += OnIsVisibleChanged;
    }

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is not true)
        {
            return;
        }

        // The visibility binding is applied during layout; focusing at
        // Background priority runs after the TextBox exists in the visual tree.
        Dispatcher.BeginInvoke(
            DispatcherPriority.Background,
            new Action(() =>
            {
                SearchBox.Focus();
                SearchBox.SelectAll();
            }));
    }
}
