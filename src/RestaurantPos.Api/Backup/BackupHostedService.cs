using Microsoft.Extensions.Options;

namespace RestaurantPos.Api.Backup;

/// <summary>
/// Checks every hour and backs up when the newest backup is older than IntervalHours
/// (so a laptop that was off at night still gets its daily backup soon after it starts).
/// Also backs up when the app stops, if anything changed.
/// </summary>
public sealed class BackupHostedService(
    DatabaseBackup backup, IOptionsMonitor<BackupOptions> options, TimeProvider clock, ILogger<BackupHostedService> log)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            // Let the app finish starting before the first check.
            await Task.Delay(TimeSpan.FromSeconds(30), clock, stoppingToken);
            while (true)
            {
                if (options.CurrentValue.Enabled && backup.IsDue()) await RunSafelyAsync(null, stoppingToken);
                await Task.Delay(TimeSpan.FromMinutes(options.CurrentValue.CheckEveryMinutes), clock, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // App is stopping.
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        if (options.CurrentValue is { Enabled: true, BackupOnShutdown: true } && backup.ChangedSinceLastBackup())
            await RunSafelyAsync(null, cancellationToken);
    }

    private async Task RunSafelyAsync(string? label, CancellationToken ct)
    {
        try
        {
            await backup.RunAsync(label, ct);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            log.LogError(e, "Scheduled backup failed");
        }
    }
}
