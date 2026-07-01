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

            // Payment state. Store the status as a string so the schema is
            // stable if the enum is ever reordered.
            e.Property(o => o.PaymentStatus).HasConversion<string>().HasMaxLength(16).IsRequired();
            e.Property(o => o.Amount).HasPrecision(18, 2);
            e.Property(o => o.TxnRef).HasMaxLength(64);
            e.Property(o => o.VnpTransactionNo).HasMaxLength(64);
        });
    }
}
