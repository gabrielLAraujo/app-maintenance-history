using System.Net;
using System.Text;

namespace Garage.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Servidor falso da FIPE, no lugar do <c>HttpMessageHandler</c> primário do <c>FipeClient</c>. As respostas são indexadas
/// pelo caminho relativo à URL base (<c>cars/brands</c>, <c>cars/brands/25/models</c>...); um caminho sem resposta dá 404.
/// Compartilhado por todos os testes da fábrica: quem o altera devolve ao estado inicial com <see cref="Reset"/>.
/// </summary>
public sealed class FakeFipeHandler : HttpMessageHandler
{
    private int _calls;

    /// <summary>Corpo JSON (200) de cada caminho.</summary>
    public Dictionary<string, string> Responses { get; } = new(StringComparer.Ordinal);

    /// <summary>Com <c>true</c>, toda requisição responde 503.</summary>
    public bool IsDown { get; set; }

    /// <summary>Requisições recebidas, inclusive as que a resiliência repete.</summary>
    public int Calls => Volatile.Read(ref _calls);

    public void Reset()
    {
        Responses.Clear();
        IsDown = false;
        Volatile.Write(ref _calls, 0);
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _calls);

        if (IsDown)
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

        var path = request.RequestUri!.AbsolutePath.TrimStart('/');
        if (!Responses.TryGetValue(path, out var json))
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });
    }
}
