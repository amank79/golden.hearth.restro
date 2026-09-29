using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace RestaurantPos.Api.Data;

public class PosDbContext(DbContextOptions<PosDbContext> options, TimeProvider clock) : DbContext(options)
{
    // Fixed values for the seeded settings row, so the migration never changes on regeneration.
    public static readonly Guid SettingsPublicId = new("6f1c8c52-3a4e-4b8e-9d0a-5b1f2e7c9a01");
    public static readonly DateTimeOffset SeedTime = new(2026, 9, 28, 0, 0, 0, TimeSpan.Zero);

    public DbSet<RestaurantSettings> Settings => Set<RestaurantSettings>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<MenuItem> MenuItems => Set<MenuItem>();
    public DbSet<ItemVariant> ItemVariants => Set<ItemVariant>();
    public DbSet<Bill> Bills => Set<Bill>();
    public DbSet<BillLine> BillLines => Set<BillLine>();
    public DbSet<Payment> Payments => Set<Payment>();

    protected override void ConfigureConventions(ModelConfigurationBuilder b)
    {
        // Enums as readable text; timestamps as UTC ticks so SQLite can sort and filter them.
        b.Properties<Enum>().HaveConversion<string>();
        b.Properties<DateTimeOffset>().HaveConversion<DateTimeOffsetToBinaryConverter>();
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<RestaurantSettings>().HasData(new RestaurantSettings
        {
            PublicId = SettingsPublicId,
            CreatedAt = SeedTime,
            UpdatedAt = SeedTime,
        });

        // Every entity gets a unique PublicId for the future cloud sync.
        foreach (var type in b.Model.GetEntityTypes().Where(t => typeof(Entity).IsAssignableFrom(t.ClrType)))
        {
            b.Entity(type.ClrType).HasIndex(nameof(Entity.PublicId)).IsUnique();
        }

        b.Entity<Category>().Property(c => c.Name).HasMaxLength(60);
        b.Entity<MenuItem>().Property(i => i.Name).HasMaxLength(100);
        b.Entity<MenuItem>().HasIndex(i => i.ShortCode);
        b.Entity<ItemVariant>().Property(v => v.Name).HasMaxLength(30);

        b.Entity<Bill>().HasIndex(x => new { x.FinancialYear, x.SeqNo }).IsUnique();
        b.Entity<Bill>().HasIndex(x => x.BillNo).IsUnique();
        b.Entity<Bill>().HasIndex(x => x.Status);
        b.Entity<Bill>().HasMany(x => x.Lines).WithOne().HasForeignKey(l => l.BillId);
        b.Entity<Bill>().HasMany(x => x.Payments).WithOne().HasForeignKey(p => p.BillId);

        // Bills are never deleted, and menu rows used on bills must not be deleted either (deactivate instead).
        b.Entity<BillLine>().HasOne<MenuItem>().WithMany().HasForeignKey(l => l.MenuItemId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<BillLine>().HasOne<ItemVariant>().WithMany().HasForeignKey(l => l.ItemVariantId).OnDelete(DeleteBehavior.Restrict);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        StampTimes();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        StampTimes();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>Sets CreatedAt/UpdatedAt on new and changed rows, and makes sure every new row has a PublicId.</summary>
    private void StampTimes()
    {
        var now = clock.GetUtcNow();
        foreach (var entry in ChangeTracker.Entries<Entity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    if (entry.Entity.PublicId == Guid.Empty) entry.Entity.PublicId = Guid.NewGuid();
                    entry.Entity.CreatedAt = now;
                    entry.Entity.UpdatedAt = now;
                    break;
                case EntityState.Modified:
                    entry.Property(e => e.CreatedAt).IsModified = false;
                    entry.Property(e => e.PublicId).IsModified = false;
                    entry.Entity.UpdatedAt = now;
                    break;
            }
        }
    }
}
