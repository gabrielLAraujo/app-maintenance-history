using System.Net;
using Garage.Infrastructure.Catalog;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Polly.Timeout;

namespace Garage.Infrastructure.Tests.Catalog;

/// <summary>O <see cref="FipeClient"/> montado como em produção (token, endereço base e resiliência), sobre um servidor falso.</summary>
public sealed class FipeClientTests
{
    private const string BaseUrl = "http://fipe.test/api/v2/";

    [Fact]
    public async Task Requests_are_relative_to_the_base_url_and_carry_the_token()
    {
        using var rig = new Rig(_ => Ok("[]"), token: "segredo");

        var body = await rig.Client.GetAsync("cars/brands/25/models", CancellationToken.None);

        Assert.Equal("[]", body);
        var request = Assert.Single(rig.Requests);
        Assert.Equal(new Uri(BaseUrl + "cars/brands/25/models"), request.RequestUri);
        Assert.Equal("segredo", Assert.Single(request.Headers.GetValues("X-Subscription-Token")));
    }

    [Fact]
    public async Task Without_a_token_no_subscription_header_is_sent()
    {
        using var rig = new Rig(_ => Ok("[]"), token: null);

        await rig.Client.GetAsync("cars/brands", CancellationToken.None);

        Assert.False(Assert.Single(rig.Requests).Headers.Contains("X-Subscription-Token"));
    }

    [Fact]
    public async Task Too_many_requests_is_never_retried()
    {
        using var rig = new Rig(_ => new HttpResponseMessage(HttpStatusCode.TooManyRequests));

        var error = await Assert.ThrowsAsync<HttpRequestException>(() => rig.Client.GetAsync("cars/brands", CancellationToken.None));

        Assert.Equal(HttpStatusCode.TooManyRequests, error.StatusCode);
        Assert.Equal(1, rig.Calls);
    }

    [Fact]
    public async Task Client_errors_are_not_retried()
    {
        using var rig = new Rig(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

        var error = await Assert.ThrowsAsync<HttpRequestException>(() => rig.Client.GetAsync("cars/000000-0/years/1998-1", CancellationToken.None));

        Assert.Equal(HttpStatusCode.NotFound, error.StatusCode);
        Assert.Equal(1, rig.Calls);
    }

    [Fact]
    public async Task Server_errors_are_retried_twice_and_then_surface()
    {
        using var rig = new Rig(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

        var error = await Assert.ThrowsAsync<HttpRequestException>(() => rig.Client.GetAsync("cars/brands", CancellationToken.None));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, error.StatusCode);
        Assert.Equal(3, rig.Calls); // 1 tentativa + 2 retries
    }

    [Fact]
    public async Task Transport_failures_are_retried_and_a_later_success_wins()
    {
        using var rig = new Rig(call => call == 1 ? throw new HttpRequestException("conexão recusada") : Ok("[{\"code\":\"1\",\"name\":\"Acura\"}]"));

        var body = await rig.Client.GetAsync("cars/brands", CancellationToken.None);

        Assert.Contains("Acura", body, StringComparison.Ordinal);
        Assert.Equal(2, rig.Calls);
    }

    [Fact]
    public async Task A_fipe_that_never_answers_surfaces_as_a_timeout_within_the_total_budget()
    {
        // A resiliência lê o TimeProvider do contêiner: com o relógio falso o tempo só passa quando o teste o faz andar.
        var clock = new FakeTimeProvider();
        using var rig = new Rig(async (_, ct) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            return Ok("[]");
        }, clock);

        var call = rig.Client.GetAsync("cars/brands", CancellationToken.None);
        for (var second = 0; second < 30 && !call.IsCompleted; second++)
        {
            await Task.Delay(20); // deixa a resiliência chegar às suas esperas antes de o relógio andar
            clock.Advance(TimeSpan.FromSeconds(1));
        }

        await Assert.ThrowsAsync<TimeoutRejectedException>(() => call);
        // 5 s por tentativa e 10 s no total: a segunda tentativa ainda cabe, a terceira nunca começa.
        Assert.InRange(rig.Calls, 1, 2);
    }

    private static HttpResponseMessage Ok(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") };

    private sealed class Rig : IDisposable
    {
        private readonly ServiceProvider _provider;
        private readonly StubHandler _handler;

        public Rig(Func<int, HttpResponseMessage> respond, string? token = null)
            : this((call, _) => Task.FromResult(respond(call)), clock: null, token)
        {
        }

        public Rig(Func<int, CancellationToken, Task<HttpResponseMessage>> respond, TimeProvider clock)
            : this(respond, clock, token: null)
        {
        }

        private Rig(Func<int, CancellationToken, Task<HttpResponseMessage>> respond, TimeProvider? clock, string? token)
        {
            _handler = new StubHandler(respond);
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["Fipe:BaseUrl"] = BaseUrl, ["Fipe:Token"] = token })
                .Build();

            var services = new ServiceCollection();
            services.AddLogging();
            if (clock is not null)
                services.AddSingleton(clock);
            services.AddFipeClient(config);
            services.AddHttpClient<FipeClient>().ConfigurePrimaryHttpMessageHandler(() => _handler);
            _provider = services.BuildServiceProvider();
        }

        public FipeClient Client => _provider.GetRequiredService<FipeClient>();

        public int Calls => _handler.Requests.Count;

        public IReadOnlyList<HttpRequestMessage> Requests => _handler.Requests;

        public void Dispose() => _provider.Dispose();
    }

    private sealed class StubHandler(Func<int, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        private readonly List<HttpRequestMessage> _requests = [];

        public IReadOnlyList<HttpRequestMessage> Requests => _requests;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _requests.Add(request);
            return respond(_requests.Count, cancellationToken);
        }
    }
}
