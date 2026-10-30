using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using SchoolMs.Infrastructure.Http;
using SchoolMs.Modules.Tenancy.Infrastructure;
using SchoolMs.SharedKernel;

namespace SchoolMs.Modules.Tenancy.Endpoints;

/// <summary>The organisation served at this address; shown before login (e.g. on the sign-in page).</summary>
public sealed record CurrentOrganisationResponse(string Slug, string Name);

internal static class CurrentOrganisationEndpoint
{
    private static readonly Error NoTenant =
        Error.NotFound("tenant.none", "This address does not belong to a school.");

    public static void MapCurrentOrganisation(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapGet("/current", HandleAsync)
            .AllowAnonymous()
            .WithName("GetCurrentOrganisation")
            .WithSummary("The organisation for the current subdomain.")
            .Produces<CurrentOrganisationResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static async Task<IResult> HandleAsync(ITenantContext tenant, TenancyDbContext db, CancellationToken cancellationToken)
    {
        if (tenant.OrganisationId is not { } organisationId)
        {
            return NoTenant.ToProblem();
        }

        var organisation = await db.Organisations
            .AsNoTracking()
            .Where(o => o.Id == organisationId)
            .Select(o => new CurrentOrganisationResponse(o.Slug, o.Name))
            .SingleAsync(cancellationToken);

        return TypedResults.Ok(organisation);
    }
}
