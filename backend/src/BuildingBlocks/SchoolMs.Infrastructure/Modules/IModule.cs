using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SchoolMs.Infrastructure.Modules;

/// <summary>
/// Entry point of a business module. The host registers modules explicitly (no assembly scanning),
/// so the list in the API project is the single place that shows what the application contains.
/// </summary>
public interface IModule
{
    /// <summary>Short name, used for the module's PostgreSQL schema, route segment and logs (e.g. "students").</summary>
    string Name { get; }

    void AddServices(IServiceCollection services, IConfiguration configuration);

    /// <summary>Maps endpoints under <c>/api/v1/{Name}</c>.</summary>
    void MapEndpoints(IEndpointRouteBuilder endpoints);
}
