using Garage.Domain.Common;

namespace Garage.Application.Demo;

public static class DemoErrors
{
    public static readonly Error AlreadyHasData = new(
        "demo.already_has_data",
        "Você já tem veículos cadastrados; os dados de exemplo são só para contas vazias.",
        ErrorType.Conflict);
}
