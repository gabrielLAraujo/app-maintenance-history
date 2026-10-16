using Garage.Infrastructure.Demo;
using Garage.Infrastructure.Identity;
using Garage.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Garage.Api.Auth;

public static class AuthSetup
{
    public const string CookieName = "garage_auth";
    public const string AntiforgeryHeaderName = "X-XSRF-TOKEN";
    public const string AntiforgeryCookieName = "XSRF-TOKEN";

    private const string RegistrationSetting = "Auth:AllowRegistration";

    public static IServiceCollection AddAuth(this IServiceCollection services)
    {
        services.AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies();

        services.AddIdentityCore<AppUser>(options =>
            {
                options.Password.RequiredLength = 10;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.User.RequireUniqueEmail = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddEntityFrameworkStores<GarageDbContext>()
            .AddClaimsPrincipalFactory<AppUserClaimsPrincipalFactory>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = CookieName;
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.ExpireTimeSpan = TimeSpan.FromDays(14);
            options.SlidingExpiration = true;

            // API: sem sessão responde 401/403, nunca redireciona para uma página de login.
            options.Events.OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            };
            options.Events.OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            };
        });

        services.AddAntiforgery(options =>
        {
            options.HeaderName = AntiforgeryHeaderName;
            // Só o cookie do antiforgery acompanha o esquema da requisição: com Always o antiforgery lança
            // InvalidOperationException (500) em qualquer requisição HTTP simples (dev local, proxy do front).
            // Em produção o Traefik manda X-Forwarded-Proto: https, então o cookie sai Secure. O cookie de
            // autenticação continua Always.
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            options.Cookie.SameSite = SameSiteMode.Strict;
        });

        services.AddScoped<PasswordResetService>();

        return services;
    }

    /// <summary>
    /// <c>Auth:AllowRegistration</c>, lido ao mapear as rotas: com o cadastro desligado a rota nem existe (404).
    /// </summary>
    public static bool IsRegistrationEnabled(this IEndpointRouteBuilder routes) =>
        routes.ServiceProvider.GetRequiredService<IConfiguration>().GetValue<bool>(RegistrationSetting);

    /// <summary>
    /// <c>Demo:Visitors:Enabled</c>, lido ao mapear as rotas: com a demo do visitante desligada, <c>POST /auth/demo</c> nem
    /// existe (404).
    /// </summary>
    public static bool IsVisitorDemoEnabled(this IEndpointRouteBuilder routes) =>
        routes.ServiceProvider.GetRequiredService<IOptions<DemoVisitorOptions>>().Value.Enabled;
}
