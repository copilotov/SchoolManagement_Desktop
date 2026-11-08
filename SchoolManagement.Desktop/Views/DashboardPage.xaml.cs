using System.Windows;
using System.Windows.Controls;
using SchoolManagement.Desktop.ViewModels;

namespace SchoolManagement.Desktop.Views;

/// <summary>
/// Phase D4 - the dashboard loads its single read snapshot on first display and
/// feeds the real content width back to the ViewModel, which owns the responsive
/// column counts. Both handlers tolerate a null DataContext so the view can be
/// instantiated by the design tools without a container.
/// </summary>
public partial class DashboardPage : UserControl
{
    public DashboardPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        SizeChanged += OnSizeChanged;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is not DashboardViewModel viewModel)
        {
            return;
        }

        viewModel.UpdateWidth(ActualWidth);
        await viewModel.EnsureLoadedAsync();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (DataContext is DashboardViewModel viewModel)
        {
            viewModel.UpdateWidth(e.NewSize.Width);
        }
    }
}
