using FluentValidation;
using Garage.Application.Abstractions.Messaging;
using Garage.Domain.Common;

namespace Garage.Application.Abstractions.Behaviors;

internal sealed class ValidationQueryDecorator<TQuery, TResult>(
    IQueryHandler<TQuery, TResult> inner,
    IEnumerable<IValidator<TQuery>> validators) : IQueryHandler<TQuery, TResult>
    where TQuery : IQuery<TResult>
{
    public async Task<Result<TResult>> Handle(TQuery query, CancellationToken ct)
    {
        var error = await ValidationErrors.ValidateAsync(validators, query, ct);
        if (error is not null)
            return error;

        return await inner.Handle(query, ct);
    }
}
