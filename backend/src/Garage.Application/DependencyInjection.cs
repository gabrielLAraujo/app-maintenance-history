using FluentValidation;
using Garage.Application.Abstractions.Behaviors;
using Garage.Application.Abstractions.Events;
using Garage.Application.Abstractions.Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace Garage.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        // Handlers são internal sealed; os decorators (genéricos abertos) ficam fora do scan.
        services.Scan(scan => scan
            .FromAssemblies(assembly)
            .AddClasses(
                classes => classes
                    .AssignableToAny(typeof(ICommandHandler<,>), typeof(IQueryHandler<,>), typeof(IDomainEventHandler<>))
                    .Where(type => !type.IsGenericTypeDefinition),
                publicOnly: false)
            .AsImplementedInterfaces()
            .WithScopedLifetime());

        services.AddValidatorsFromAssembly(assembly, ServiceLifetime.Scoped, includeInternalTypes: true);

        // O primeiro decorator registrado fica mais perto do handler: Logging → Validation → Transaction → handler.
        services.TryDecorate(typeof(ICommandHandler<,>), typeof(TransactionCommandDecorator<,>));
        services.TryDecorate(typeof(ICommandHandler<,>), typeof(ValidationCommandDecorator<,>));
        services.TryDecorate(typeof(ICommandHandler<,>), typeof(LoggingCommandDecorator<,>));
        services.TryDecorate(typeof(IQueryHandler<,>), typeof(ValidationQueryDecorator<,>));
        services.TryDecorate(typeof(IQueryHandler<,>), typeof(LoggingQueryDecorator<,>));

        services.AddScoped<ISender, Sender>();
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();

        return services;
    }
}
