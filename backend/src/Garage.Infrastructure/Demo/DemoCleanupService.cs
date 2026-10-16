using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Garage.Infrastructure.Demo;

/// <summary>
/// Limpeza das contas demo vencidas (spec 3.3): uma rodada na subida da API e depois a cada
/// <c>Demo:Visitors:CleanupIntervalMinutes</c>, em lotes de 50, repetidos enquanto o lote vier cheio. Roda mesmo com a
/// demo desligada (as contas que sobraram também vencem) e não roda com o intervalo 0. A rodada na subida mantém o prazo
/// de 25 horas mesmo quando a API reinicia (deploy) antes de completar um intervalo.
/// </summary>
internal sealed partial class DemoCleanupService(
    IServiceScopeFactory scopes,
    IOptions<DemoVisitorOptions> options,
    TimeProvider time,
    ILogger<DemoCleanupService> logger) : BackgroundService
{
    internal const int BatchSize = 50;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = options.Value.CleanupIntervalMinutes;
        if (interval <= 0)
            return;

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(interval), time);
        do
        {
            await TryRunOnceAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task TryRunOnceAsync(CancellationToken ct)
    {
        try
        {
            await RunOnceAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Uma rodada que falha (banco fora do ar, por exemplo) não derruba a API: a próxima tenta de novo.
            LogRunFailed(logger, ex);
        }
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        int accounts = 0, files = 0;
        DemoPurgeResult batch;
        do
        {
            await using var scope = scopes.CreateAsyncScope();
            batch = await scope.ServiceProvider.GetRequiredService<DemoAccountService>().PurgeExpiredAsync(BatchSize, ct);
            accounts += batch.Accounts;
            files += batch.Files;
        }
        while (batch.Accounts == BatchSize);

        LogRunCompleted(logger, accounts, files);
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information,
        Message = "Demo cleanup removed {Accounts} expired accounts and {Files} files")]
    private static partial void LogRunCompleted(ILogger logger, int accounts, int files);

    [LoggerMessage(EventId = 2, Level = LogLevel.Error, Message = "Demo cleanup run failed")]
    private static partial void LogRunFailed(ILogger logger, Exception exception);
}
