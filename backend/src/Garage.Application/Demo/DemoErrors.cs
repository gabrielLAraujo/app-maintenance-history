using Garage.Domain.Common;

namespace Garage.Application.Demo;

public static class DemoErrors
{
    public static readonly Error AlreadyHasData = new(
        "demo.already_has_data",
        "Você já tem veículos cadastrados; os dados de exemplo são só para contas vazias.",
        ErrorType.Conflict);

    public static readonly Error Unavailable = new(
        "demo.unavailable",
        "A demonstração está cheia agora. Tente de novo mais tarde.",
        ErrorType.Unavailable);

    /// <summary>A espera pela trava do teto de veículos passou do limite: outro pedido da mesma conta está em curso.</summary>
    public static readonly Error Busy = new(
        "demo.busy",
        "A demonstração está ocupada com outro pedido. Tente de novo em instantes.",
        ErrorType.Unavailable);

    /// <summary>A demo não serve de hospedagem de arquivos: as fotos de exemplo já estão lá.</summary>
    public static readonly Error UploadsDisabled = new(
        "demo.uploads_disabled",
        "Na demonstração não dá para enviar arquivos.",
        ErrorType.Forbidden);

    public static Error VehicleLimitReached(int max) => new(
        "demo.vehicle_limit_reached",
        $"A demonstração permite até {max} veículos.",
        ErrorType.DomainRule,
        new Dictionary<string, object?> { ["max"] = max });
}
