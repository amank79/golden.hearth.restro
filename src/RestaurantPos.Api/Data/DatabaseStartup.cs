using Microsoft.EntityFrameworkCore;
using RestaurantPos.Api.Backup;

namespace RestaurantPos.Api.Data;

public static class DatabaseStartup
{
    /// <summary>
    /// Applies pending migrations. When an update changes an existing database, a backup is taken first,
    /// and the update is refused if that backup fails, so an update can always be undone.
    /// </summary>
    public static async Task MigrateDatabaseAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PosDbContext>();

        var hasData = (await db.Database.GetAppliedMigrationsAsync()).Any();
        var hasPending = (await db.Database.GetPendingMigrationsAsync()).Any();
        if (hasData && hasPending)
        {
            var result = await scope.ServiceProvider.GetRequiredService<DatabaseBackup>().RunAsync("before-update");
            if (!result.Success)
                throw new InvalidOperationException("Backup before the database update failed, so the update was not applied: "
                    + string.Join("; ", result.Messages));
        }

        await db.Database.MigrateAsync();
    }
}
