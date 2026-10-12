namespace Garage.Infrastructure.Catalog;

/// <summary>
/// HttpClient tipado da FIPE: devolve o JSON bruto e não conhece o formato dele. O endereço base, o token e a resiliência
/// (timeouts, retries, circuit breaker) vêm do registro em <see cref="FipeServiceCollectionExtensions"/>.
/// </summary>
public sealed class FipeClient(HttpClient http)
{
    /// <summary>
    /// GET em <paramref name="relativePath"/> (relativo à URL base, sem barra inicial). Resposta fora de 2xx vira
    /// <see cref="HttpRequestException"/>, com o status em <see cref="HttpRequestException.StatusCode"/>.
    /// </summary>
    public async Task<string> GetAsync(string relativePath, CancellationToken ct)
    {
        using var response = await http.GetAsync(relativePath, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct);
    }
}
