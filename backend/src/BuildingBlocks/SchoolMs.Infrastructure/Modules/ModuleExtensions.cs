using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SchoolMs.Infrastructure.Modules;

public static class ModuleExtensions
{
    public static IServiceCollection AddModules(
        this IServiceCollection services,
        IConfiguration configuration,
        IReadOnlyList<IModule> modules)
    {
        var duplicate = modules.GroupBy(m => m.Name, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidOperationException($"Module name '{duplicate.Key}' is registered more than once.");
        }

        foreach (var module in modules)
        {
            module.AddServices(services, configuration);
        }

        return services;
    }

    /// <summary>Maps each module's endpoints into its own route group, e.g. <c>/api/v1/students</c>.</summary>
    public static IEndpointRouteBuilder MapModules(this IEndpointRouteBuilder api, IReadOnlyList<IModule> modules)
    {
        foreach (var module in modules)
        {
            var group = api.MapGroup($"/{module.Name}").WithTags(module.Name);
            module.MapEndpoints(group);
        }

        return api;
    }
}
