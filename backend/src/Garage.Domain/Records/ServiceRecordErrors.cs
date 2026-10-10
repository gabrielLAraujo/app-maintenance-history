using System.Globalization;
using Garage.Domain.Common;

namespace Garage.Domain.Records;

public static class ServiceRecordErrors
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public static readonly Error NotFound = new(
        "record.not_found",
        "Registro não encontrado.",
        ErrorType.NotFound);

    public static readonly Error DateInFuture = new(
        "record.date_in_future",
        "A data do registro não pode estar no futuro.",
        ErrorType.DomainRule);

    public static readonly Error ReplacedPartOnlyForModification = new(
        "record.replaced_part_only_for_modification",
        "Peça original substituída só vale para modificações.",
        ErrorType.DomainRule);

    public static Error OdometerOutOfOrder(int? maxKmBefore, int? minKmAfter) => new(
        "record.odometer_out_of_order",
        OdometerOutOfOrderMessage(maxKmBefore, minKmAfter),
        ErrorType.DomainRule,
        new Dictionary<string, object?>
        {
            ["maxKmBefore"] = maxKmBefore,
            ["minKmAfter"] = minKmAfter
        });

    private static string OdometerOutOfOrderMessage(int? maxKmBefore, int? minKmAfter) =>
        (maxKmBefore, minKmAfter) switch
        {
            ({ } before, { } after) =>
                $"A km deve ficar entre {Format(before)} e {Format(after)} km para manter a ordem do histórico.",
            ({ } before, null) =>
                $"A km deve ser de pelo menos {Format(before)} km para manter a ordem do histórico.",
            (null, { } after) =>
                $"A km deve ser de no máximo {Format(after)} km para manter a ordem do histórico.",
            _ => "A km não respeita a ordem do histórico."
        };

    private static string Format(int km) => km.ToString("N0", PtBr);
}
