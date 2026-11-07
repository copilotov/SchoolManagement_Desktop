using System.Windows.Controls;
using SchoolManagement.Desktop.ViewModels;

namespace SchoolManagement.Desktop.Views;

public partial class AcademicsPage : UserControl
{
    private bool _isSelecting;

    public AcademicsPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is AcademicsViewModel viewModel)
        {
            await viewModel.EnsureLoadedAsync();
        }
    }

    private async void OnTabSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isSelecting || DataContext is not AcademicsViewModel viewModel)
        {
            return;
        }

        if (!ReferenceEquals(e.OriginalSource, sender))
        {
            return;
        }

        _isSelecting = true;
        try
        {
            await viewModel.SelectTabAsync(((TabControl)sender).SelectedIndex);
        }
        finally
        {
            _isSelecting = false;
        }
    }
}
