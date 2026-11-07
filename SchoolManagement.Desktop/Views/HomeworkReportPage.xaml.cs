using System.Windows.Controls;
using SchoolManagement.Desktop.ViewModels;

namespace SchoolManagement.Desktop.Views;

public partial class HomeworkReportPage : UserControl
{
    public HomeworkReportPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is HomeworkViewModel viewModel)
        {
            await viewModel.EnsureLoadedAsync();
        }
    }
}
