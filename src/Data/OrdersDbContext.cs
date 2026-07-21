using Microsoft.EntityFrameworkCore;
using OrderService.Domain;

namespace OrderService.Data;

public class OrdersDbContext : DbContext
{
    public OrdersDbContext(DbContextOptions<OrdersDbContext> options) : base(options) { }

    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>(e =>
        {
            e.HasKey(o => o.Id);
            e.Property(o => o.Sku).HasMaxLength(64).IsRequired();
            e.Property(o => o.Name).HasMaxLength(256).IsRequired();
            e.Property(o => o.UnitPrice).HasColumnType("decimal(18,2)");
            e.Property(o => o.Currency).HasMaxLength(8).IsRequired();
            e.Property(o => o.Status).HasMaxLength(32).IsRequired();
        });
    }
}
