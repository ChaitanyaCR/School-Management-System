using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SchoolMs.Infrastructure.Modules;

namespace SchoolMs.Modules.ModuleName;

public sealed class ModuleNameModule : IModule
{
    public string Name => "module-slug";

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
    }
}
