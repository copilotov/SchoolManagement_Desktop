using System.Windows.Controls;
using SchoolManagement.Desktop.ViewModels;

namespace SchoolManagement.Desktop.Views;

public partial class GroupSchedulePage : UserControl
{
    public GroupSchedulePage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is GroupScheduleViewModel viewModel)
        {
            await viewModel.LoadAsync();
        }
    }
}
