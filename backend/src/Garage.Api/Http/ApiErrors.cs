using Garage.Domain.Common;

namespace Garage.Api.Http;

/// <summary>Erros que nascem na borda HTTP (autenticação, antiforgery, rate limit), fora dos casos de uso.</summary>
public static class ApiErrors
{
    public static readonly Error InvalidCredentials = new(
        "auth.invalid_credentials", "E-mail ou senha inválidos.", ErrorType.Unauthorized);

    public static readonly Error LockedOut = new(
        "auth.locked_out", "Conta bloqueada por 15 minutos após várias tentativas.", ErrorType.Unauthorized);

    public static readonly Error AntiforgeryInvalid = new(
        "auth.antiforgery_invalid", "Sessão expirada. Recarregue a página.", ErrorType.Validation);

    public static readonly Error RateLimitExceeded = new(
        "rate_limit.exceeded", "Muitas tentativas. Aguarde um minuto.", ErrorType.TooManyRequests);
}
