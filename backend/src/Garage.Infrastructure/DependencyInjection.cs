using Garage.Application.Abstractions.Ports;
using Garage.Infrastructure.Access;
using Garage.Infrastructure.Identity;
using Garage.Infrastructure.Persistence;
using Garage.Infrastructure.Persistence.Repositories;
using Garage.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Garage.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        // A connection string é lida ao criar as opções (não no registro), então configurações
        // adicionadas depois — como as dos testes — também valem.
        services.AddDbContext<GarageDbContext>(options => options
            .UseNpgsql(config.GetConnectionString("Garage"))
            .UseSnakeCaseNamingConvention());
        services.AddScoped<IReadDbContext>(sp => sp.GetRequiredService<GarageDbContext>());
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IVehicleRepository, VehicleRepository>();
        services.AddScoped<IReminderRepository, ReminderRepository>();
        services.AddScoped<IVehicleAccess, VehicleAccess>();

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();

        services.AddOptions<AppOptions>().Bind(config.GetSection(AppOptions.SectionName));
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IAppClock, AppClock>();

        return services;
    }
}
