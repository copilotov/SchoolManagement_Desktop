using System.Windows.Controls;
using SchoolManagement.Desktop.Controls;
using SchoolManagement.Desktop.ViewModels;

namespace SchoolManagement.Desktop.Views;

public partial class GroupSchedulePage : UserControl
{
    public GroupSchedulePage()
    {
        InitializeComponent();
        Loaded += OnLoaded;

        // Phase D5 - the grid raises intents; the view model owns the rules.
        ScheduleGrid.SlotRequested += OnSlotRequested;
        ScheduleGrid.SlotActivated += OnSlotActivated;
        ScheduleGrid.SlotMoved += OnSlotMoved;
    }

    private async void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is GroupScheduleViewModel viewModel)
        {
            await viewModel.LoadAsync();
        }
    }

    private void OnSlotRequested(object? sender, WeekScheduleSlotRequestedEventArgs e)
    {
        if (DataContext is GroupScheduleViewModel viewModel)
        {
            viewModel.RequestSlot(e.DayIndex, e.Start);
        }
    }

    private void OnSlotActivated(object? sender, WeekScheduleSlotActivatedEventArgs e)
    {
        if (DataContext is GroupScheduleViewModel viewModel)
        {
            viewModel.ActivateSlot(e.Item);
        }
    }

    private async void OnSlotMoved(object? sender, WeekScheduleMovedEventArgs e)
    {
        if (DataContext is GroupScheduleViewModel viewModel)
        {
            await viewModel.MoveSlotAsync(e.Item, e.DayIndex, e.Start, e.End);
        }
    }
}
