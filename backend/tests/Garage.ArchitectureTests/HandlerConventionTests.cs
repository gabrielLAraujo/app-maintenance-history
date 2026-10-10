using Garage.Application.Abstractions.Events;
using Garage.Application.Abstractions.Messaging;

namespace Garage.ArchitectureTests;

public class HandlerConventionTests
{
    private static readonly Type[] HandlerContracts =
        [typeof(ICommandHandler<,>), typeof(IQueryHandler<,>), typeof(IDomainEventHandler<>)];

    [Fact]
    public void Handlers_and_decorators_are_internal_sealed()
    {
        var handlers = typeof(Garage.Application.AssemblyMarker).Assembly.GetTypes()
            .Where(type => type.IsClass && type.GetInterfaces().Any(IsHandlerContract))
            .ToList();

        Assert.NotEmpty(handlers);
        var offenders = handlers.Where(type => !type.IsSealed || type.IsVisible).Select(type => type.FullName);
        Assert.Empty(offenders);
    }

    private static bool IsHandlerContract(Type contract) =>
        contract.IsGenericType && HandlerContracts.Contains(contract.GetGenericTypeDefinition());
}
