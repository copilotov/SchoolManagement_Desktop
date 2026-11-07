using System.Windows.Controls;
using SchoolManagement.Desktop.ViewModels;

namespace SchoolManagement.Desktop.Views;

public partial class AttendanceReportPage : UserControl
{
    public AttendanceReportPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is AttendanceReportViewModel viewModel)
        {
            await viewModel.EnsureLoadedAsync();
        }
    }
}
