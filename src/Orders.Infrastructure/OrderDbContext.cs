using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Orders.Domain;
using System.Text.Json;

namespace Orders.Infrastructure;

public sealed class OrderDbContext : DbContext
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public OrderDbContext(DbContextOptions<OrderDbContext> options) : base(options)
    {
    }

    public DbSet<CustomerOrder> Orders => Set<CustomerOrder>();
    public DbSet<Pallet> Pallets => Set<Pallet>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var timelineComparer = new ValueComparer<List<string>>(
            (left, right) =>
                ReferenceEquals(left, right) ||
                left != null && right != null && left.SequenceEqual(right),
            value => value.Aggregate(0, (hash, item) => HashCode.Combine(hash, item.GetHashCode())),
            value => value.ToList());

        modelBuilder.Entity<CustomerOrder>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.CustomerId).IsRequired();
            entity.Property(x => x.DestinationRegion).IsRequired();
            entity.Property(x => x.DockPreference).IsRequired();
            entity.Property(x => x.IdempotencyKey).HasMaxLength(200);
            entity.HasIndex(x => x.IdempotencyKey).IsUnique();

            entity.Navigation(x => x.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
            entity.OwnsMany(x => x.Lines, line =>
            {
                line.ToTable("OrderLines");
                line.WithOwner().HasForeignKey("OrderId");
                line.Property<int>("Id");
                line.HasKey("Id");
                line.Property(x => x.Sku).HasMaxLength(64).IsRequired();
                line.Property(x => x.Quantity).IsRequired();
                line.Property(x => x.WeightKg).HasColumnType("decimal(18,2)").IsRequired();
            });
        });

        modelBuilder.Entity<Pallet>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.WeightKg).HasColumnType("decimal(18,2)");
            entity.Property(x => x.AssignedRoute);

            entity
                .Property<List<string>>("_timeline")
                .HasColumnName("Timeline")
                .HasConversion(
                    value => JsonSerializer.Serialize(value, JsonOptions),
                    value => JsonSerializer.Deserialize<List<string>>(value, JsonOptions) ?? new List<string>())
                .Metadata.SetValueComparer(timelineComparer);
        });
    }
}
