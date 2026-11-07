using System.Windows.Controls;
using SchoolManagement.Desktop.ViewModels;

namespace SchoolManagement.Desktop.Views;

public partial class AttendancePage : UserControl
{
    public AttendancePage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is AttendanceViewModel viewModel)
        {
            await viewModel.EnsureLoadedAsync();
        }
    }
}
