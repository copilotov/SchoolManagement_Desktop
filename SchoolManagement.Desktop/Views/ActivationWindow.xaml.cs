using System.Windows;
using SchoolManagement.Desktop.ViewModels;

namespace SchoolManagement.Desktop.Views;

public partial class ActivationWindow : Window
{
    private readonly ActivationViewModel _viewModel;

    public ActivationWindow(ActivationViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;

        viewModel.Activated += OnActivated;
        Closed += OnClosed;
    }

    private void OnActivated(object? sender, EventArgs e)
    {
        try
        {
            DialogResult = true;
        }
        catch (InvalidOperationException)
        {
            // Shown non-modally (tests/tools): close explicitly.
            Close();
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _viewModel.Activated -= OnActivated;
        _viewModel.Dispose();
    }
}
