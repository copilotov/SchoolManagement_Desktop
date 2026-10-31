using Microsoft.Extensions.DependencyInjection;

namespace SchoolManagement.Application.DependencyInjection;

public static class ApplicationServiceCollectionExtensions
{
    /// <summary>
    /// Registers application use cases. Repositories are registered by
    /// Infrastructure, and ViewModels by Desktop; this method is the single
    /// place where use cases will be added phase by phase.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services;
    }
}
