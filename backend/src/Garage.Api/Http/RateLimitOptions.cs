namespace Garage.Api.Http;

/// <summary>Limites por minuto (seção <c>RateLimits</c>). Os testes de integração os sobem para não interferirem entre si.</summary>
public sealed class RateLimitOptions
{
    public const string SectionName = "RateLimits";

    /// <summary><c>POST /auth/login</c>, por IP.</summary>
    public int LoginPerMinute { get; set; } = 5;

    /// <summary><c>/public/*</c>, por IP.</summary>
    public int PublicPerMinute { get; set; } = 60;

    /// <summary><c>/catalog/*</c>, por usuário.</summary>
    public int CatalogPerMinute { get; set; } = 30;

    /// <summary>Upload de anexos, por usuário.</summary>
    public int UploadsPerMinute { get; set; } = 30;
}
