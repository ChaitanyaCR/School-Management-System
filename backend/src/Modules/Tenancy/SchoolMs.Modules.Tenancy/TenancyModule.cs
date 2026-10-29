using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SchoolMs.Infrastructure.Modules;
using SchoolMs.Infrastructure.Persistence;
using SchoolMs.Infrastructure.Tenancy;
using SchoolMs.Modules.Tenancy.Endpoints;
using SchoolMs.Modules.Tenancy.Infrastructure;

namespace SchoolMs.Modules.Tenancy;

/// <summary>Organisations (tenants) and their branches (PLT-01…09).</summary>
public sealed class TenancyModule : IModule
{
    public string Name => "tenancy";

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<TenancyDbContext>(TenancyDbContext.SchemaName);
        services.AddScoped<ITenantLookup, TenantLookup>();
        services.AddScoped<IDataSeeder, DemoDataSeeder>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapCurrentOrganisation();
    }
}
