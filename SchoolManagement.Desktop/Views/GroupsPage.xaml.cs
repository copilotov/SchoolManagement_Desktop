using System.Windows.Controls;
using SchoolManagement.Desktop.ViewModels;

namespace SchoolManagement.Desktop.Views;

public partial class GroupsPage : UserControl
{
    public GroupsPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is GroupsViewModel viewModel)
        {
            await viewModel.EnsureLoadedAsync();
        }
    }
}
