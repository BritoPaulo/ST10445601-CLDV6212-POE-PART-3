using Microsoft.EntityFrameworkCore;
using ABCRetailers.Models.SQL;

namespace ABCRetailers.Data
{
    public class AuthDbContext : DbContext
    {
        public AuthDbContext(DbContextOptions<AuthDbContext> options) : base(options)
        {
        }

        public DbSet<SqlUser> Users { get; set; }
        public DbSet<SqlCart> ShoppingCart { get; set; }
        public DbSet<SqlOrder> Orders { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // User configuration
            modelBuilder.Entity<SqlUser>(entity =>
            {
                entity.ToTable("Users");
                entity.HasKey(e => e.UserId);
                entity.HasIndex(e => e.Username).IsUnique();
                entity.HasIndex(e => e.Email).IsUnique();
                entity.Property(e => e.Role).HasDefaultValue("Customer");
                entity.Property(e => e.CreatedDate).HasDefaultValueSql("GETDATE()");
                entity.Property(e => e.IsActive).HasDefaultValue(true);
            });

            // ShoppingCart configuration - SIMPLIFIED
            modelBuilder.Entity<SqlCart>(entity =>
            {
                entity.ToTable("ShoppingCart");
                entity.HasKey(e => e.CartId);

                // Remove complex relationships - just use simple foreign key
                entity.HasIndex(e => e.UserId); // Index for performance

                entity.Property(e => e.AddedDate).HasDefaultValueSql("GETDATE()");
                entity.Property(e => e.ProductId).HasMaxLength(100).IsRequired();

                // NO relationship configuration to avoid extra columns
            });

            // Order configuration - SIMPLIFIED
            modelBuilder.Entity<SqlOrder>(entity =>
            {
                entity.ToTable("Orders");
                entity.HasKey(e => e.OrderId);

                // Remove complex relationships - just use simple foreign key
                entity.HasIndex(e => e.UserId); // Index for performance

                entity.Property(e => e.OrderDate).HasDefaultValueSql("GETDATE()");
                entity.Property(e => e.Status).HasDefaultValue("Submitted");
                entity.Property(e => e.UnitPrice).HasColumnType("decimal(18,2)");
                entity.Property(e => e.TotalPrice).HasColumnType("decimal(18,2)");

                // NO relationship configuration to avoid extra columns
            });

            base.OnModelCreating(modelBuilder);
        }
    }
}