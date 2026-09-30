using Microsoft.Extensions.DependencyInjection.Extensions;

namespace RestaurantPos.Api.Backup;

public static class BackupEndpoints
{
    public static IServiceCollection AddPosBackup(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<BackupOptions>(config.GetSection("Backup"));
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<DatabaseBackup>();
        services.AddHostedService<BackupHostedService>();
        return services;
    }

    public static void MapBackupEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/backup");

        group.MapGet("/status", (DatabaseBackup backup, TimeProvider clock) =>
        {
            var newest = backup.NewestBackupTime();
            return Results.Ok(new
            {
                newestBackupAt = newest,
                // Shown as a warning on screen when true.
                overdue = newest is null || clock.GetLocalNow().DateTime - newest.Value > TimeSpan.FromDays(2),
                folder = backup.Folder,
                extraFolders = backup.ExtraFolders.Select(f => new { path = f, connected = DatabaseBackup.IsAvailable(f) }),
                lastResult = backup.LastResult,
            });
        });

        group.MapPost("/run", async (DatabaseBackup backup, CancellationToken ct) =>
        {
            var result = await backup.RunAsync(null, ct);
            return result.Success ? Results.Ok(result) : Results.Json(result, statusCode: StatusCodes.Status500InternalServerError);
        });
    }
}
