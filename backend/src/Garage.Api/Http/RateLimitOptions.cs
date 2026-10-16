namespace Garage.Api.Http;

/// <summary>
/// Limites por minuto, exceto o da demo, por hora (seção <c>RateLimits</c>). Os testes de integração os sobem para não
/// interferirem entre si.
/// </summary>
public sealed class RateLimitOptions
{
    public const string SectionName = "RateLimits";

    /// <summary><c>POST /auth/login</c> e <c>POST /auth/register</c> (a mesma cota), por IP.</summary>
    public int LoginPerMinute { get; set; } = 5;

    /// <summary><c>GET /public/{token}</c> (a visão JSON), por IP.</summary>
    public int PublicPerMinute { get; set; } = 60;

    /// <summary>
    /// <c>GET /public/{token}/attachments/{id}</c>, por IP. À parte da visão: uma página com 30 registros e algumas fotos em
    /// cada um carrega dezenas de imagens de uma vez.
    /// </summary>
    public int PublicPhotosPerMinute { get; set; } = 600;

    /// <summary><c>/catalog/*</c>, por usuário.</summary>
    public int CatalogPerMinute { get; set; } = 30;

    /// <summary>Upload de anexos, por usuário.</summary>
    public int UploadsPerMinute { get; set; } = 30;

    /// <summary><c>POST /auth/demo</c>, por IP e por hora: cada demo cria uma conta e copia as fotos de exemplo.</summary>
    public int DemoPerHour { get; set; } = 5;
}
