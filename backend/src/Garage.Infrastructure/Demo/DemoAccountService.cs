using System.Data;
using Garage.Application.Abstractions.Ports;
using Garage.Domain.Attachments;
using Garage.Domain.Vehicles;
using Garage.Infrastructure.Identity;
using Garage.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Garage.Infrastructure.Demo;

/// <summary>
/// Contas de visitante da demo (spec 3): sem senha, com validade, e apagadas com tudo o que é delas (veículos, registros,
/// anexos e links saem em cascata pelo banco). Só mexe em contas com <see cref="AppUser.IsDemo"/>.
/// </summary>
public sealed partial class DemoAccountService(
    UserManager<AppUser> users,
    GarageDbContext db,
    IFileStorage storage,
    IAppClock clock,
    IOptions<DemoVisitorOptions> options,
    ILogger<DemoAccountService> logger)
{
    private const string EmailDomain = "demo.garagem.invalid";

    // pg_advisory_xact_lock: uma criação de conta demo por vez, em qualquer instância da API, para o teto valer.
    private const long CreationLockKey = 0x47415241_47450001;

    /// <summary>Contas demo ainda válidas (vencimento no futuro).</summary>
    public Task<int> CountActiveAsync(CancellationToken ct)
    {
        var now = clock.Now;
        return db.Users.CountAsync(user => user.IsDemo && user.DemoExpiresAt > now, ct);
    }

    /// <summary>
    /// Cria a conta se ainda houver vaga (menos de <paramref name="maxActive"/> contas válidas); sem vaga, devolve null.
    /// Contar e criar acontecem na mesma transação, sob uma trava do Postgres: visitantes simultâneos não furam o teto.
    /// </summary>
    public async Task<AppUser?> TryCreateAsync(int maxActive, CancellationToken ct)
    {
        // READ COMMITTED explícito: a contagem, feita depois da trava, enxerga a conta de quem acabou de soltá-la.
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({CreationLockKey})", ct);

        if (await CountActiveAsync(ct) >= maxActive)
            return null;

        var user = await CreateAsync(ct);
        // Sem o token da requisição: um cliente que cai durante o COMMIT não deixa uma conta gravada que a API não semeia.
        await transaction.CommitAsync(CancellationToken.None);
        return user;
    }

    /// <summary>Cria a conta sem senha (ninguém entra nela por login), vencendo em <c>TtlHours</c>.</summary>
    public async Task<AppUser> CreateAsync(CancellationToken ct)
    {
        var id = Guid.CreateVersion7();
        var email = $"demo-{id:N}@{EmailDomain}";
        // Em segundos inteiros, como a validade do cookie: a resposta, o banco e a sessão dizem o mesmo instante.
        var expiresAt = clock.Now.AddHours(options.Value.TtlHours);
        var user = new AppUser
        {
            Id = id,
            UserName = email,
            Email = email,
            IsDemo = true,
            DemoExpiresAt = expiresAt.AddTicks(-(expiresAt.Ticks % TimeSpan.TicksPerSecond))
        };

        var created = await users.CreateAsync(user);
        if (!created.Succeeded)
        {
            throw new InvalidOperationException(
                $"Não foi possível criar a conta demo: {string.Join("; ", created.Errors.Select(error => error.Code))}");
        }

        return user;
    }

    /// <summary>Marca a conta demo como vencida agora; a próxima limpeza a apaga.</summary>
    public async Task ExpireNowAsync(Guid userId, CancellationToken ct)
    {
        var now = clock.Now;
        await db.Users
            .Where(user => user.Id == userId && user.IsDemo)
            .ExecuteUpdateAsync(setters => setters.SetProperty(user => user.DemoExpiresAt, now), ct);
    }

    /// <summary>
    /// Um lote da limpeza (spec 3.3): até <paramref name="batchSize"/> contas demo vencidas saem com tudo o que é delas (o
    /// banco apaga veículos, registros, anexos e links em cascata) e, depois do commit, os arquivos dos anexos, um por vez
    /// (a falha de um vai para o log e não impede os outros). Idempotente: no pior caso sobra arquivo sem linha.
    /// </summary>
    public async Task<DemoPurgeResult> PurgeExpiredAsync(int batchSize, CancellationToken ct)
    {
        var now = clock.Now;
        var expired = await db.Users
            .Where(user => user.IsDemo && user.DemoExpiresAt <= now)
            .OrderBy(user => user.DemoExpiresAt)
            .Select(user => user.Id)
            .Take(batchSize)
            .ToListAsync(ct);
        if (expired.Count == 0)
            return new DemoPurgeResult(0, 0);

        var keys = await StorageKeysOfAsync(expired, ct);

        var accounts = await db.Users
            .Where(user => expired.Contains(user.Id) && user.IsDemo && user.DemoExpiresAt <= now)
            .ExecuteDeleteAsync(ct);

        return new DemoPurgeResult(accounts, await DeleteFilesAsync(keys));
    }

    /// <summary>
    /// Apaga a conta demo e, em cascata, tudo o que é dela; depois, os arquivos dos anexos (o caminho da semeadura que
    /// falha depois de copiar as fotos). Conta real não é tocada.
    /// </summary>
    public async Task DeleteAsync(Guid userId, CancellationToken ct)
    {
        var keys = await StorageKeysOfAsync([userId], ct);

        var deleted = await db.Users
            .Where(user => user.Id == userId && user.IsDemo)
            .ExecuteDeleteAsync(ct);

        if (deleted > 0)
            await DeleteFilesAsync(keys);
    }

    private Task<List<string>> StorageKeysOfAsync(List<Guid> ownerIds, CancellationToken ct) =>
        db.Set<Attachment>()
            .Where(attachment => db.Set<Vehicle>().Any(vehicle => vehicle.Id == attachment.VehicleId && ownerIds.Contains(vehicle.OwnerId)))
            .Select(attachment => attachment.StorageKey)
            .ToListAsync(ct);

    // As linhas já saíram: os arquivos vão até o fim, mesmo com a parada do serviço; a falha de um não impede os outros.
    private async Task<int> DeleteFilesAsync(List<string> keys)
    {
        var files = 0;
        foreach (var key in keys)
        {
            try
            {
                await storage.DeleteAsync(key, CancellationToken.None);
                files++;
            }
            catch (Exception ex)
            {
                LogFileDeleteFailed(logger, key, ex);
            }
        }

        return files;
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "Demo cleanup could not delete the file {StorageKey}")]
    private static partial void LogFileDeleteFailed(ILogger logger, string storageKey, Exception exception);
}

/// <summary>Quantas contas demo e quantos arquivos um lote da limpeza removeu.</summary>
public sealed record DemoPurgeResult(int Accounts, int Files);
