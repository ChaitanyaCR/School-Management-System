using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using SchoolMs.SharedKernel;

namespace SchoolMs.Infrastructure.Persistence;

/// <summary>
/// Sets <c>app.current_org</c> on every connection EF Core opens, which the PostgreSQL RLS policies read.
/// An empty value (no tenant) matches no rows. Npgsql resets session state when a connection returns
/// to the pool, so the setting never leaks between requests.
/// </summary>
internal sealed class TenantRlsConnectionInterceptor(ITenantContext tenantContext) : DbConnectionInterceptor
{
    public const string SettingName = "app.current_org";

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        using var command = CreateCommand(connection);
        command.ExecuteNonQuery();
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        await using var command = CreateCommand(connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private NpgsqlCommand CreateCommand(DbConnection connection)
    {
        var command = (NpgsqlCommand)connection.CreateCommand();
        command.CommandText = $"SELECT set_config('{SettingName}', @org, false)";
        command.Parameters.AddWithValue("org", tenantContext.OrganisationId?.ToString() ?? string.Empty);
        return command;
    }
}
