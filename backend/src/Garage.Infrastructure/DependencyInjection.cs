using Garage.Application.Abstractions.Ports;
using Garage.Infrastructure.Access;
using Garage.Infrastructure.Catalog;
using Garage.Infrastructure.Demo;
using Garage.Infrastructure.Identity;
using Garage.Infrastructure.Persistence;
using Garage.Infrastructure.Persistence.Repositories;
using Garage.Infrastructure.Storage;
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
        services.AddScoped<IServiceRecordRepository, ServiceRecordRepository>();
        services.AddScoped<IAttachmentRepository, AttachmentRepository>();
        services.AddScoped<IShareLinkRepository, ShareLinkRepository>();
        services.AddScoped<IVehicleAccess, VehicleAccess>();

        services.AddFipeClient(config);
        services.AddScoped<IVehicleCatalog, FipeVehicleCatalog>();

        // Configuração inválida derruba a subida (ValidateOnStart), não a primeira requisição que precisar dela.
        services.AddOptions<StorageOptions>()
            .Bind(config.GetSection(StorageOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.RootPath),
                "Storage:RootPath não está configurado: defina a pasta dos anexos (na imagem, /data/attachments).")
            .ValidateOnStart();
        services.AddSingleton<IFileStorage, LocalDiskFileStorage>();
        services.AddSingleton<IImageProcessor, MagickImageProcessor>();
        services.AddSingleton<IDemoAssets, EmbeddedDemoAssets>();

        services.AddOptions<DemoVisitorOptions>()
            .Bind(config.GetSection(DemoVisitorOptions.SectionName))
            .Validate(options => options.TtlHours > 0 && options.MaxVehicles > 0
                                 && options.MaxActive >= 0 && options.CleanupIntervalMinutes >= 0,
                "Demo:Visitors inválido: TtlHours e MaxVehicles precisam ser positivos; MaxActive (0 fecha a demo) e "
                + "CleanupIntervalMinutes (0 desliga a limpeza automática), zero ou positivos.")
            .ValidateOnStart();
        services.AddScoped<DemoAccountService>();
        // Sempre registrado: confere Enabled e o intervalo ao subir, lendo as opções (e não a configuração no registro), então
        // valem também as configurações aplicadas depois, como as dos testes.
        services.AddHostedService<DemoCleanupService>();
        services.AddSingleton<IDemoLimits, DemoLimits>();

        services.AddGarageDataProtection(config);

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();

        services.AddOptions<AppOptions>()
            .Bind(config.GetSection(AppOptions.SectionName))
            .Validate(options => AppOptions.IsKnownTimeZone(options.TimeZone),
                "App:TimeZone não é um fuso conhecido pelo sistema (id IANA, ex.: America/Sao_Paulo; "
                + "a imagem precisa de ICU e tzdata).")
            .ValidateOnStart();
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IAppClock, AppClock>();

        return services;
    }
}
