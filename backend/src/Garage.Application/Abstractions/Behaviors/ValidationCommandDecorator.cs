using FluentValidation;
using Garage.Application.Abstractions.Messaging;
using Garage.Domain.Common;

namespace Garage.Application.Abstractions.Behaviors;

internal sealed class ValidationCommandDecorator<TCommand, TResult>(
    ICommandHandler<TCommand, TResult> inner,
    IEnumerable<IValidator<TCommand>> validators) : ICommandHandler<TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    public async Task<Result<TResult>> Handle(TCommand command, CancellationToken ct)
    {
        var error = await ValidationErrors.ValidateAsync(validators, command, ct);
        if (error is not null)
            return error;

        return await inner.Handle(command, ct);
    }
}
