using Garage.Application.Abstractions;
using Garage.Application.Abstractions.Events;
using Garage.Application.Abstractions.Ports;
using Garage.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Garage.Infrastructure.Persistence;

internal sealed partial class UnitOfWork(
    GarageDbContext db,
    IDomainEventDispatcher dispatcher,
    ILogger<UnitOfWork> logger) : IUnitOfWork
{
    private const int MaxDispatchRounds = 5;

    private readonly List<Func<CancellationToken, Task>> _afterCommit = [];
    private readonly List<Func<CancellationToken, Task>> _onRollback = [];

    public void AfterCommit(Func<CancellationToken, Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        _afterCommit.Add(action);
    }

    public void OnRollback(Func<CancellationToken, Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        _onRollback.Add(action);
    }

    public async Task<Result> SaveChangesAsync(CancellationToken ct)
    {
        Result dispatched;
        try
        {
            dispatched = await DispatchDomainEventsAsync(ct);
            if (dispatched.IsSuccess)
                await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            await DiscardAsync(CancellationToken.None);
            return CommonErrors.ConcurrencyConflict;
        }
        catch
        {
            await DiscardAsync(CancellationToken.None);
            throw;
        }

        if (dispatched.IsFailure)
        {
            await DiscardAsync(CancellationToken.None);
            return dispatched;
        }

        _onRollback.Clear();
        // O commit já aconteceu: as ações não devem ser interrompidas pelo cancelamento da requisição.
        await RunIsolatedAsync(Drain(_afterCommit), CancellationToken.None, LogAfterCommitActionFailed);
        return Result.Success();
    }

    public async Task DiscardAsync(CancellationToken ct)
    {
        db.ChangeTracker.Clear();
        _afterCommit.Clear();
        await RunIsolatedAsync(Drain(_onRollback), ct, LogRollbackActionFailed);
    }

    private async Task<Result> DispatchDomainEventsAsync(CancellationToken ct)
    {
        for (var round = 0; round < MaxDispatchRounds; round++)
        {
            var events = TakeDomainEvents();
            if (events.Count == 0)
                return Result.Success();

            var result = await dispatcher.Dispatch(events, ct);
            if (result.IsFailure)
                return result;
        }

        if (AggregatesWithEvents().Any())
            throw new InvalidOperationException(
                $"Os eventos de domínio continuam surgindo após {MaxDispatchRounds} rodadas de despacho.");

        return Result.Success();
    }

    private List<IDomainEvent> TakeDomainEvents()
    {
        var aggregates = AggregatesWithEvents().ToList();
        var events = aggregates.SelectMany(aggregate => aggregate.DomainEvents).ToList();
        aggregates.ForEach(aggregate => aggregate.ClearDomainEvents());
        return events;
    }

    private IEnumerable<AggregateRoot> AggregatesWithEvents() =>
        db.ChangeTracker.Entries<AggregateRoot>()
            .Select(entry => entry.Entity)
            .Where(aggregate => aggregate.DomainEvents.Count > 0);

    private async Task RunIsolatedAsync(
        IEnumerable<Func<CancellationToken, Task>> actions, CancellationToken ct, Action<ILogger, Exception> logFailure)
    {
        foreach (var action in actions)
        {
            try
            {
                await action(ct);
            }
            catch (Exception ex)
            {
                logFailure(logger, ex);
            }
        }
    }

    private static Func<CancellationToken, Task>[] Drain(List<Func<CancellationToken, Task>> actions)
    {
        var drained = actions.ToArray();
        actions.Clear();
        return drained;
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "An after-commit action failed")]
    private static partial void LogAfterCommitActionFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "A rollback action failed")]
    private static partial void LogRollbackActionFailed(ILogger logger, Exception exception);
}
