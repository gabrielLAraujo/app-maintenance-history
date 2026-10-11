using Garage.Application.Abstractions.Events;
using Garage.Application.Abstractions.Ports;
using Garage.Domain.Common;
using Garage.Domain.Records;
using Garage.Domain.Reminders;

namespace Garage.Application.Records;

/// <summary>
/// R5: o registro novo cumpre os lembretes indicados (M3, M4) na mesma unidade de trabalho — uma falha aqui descarta
/// também o registro. Os lembretes são buscados só no veículo do registro: o id de um lembrete de outro veículo (de
/// qualquer usuário) conta como inexistente.
/// </summary>
internal sealed class FulfillRemindersOnRecordAdded(IReminderRepository reminders) : IDomainEventHandler<ServiceRecordAdded>
{
    public async Task<Result> Handle(ServiceRecordAdded e, CancellationToken ct)
    {
        if (e.FulfilledReminderIds.Count == 0)
            return Result.Success();

        var ids = e.FulfilledReminderIds.Distinct().ToList();
        var found = await reminders.GetManyAsync(e.VehicleId, ids, ct);
        if (found.Count != ids.Count)
            return ReminderErrors.NotFound;

        // Na ordem pedida, para que a falha devolvida seja sempre a do primeiro lembrete que não pode ser cumprido.
        foreach (var reminder in found.OrderBy(reminder => ids.IndexOf(reminder.Id)))
        {
            var done = reminder.MarkDone(e.Date, e.Odometer);
            if (done.IsFailure)
                return done;
        }

        return Result.Success();
    }
}
