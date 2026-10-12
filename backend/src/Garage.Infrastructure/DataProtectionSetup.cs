using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Garage.Infrastructure;

public static class DataProtectionSetup
{
    /// <summary>Fixo para o anel de chaves não depender do caminho do diretório da aplicação (que muda entre ambientes).</summary>
    public const string ApplicationName = "garage";

    private const string KeysPathSetting = "DataProtection:KeysPath";

    /// <summary>
    /// Nome fixo da aplicação e, com <c>DataProtection:KeysPath</c>, chaves em disco: sem isso as chaves ficam no perfil do
    /// usuário do contêiner e cada deploy as perde, derrubando as sessões (cookie <c>garage_auth</c>) e os tokens antiforgery.
    /// Sem o caminho (testes, desenvolvimento) nada é persistido por aqui.
    /// </summary>
    public static IServiceCollection AddGarageDataProtection(this IServiceCollection services, IConfiguration config)
    {
        var dataProtection = services.AddDataProtection().SetApplicationName(ApplicationName);

        var keysPath = config[KeysPathSetting];
        if (!string.IsNullOrWhiteSpace(keysPath))
            dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keysPath));

        return services;
    }
}
