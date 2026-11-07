using Microsoft.Extensions.DependencyInjection;
using SchoolManagement.Desktop.ViewModels;
using SchoolManagement.Desktop.Views;

namespace SchoolManagement.Desktop.Services;

/// <summary>
/// Creates the activation window on demand. Kept out of the ViewModels so they
/// stay testable without WPF.
/// </summary>
public sealed class ActivationDialogService : IActivationDialogService
{
    private readonly IServiceProvider _services;

    public ActivationDialogService(IServiceProvider services)
    {
        _services = services;
    }

    public async Task<bool> ShowAsync()
    {
        var window = _services.GetRequiredService<ActivationWindow>();
        if (window.DataContext is ActivationViewModel viewModel)
        {
            await viewModel.InitializeAsync();
        }

        return window.ShowDialog() == true;
    }
}
