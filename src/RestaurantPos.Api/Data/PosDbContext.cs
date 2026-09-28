using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace RestaurantPos.Api.Data;

public class PosDbContext(DbContextOptions<PosDbContext> options) : DbContext(options)
{
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
        b.Entity<RestaurantSettings>().HasData(new RestaurantSettings());

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
}
