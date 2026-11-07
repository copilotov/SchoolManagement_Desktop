using System.Windows.Controls;
using SchoolManagement.Desktop.ViewModels;

namespace SchoolManagement.Desktop.Views;

public partial class GroupDetailsPage : UserControl
{
    public GroupDetailsPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is GroupDetailsViewModel viewModel)
        {
            await viewModel.LoadAsync();
        }
    }
}
