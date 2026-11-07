using System.Windows.Controls;
using SchoolManagement.Desktop.ViewModels;

namespace SchoolManagement.Desktop.Views;

public partial class EnrollmentPage : UserControl
{
    public EnrollmentPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is EnrollmentViewModel viewModel)
        {
            await viewModel.LoadAsync();
        }
    }
}
