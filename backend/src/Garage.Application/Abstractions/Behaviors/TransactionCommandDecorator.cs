using Garage.Application.Abstractions.Messaging;
using Garage.Application.Abstractions.Ports;
using Garage.Domain.Common;

namespace Garage.Application.Abstractions.Behaviors;

internal sealed class TransactionCommandDecorator<TCommand, TResult>(
    ICommandHandler<TCommand, TResult> inner,
    IUnitOfWork unitOfWork) : ICommandHandler<TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    public async Task<Result<TResult>> Handle(TCommand command, CancellationToken ct)
    {
        Result<TResult> result;
        try
        {
            result = await inner.Handle(command, ct);
        }
        catch
        {
            // A limpeza (OnRollback) precisa rodar mesmo se a requisição foi cancelada.
            await unitOfWork.DiscardAsync(CancellationToken.None);
            throw;
        }

        if (result.IsFailure)
        {
            await unitOfWork.DiscardAsync(CancellationToken.None);
            return result;
        }

        var saved = await unitOfWork.SaveChangesAsync(ct);
        return saved.IsSuccess ? result : saved.Error!;
    }
}
