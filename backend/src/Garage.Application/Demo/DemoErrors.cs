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
}
