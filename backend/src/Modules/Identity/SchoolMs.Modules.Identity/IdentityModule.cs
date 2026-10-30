using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SchoolMs.Infrastructure.Modules;
using SchoolMs.Infrastructure.Persistence;
using SchoolMs.Infrastructure.Security;
using SchoolMs.Modules.Identity.Application;
using SchoolMs.Modules.Identity.Domain;
using SchoolMs.Modules.Identity.Endpoints;
using SchoolMs.Modules.Identity.Infrastructure;
using SchoolMs.Modules.Identity.Infrastructure.Email;
using SchoolMs.Modules.Identity.Infrastructure.Passwords;

namespace SchoolMs.Modules.Identity;

/// <summary>Settings in the <c>Identity</c> configuration section.</summary>
public sealed class IdentityModuleOptions
{
    public const string SectionName = "Identity";

    /// <summary>Reject passwords found in data breaches (IAM-04). Turn off only where outbound calls are not allowed.</summary>
    public bool CheckBreachedPasswords { get; init; } = true;

    /// <summary>Password of the demo organisation's users; set only for local and demo environments.</summary>
    public string? DemoPassword { get; init; }
}

/// <summary>Users, sign-in, sessions, invitations and password reset (IAM-01…05, ADR-0003).</summary>
public sealed class IdentityModule : IModule
{
    public string Name => "identity";

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<IdentityModuleOptions>().Bind(configuration.GetSection(IdentityModuleOptions.SectionName));
        services.AddOptions<SecurityOptions>().Bind(configuration.GetSection(SecurityOptions.SectionName));
        services.AddModuleDbContext<IdentityDbContext>(IdentityDbContext.SchemaName);

        // One key ring for every instance, kept in the database: sessions and links survive restarts and deploys.
        services.AddDataProtection()
            .SetApplicationName("schoolms")
            .PersistKeysToDbContext<IdentityDbContext>();

        services.AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies();
        services.AddIdentityCore<User>(ConfigureIdentity)
            .AddUserStore<UserStore>()
            .AddSignInManager()
            .AddClaimsPrincipalFactory<SessionClaimsPrincipalFactory>()
            .AddPasswordValidator<PasswordPolicyValidator>()
            .AddPasswordValidator<BreachedPasswordValidator>()
            .AddTokenProvider<InvitationTokenProvider>(InvitationTokenProvider.ProviderName)
            .AddTokenProvider<PasswordResetTokenProvider>(PasswordResetTokenProvider.ProviderName);

        services.AddSingleton<Argon2idPasswordHasher>();
        services.AddSingleton<IPasswordHasher<User>>(sp => sp.GetRequiredService<Argon2idPasswordHasher>());
        services.AddScoped<IUserConfirmation<User>, ActiveUserConfirmation>();
        services.AddScoped<SessionCookieEvents>();
        services.AddOptions<CookieAuthenticationOptions>(IdentityConstants.ApplicationScheme)
            .Configure<IOptions<SecurityOptions>>((options, security) => ConfigureSessionCookie(options, security.Value));

        // Password reset and deactivation end other sessions at their next check, within this interval.
        services.Configure<SecurityStampValidatorOptions>(options => options.ValidationInterval = TimeSpan.FromMinutes(5));

        services.AddHttpClient<PwnedPasswordsClient>(client =>
        {
            client.BaseAddress = PwnedPasswordsClient.BaseAddress;
            client.Timeout = TimeSpan.FromSeconds(3);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("schoolms-password-check");
        });

        services.Configure<RateLimiterOptions>(options =>
        {
            options.AddClientFixedWindowPolicy(SessionEndpoints.SignInRateLimit, permitLimit: 20, TimeSpan.FromMinutes(1));
            options.AddClientFixedWindowPolicy(PasswordEndpoints.EmailRateLimit, permitLimit: 5, TimeSpan.FromMinutes(10));
        });

        services.AddScoped<AccountService>();
        services.AddScoped<AccountEmailJob>();
        services.AddScoped<IDataSeeder, DemoUserSeeder>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapSessionEndpoints();
        endpoints.MapInvitationEndpoints();
        endpoints.MapPasswordEndpoints();
    }

    private static void ConfigureIdentity(IdentityOptions options)
    {
        // NIST SP 800-63B (IAM-04): length over composition rules; breached and over-long passwords are
        // rejected by the module's own validators.
        options.Password.RequiredLength = PasswordPolicyValidator.MinLength;
        options.Password.RequiredUniqueChars = 1;
        options.Password.RequireDigit = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;

        // IAM-05: five failed attempts lock the account for 15 minutes.
        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);

        // The email address is the user name and is validated by the domain model.
        options.User.RequireUniqueEmail = true;
        options.User.AllowedUserNameCharacters = string.Empty;

        // Only active users may sign in (see ActiveUserConfirmation).
        options.SignIn.RequireConfirmedAccount = true;
        options.Tokens.PasswordResetTokenProvider = PasswordResetTokenProvider.ProviderName;
    }

    private static void ConfigureSessionCookie(CookieAuthenticationOptions options, SecurityOptions security)
    {
        options.Cookie.Name = security.CookieName("session");
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = security.CookieSecurePolicy;
        options.ExpireTimeSpan = TimeSpan.FromHours(12);
        options.SlidingExpiration = true;
        options.EventsType = typeof(SessionCookieEvents);
    }
}
