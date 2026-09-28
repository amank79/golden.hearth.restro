using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using RestaurantPos.Api.Data;

namespace RestaurantPos.Tests;

public class SyncFieldsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task New_rows_get_public_id_and_created_and_updated_times()
    {
        factory.Clock.Now = TestClock.India(2026, 10, 1, 10, 0);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PosDbContext>();

        var category = new Category { Name = "Sync test" };
        db.Categories.Add(category);
        await db.SaveChangesAsync();

        Assert.NotEqual(Guid.Empty, category.PublicId);
        Assert.Equal(factory.Clock.Now, category.CreatedAt);
        Assert.Equal(factory.Clock.Now, category.UpdatedAt);

        var created = category.CreatedAt;
        var publicId = category.PublicId;
        factory.Clock.Advance(TimeSpan.FromMinutes(5));
        category.Name = "Sync test 2";
        category.CreatedAt = DateTimeOffset.MinValue; // must be ignored
        await db.SaveChangesAsync();

        using var scope2 = factory.Services.CreateScope();
        var reloaded = await scope2.ServiceProvider.GetRequiredService<PosDbContext>().Categories.SingleAsync(c => c.Id == category.Id);
        Assert.Equal(publicId, reloaded.PublicId);
        Assert.Equal(created, reloaded.CreatedAt);
        Assert.Equal(factory.Clock.Now, reloaded.UpdatedAt);
    }

    [Fact]
    public async Task Rows_can_be_found_by_public_id()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PosDbContext>();
        var category = new Category { Name = "Find me" };
        db.Categories.Add(category);
        await db.SaveChangesAsync();

        using var scope2 = factory.Services.CreateScope();
        var found = await scope2.ServiceProvider.GetRequiredService<PosDbContext>().Categories.SingleAsync(c => c.PublicId == category.PublicId);
        Assert.Equal(category.Id, found.Id);
    }

    [Fact]
    public void Seeded_settings_row_has_fixed_public_id()
    {
        using var scope = factory.Services.CreateScope();
        var settings = scope.ServiceProvider.GetRequiredService<PosDbContext>().Settings.Single();
        Assert.Equal(PosDbContext.SettingsPublicId, settings.PublicId);
    }

    [Fact]
    public void Migration_gives_existing_rows_unique_public_ids()
    {
        var path = Path.Combine(Path.GetTempPath(), $"pos-migrate-{Guid.NewGuid():N}.db");
        try
        {
            var options = new DbContextOptionsBuilder<PosDbContext>().UseSqlite($"Data Source={path};Pooling=False").Options;
            using (var db = new PosDbContext(options, TimeProvider.System))
            {
                db.GetService<IMigrator>().Migrate("20260928172951_InitialCreate");
                db.Database.ExecuteSqlRaw("INSERT INTO Categories (Name, SortOrder, IsActive) VALUES ('A', 1, 1), ('B', 2, 1)");
                db.Database.Migrate();
            }

            using var check = new PosDbContext(options, TimeProvider.System);
            var ids = check.Categories.Select(c => c.PublicId).ToList();
            Assert.Equal(2, ids.Count);
            Assert.DoesNotContain(Guid.Empty, ids);
            Assert.Equal(2, ids.Distinct().Count());
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(path);
        }
    }
}
