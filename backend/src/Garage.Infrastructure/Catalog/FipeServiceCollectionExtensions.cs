using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Timeout;

namespace Garage.Infrastructure.Catalog;

internal static class FipeServiceCollectionExtensions
{
    private const string SubscriptionTokenHeader = "X-Subscription-Token";

    /// <summary>
    /// Registra o <see cref="FipeClient"/> com as opções <c>Fipe</c> e a resiliência da spec 6.4: 5 s por tentativa, 10 s no
    /// total, até 2 retries com backoff e circuit breaker (o padrão do pacote).
    /// </summary>
    public static IServiceCollection AddFipeClient(this IServiceCollection services, IConfiguration config)
    {
        services.AddOptions<FipeOptions>().Bind(config.GetSection(FipeOptions.SectionName));

        services.AddHttpClient<FipeClient>((provider, http) =>
            {
                var options = provider.GetRequiredService<IOptions<FipeOptions>>().Value;
                // Sem a barra final o último segmento da URL base seria descartado ao resolver os caminhos relativos.
                http.BaseAddress = new Uri(options.BaseUrl.EndsWith('/') ? options.BaseUrl : options.BaseUrl + "/");
                if (!string.IsNullOrWhiteSpace(options.Token))
                    http.DefaultRequestHeaders.Add(SubscriptionTokenHeader, options.Token);
            })
            .AddStandardResilienceHandler(options =>
            {
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(5);
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(10);
                options.Retry.MaxRetryAttempts = 2;
                options.Retry.Delay = TimeSpan.FromMilliseconds(500);
                options.Retry.ShouldHandle = args => ValueTask.FromResult(IsRetryable(args.Outcome));
            });

        return services;
    }

    // O padrão do pacote também repete 408 e 429. Aqui só falha de rede, timeout e 5xx: com 429 a FIPE está dizendo que a
    // cota acabou, e repetir só gastaria mais dela.
    private static bool IsRetryable(Outcome<HttpResponseMessage> outcome) => outcome switch
    {
        { Exception: HttpRequestException or TimeoutRejectedException } => true,
        { Result.StatusCode: >= HttpStatusCode.InternalServerError } => true,
        _ => false
    };
}
