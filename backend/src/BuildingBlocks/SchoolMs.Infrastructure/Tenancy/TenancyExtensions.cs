using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using SchoolMs.SharedKernel;

namespace SchoolMs.Infrastructure.Tenancy;

public static class TenancyExtensions
{
    public static IServiceCollection AddTenantResolution(this IServiceCollection services, string baseDomain)
    {
        services.AddMemoryCache();
        services.AddSingleton(new TenantResolutionOptions(baseDomain.Trim().ToLowerInvariant()));
        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
        return services;
    }

    public static IApplicationBuilder UseTenantResolution(this IApplicationBuilder app) =>
        app.UseMiddleware<TenantResolutionMiddleware>();
}
