using Garage.Domain.Common;

namespace Garage.Application.Abstractions;

public static class CommonErrors
{
    public static readonly Error ConcurrencyConflict = new(
        "concurrency.conflict",
        "Este item foi alterado em outra tela. Recarregue e tente de novo.",
        ErrorType.Conflict);
}
