using System.Windows;
using SchoolManagement.Desktop.ViewModels;

namespace SchoolManagement.Desktop.Views;

/// <summary>
/// Phase D1 - debug-only design gallery window. Opened with F12 from the
/// shell; it owns the lifetime of its ViewModel and disposes it on close so
/// the theme/language subscriptions never leak across openings.
/// </summary>
public partial class DesignGalleryWindow : Window
{
    private readonly DesignGalleryViewModel _viewModel;

    public DesignGalleryWindow(DesignGalleryViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;

        viewModel.CloseRequested += OnCloseRequested;
        Closed += OnClosed;
    }

    private void OnCloseRequested(object? sender, EventArgs e) => Close();

    private void OnClosed(object? sender, EventArgs e)
    {
        _viewModel.CloseRequested -= OnCloseRequested;
        Closed -= OnClosed;
        _viewModel.Dispose();
    }
}
