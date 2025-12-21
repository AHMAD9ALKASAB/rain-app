using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Rain.Domain.Entities;
using Rain.Infrastructure.Identity;

namespace Rain.Infrastructure.Persistence
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser, IdentityRole, string>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        public DbSet<Category> Categories => Set<Category>();
        public DbSet<Brand> Brands => Set<Brand>();
        public DbSet<Product> Products => Set<Product>();
        public DbSet<ProductImage> ProductImages => Set<ProductImage>();
        public DbSet<SupplierProfile> SupplierProfiles => Set<SupplierProfile>();
        public DbSet<ShopProfile> ShopProfiles => Set<ShopProfile>();
        public DbSet<SupplierOffer> SupplierOffers => Set<SupplierOffer>();
        public DbSet<SupplierApplication> SupplierApplications => Set<SupplierApplication>();
        public DbSet<Address> Addresses => Set<Address>();
        public DbSet<Order> Orders => Set<Order>();
        public DbSet<OrderItem> OrderItems => Set<OrderItem>();
        public DbSet<Review> Reviews => Set<Review>();
        public DbSet<ComparisonList> ComparisonLists => Set<ComparisonList>();
        public DbSet<ComparisonItem> ComparisonItems => Set<ComparisonItem>();
        public DbSet<Rain.Domain.Entities.Payment> Payments => Set<Rain.Domain.Entities.Payment>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<Category>()
                .HasOne(c => c.Parent)
                .WithMany(c => c.Children)
                .HasForeignKey(c => c.ParentId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Product>()
                .HasOne(p => p.Category)
                .WithMany(c => c.Products)
                .HasForeignKey(p => p.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Product>()
                .HasOne(p => p.Brand)
                .WithMany(b => b.Products)
                .HasForeignKey(p => p.BrandId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<ProductImage>()
                .HasOne(pi => pi.Product)
                .WithMany(p => p.Images)
                .HasForeignKey(pi => pi.ProductId);

            builder.Entity<SupplierOffer>()
                .HasIndex(o => new { o.ProductId, o.SupplierId });

            // Decimal precision
            builder.Entity<SupplierOffer>()
                .Property(o => o.Price)
                .HasColumnType("decimal(18,2)");

            builder.Entity<OrderItem>()
                .Property(oi => oi.UnitPrice)
                .HasColumnType("decimal(18,2)");
            builder.Entity<OrderItem>()
                .Property(oi => oi.CommissionRate)
                .HasColumnType("decimal(5,4)");
            builder.Entity<OrderItem>()
                .Property(oi => oi.CommissionAmount)
                .HasColumnType("decimal(18,2)");
            builder.Entity<OrderItem>()
                .Property(oi => oi.NetToSupplier)
                .HasColumnType("decimal(18,2)");

            builder.Entity<Order>()
                .Property(o => o.Total)
                .HasColumnType("decimal(18,2)");

            builder.Entity<Rain.Domain.Entities.Payment>()
                .Property(p => p.Amount)
                .HasColumnType("decimal(18,2)");

            builder.Entity<OrderItem>()
                .HasOne(oi => oi.Order)
                .WithMany(o => o.Items)
                .HasForeignKey(oi => oi.OrderId);

            builder.Entity<OrderItem>()
                .HasOne(oi => oi.SupplierOffer)
                .WithMany()
                .HasForeignKey(oi => oi.SupplierOfferId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<ComparisonItem>()
                .HasOne(ci => ci.ComparisonList)
                .WithMany(cl => cl.Items)
                .HasForeignKey(ci => ci.ComparisonListId);

            builder.Entity<ComparisonItem>()
                .HasOne(ci => ci.SupplierOffer)
                .WithMany()
                .HasForeignKey(ci => ci.SupplierOfferId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
