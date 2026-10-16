using System.Net;
using System.Net.Sockets;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Garage.Infrastructure.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Garage.Api.Http;

/// <summary>Nomes das políticas para <c>RequireRateLimiting</c>.</summary>
public static class RateLimitPolicies
{
    public const string Login = "login";
    public const string Public = "public";
    public const string PublicPhotos = "public-photos";
    public const string Catalog = "catalog";
    public const string Uploads = "uploads";
    public const string Demo = "demo";
}

public static class RateLimitingSetup
{
    private static readonly TimeSpan Minute = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan Hour = TimeSpan.FromHours(1);

    public static IServiceCollection AddRateLimiting(this IServiceCollection services)
    {
        services.AddOptions<RateLimitOptions>().BindConfiguration(RateLimitOptions.SectionName);

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, _) =>
                await ApiErrors.RateLimitExceeded.ToProblem().ExecuteAsync(context.HttpContext);

            options.AddPolicy(RateLimitPolicies.Login, http => ByIp(http, limits => limits.LoginPerMinute));
            options.AddPolicy(RateLimitPolicies.Public, http => ByIp(http, limits => limits.PublicPerMinute));
            options.AddPolicy(RateLimitPolicies.PublicPhotos, http => ByIp(http, limits => limits.PublicPhotosPerMinute));
            options.AddPolicy(RateLimitPolicies.Catalog, http => ByUser(http, limits => limits.CatalogPerMinute));
            options.AddPolicy(RateLimitPolicies.Uploads, http => ByUser(http, limits => limits.UploadsPerMinute));
            options.AddPolicy(RateLimitPolicies.Demo, http => ByIp(http, limits => limits.DemoPerHour, Hour));
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(DemoWrites);
        });

        return services;
    }

    private static RateLimitPartition<string> ByIp(
        HttpContext http, Func<RateLimitOptions, int> limit, TimeSpan? window = null) =>
        Partition(http, ClientKey(http), limit, window ?? Minute);

    // Rotas por usuário exigem login; sem usuário (não deveria ocorrer) cai para o IP.
    private static RateLimitPartition<string> ByUser(HttpContext http, Func<RateLimitOptions, int> limit) =>
        http.User.FindFirstValue(ClaimTypes.NameIdentifier) is { } userId
            ? Partition(http, $"user:{userId}", limit, Minute)
            : Partition(http, ClientKey(http), limit, Minute);

    // Toda escrita de uma conta demo conta na cota da conta; o resto (leituras, contas reais, /api/auth) não tem limite aqui.
    private static RateLimitPartition<string> DemoWrites(HttpContext http) =>
        IsDemoWrite(http) && http.User.FindFirstValue(ClaimTypes.NameIdentifier) is { } userId
            ? Partition(http, $"demo-writes:{userId}", limits => limits.DemoWritesPerHour, Hour)
            : RateLimitPartition.GetNoLimiter("unlimited");

    private static bool IsDemoWrite(HttpContext http) =>
        http.User.HasClaim(GarageClaims.Demo, "true")
        && !HttpMethods.IsGet(http.Request.Method)
        && !HttpMethods.IsHead(http.Request.Method)
        && !HttpMethods.IsOptions(http.Request.Method)
        && !http.Request.Path.StartsWithSegments("/api/auth");

    // IPv6: o provedor entrega uma rede /64 a cada cliente, que troca o fim do endereço à vontade; a cota é da rede.
    internal static string ClientKey(HttpContext http)
    {
        var ip = http.Connection.RemoteIpAddress;
        if (ip is null)
            return "ip:unknown";
        if (ip.IsIPv4MappedToIPv6)
            ip = ip.MapToIPv4();
        if (ip.AddressFamily != AddressFamily.InterNetworkV6)
            return $"ip:{ip}";

        var network = ip.GetAddressBytes();
        Array.Clear(network, 8, 8);
        return $"ip:{new IPAddress(network)}/64";
    }

    private static RateLimitPartition<string> Partition(
        HttpContext http, string key, Func<RateLimitOptions, int> limit, TimeSpan window)
    {
        var permitLimit = limit(http.RequestServices.GetRequiredService<IOptions<RateLimitOptions>>().Value);
        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permitLimit,
            Window = window,
            QueueLimit = 0
        });
    }
}
