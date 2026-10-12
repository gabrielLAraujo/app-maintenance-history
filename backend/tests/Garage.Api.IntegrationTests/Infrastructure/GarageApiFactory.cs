using Garage.Infrastructure.Catalog;
using Garage.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Time.Testing;
using Testcontainers.PostgreSql;

namespace Garage.Api.IntegrationTests.Infrastructure;

public sealed class GarageApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private static readonly string[] RateLimitKeys =
        ["LoginPerMinute", "PublicPerMinute", "CatalogPerMinute", "UploadsPerMinute"];

    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder("postgres:18").Build();

    public FakeTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 10, 10, 15, 0, 0, TimeSpan.Zero));

    public string StoragePath { get; } =
        Path.Combine(Path.GetTempPath(), "garage-tests", Guid.NewGuid().ToString("N"));

    /// <summary>
    /// A FIPE falsa, no lugar da real. É uma instância só, também para as fábricas derivadas de <c>WithWebHostBuilder</c>;
    /// os testes que a alteram a devolvem ao estado inicial (<see cref="FakeFipeHandler.Reset"/>).
    /// </summary>
    public FakeFipeHandler Fipe { get; } = new();

    /// <summary>
    /// Fábrica derivada com relógio próprio, para os testes que avançam o tempo: o <see cref="Clock"/> é compartilhado com
    /// os outros testes e não volta no tempo. Usa o mesmo banco e o mesmo armazenamento.
    /// </summary>
    public WebApplicationFactory<Program> WithClock(FakeTimeProvider clock) =>
        WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(clock);
        }));

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(StoragePath);
        await _database.StartAsync();

        await using var scope = Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<GarageDbContext>().Database.MigrateAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _database.DisposeAsync();

        if (Directory.Exists(StoragePath))
            Directory.Delete(StoragePath, recursive: true);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Garage", _database.GetConnectionString());
        builder.UseSetting("Auth:AllowRegistration", "true");
        builder.UseSetting("Storage:RootPath", StoragePath);
        // Sem caminho na URL base: o FakeFipeHandler indexa as respostas pelo caminho relativo (cars/brands...).
        builder.UseSetting("Fipe:BaseUrl", "http://fipe.test/");
        foreach (var key in RateLimitKeys)
            builder.UseSetting($"RateLimits:{key}", "10000");

        builder.UseDefaultServiceProvider(options =>
        {
            options.ValidateScopes = true;
            options.ValidateOnBuild = true;
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
            services.AddHttpClient<FipeClient>().ConfigurePrimaryHttpMessageHandler(() => Fipe);
            // A resiliência do FipeClient lê o TimeProvider do contêiner, que aqui é o relógio falso (não anda sozinho): sem
            // isto a pausa entre retries nunca terminaria e a FIPE fora do ar travaria a requisição até o timeout do cliente.
            services.PostConfigureAll<HttpStandardResilienceOptions>(options => options.Retry.Delay = TimeSpan.Zero);
            // O TestServer não limita o corpo da requisição; o Kestrel, sim (inclusive o RequestSizeLimit dos endpoints).
            services.AddSingleton<IStartupFilter, KestrelRequestBodyLimit>();
        });
    }
}
