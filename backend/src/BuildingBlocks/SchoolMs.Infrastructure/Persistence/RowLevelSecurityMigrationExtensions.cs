using Microsoft.EntityFrameworkCore.Migrations;

namespace SchoolMs.Infrastructure.Persistence;

/// <summary>Adds the tenant isolation policy to a table. Call it in the migration that creates every tenant-owned table.</summary>
public static class RowLevelSecurityMigrationExtensions
{
    public static void EnableTenantRowLevelSecurity(this MigrationBuilder migrationBuilder, string schema, string table)
    {
        var qualified = $"\"{schema}\".\"{table}\"";
        const string currentOrg = $"nullif(current_setting('{TenantRlsConnectionInterceptor.SettingName}', true), '')::uuid";

        migrationBuilder.Sql($"ALTER TABLE {qualified} ENABLE ROW LEVEL SECURITY;");
        // FORCE applies the policy to the table owner too; only roles with BYPASSRLS (the migrator) skip it.
        migrationBuilder.Sql($"ALTER TABLE {qualified} FORCE ROW LEVEL SECURITY;");
        migrationBuilder.Sql(
            $"CREATE POLICY tenant_isolation ON {qualified} " +
            $"USING (organisation_id = {currentOrg}) WITH CHECK (organisation_id = {currentOrg});");
    }

    public static void DisableTenantRowLevelSecurity(this MigrationBuilder migrationBuilder, string schema, string table)
    {
        var qualified = $"\"{schema}\".\"{table}\"";
        migrationBuilder.Sql($"DROP POLICY IF EXISTS tenant_isolation ON {qualified};");
        migrationBuilder.Sql($"ALTER TABLE {qualified} NO FORCE ROW LEVEL SECURITY;");
        migrationBuilder.Sql($"ALTER TABLE {qualified} DISABLE ROW LEVEL SECURITY;");
    }
}
