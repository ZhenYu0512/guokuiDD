using GuokuiDD.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GuokuiDD.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Store> Stores => Set<Store>();
    public DbSet<Banner> Banners => Set<Banner>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<SpecGroup> SpecGroups => Set<SpecGroup>();
    public DbSet<SpecOption> SpecOptions => Set<SpecOption>();
    public DbSet<Addon> Addons => Set<Addon>();
    public DbSet<User> Users => Set<User>();
    public DbSet<MemberCard> MemberCards => Set<MemberCard>();
    public DbSet<Coupon> Coupons => Set<Coupon>();
    public DbSet<UserCoupon> UserCoupons => Set<UserCoupon>();
    public DbSet<PointsRecord> PointsRecords => Set<PointsRecord>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Printer> Printers => Set<Printer>();
    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<Store>(e => { e.ToTable("Stores"); e.Property(p => p.Name).HasMaxLength(100); e.Property(p => p.Address).HasMaxLength(200); e.Property(p => p.Phone).HasMaxLength(30); e.Property(p => p.BusinessHours).HasMaxLength(50); e.Property(p => p.Notice).HasMaxLength(500); });
        mb.Entity<Banner>(e => { e.ToTable("Banners"); e.Property(p => p.ImageUrl).HasMaxLength(300); e.Property(p => p.LinkUrl).HasMaxLength(300); });
        mb.Entity<Category>(e => { e.ToTable("Categories"); e.Property(p => p.Name).HasMaxLength(50); });
        mb.Entity<Product>(e => { e.ToTable("Products"); e.Property(p => p.Name).HasMaxLength(100); e.Property(p => p.ImageUrl).HasMaxLength(300); e.Property(p => p.Description).HasMaxLength(500); });
        mb.Entity<SpecGroup>(e => { e.ToTable("SpecGroups"); e.Property(p => p.Name).HasMaxLength(50); });
        mb.Entity<SpecOption>(e => { e.ToTable("SpecOptions"); e.Property(p => p.Name).HasMaxLength(50); });
        mb.Entity<Addon>(e => { e.ToTable("Addons"); e.Property(p => p.Name).HasMaxLength(50); });
        mb.Entity<User>(e => { e.ToTable("Users"); e.HasIndex(p => p.OpenId).IsUnique(); e.Property(p => p.OpenId).HasMaxLength(64); e.Property(p => p.NickName).HasMaxLength(50); e.Property(p => p.AvatarUrl).HasMaxLength(300); e.Property(p => p.Phone).HasMaxLength(20); });
        mb.Entity<MemberCard>(e => { e.ToTable("MemberCards"); e.HasIndex(p => p.UserId).IsUnique(); e.Property(p => p.CardNo).HasMaxLength(30); });
        mb.Entity<Coupon>(e => { e.ToTable("Coupons"); e.Property(p => p.Name).HasMaxLength(100); });
        mb.Entity<UserCoupon>(e => { e.ToTable("UserCoupons"); });
        mb.Entity<PointsRecord>(e => { e.ToTable("PointsRecords"); e.Property(p => p.Reason).HasMaxLength(100); });
        mb.Entity<Order>(e =>
        {
            e.ToTable("Orders");
            e.HasIndex(p => p.OrderNo).IsUnique();
            // 取餐码：门店当日唯一
            e.HasIndex(p => new { p.StoreId, p.PickupDate, p.PickupCode }).IsUnique()
             .HasFilter("[PickupCode] IS NOT NULL");
            e.Property(p => p.OrderNo).HasMaxLength(40);
            e.Property(p => p.PickupCode).HasMaxLength(4);
            e.Property(p => p.PickupDate).HasMaxLength(8);
            e.Property(p => p.ContactName).HasMaxLength(30);
            e.Property(p => p.ContactPhone).HasMaxLength(20);
            e.Property(p => p.Remark).HasMaxLength(200);
            e.Property(p => p.CancelReason).HasMaxLength(200);
        });
        mb.Entity<OrderItem>(e =>
        {
            e.ToTable("OrderItems");
            e.Property(p => p.ProductName).HasMaxLength(100);
            e.Property(p => p.ProductImage).HasMaxLength(300);
            e.Property(p => p.SpecsJson).HasColumnType("nvarchar(max)");
            e.Property(p => p.AddonsJson).HasColumnType("nvarchar(max)");
        });
        mb.Entity<Printer>(e => { e.ToTable("Printers"); e.Property(p => p.Name).HasMaxLength(50); e.Property(p => p.ConnType).HasMaxLength(10); e.Property(p => p.Sn).HasMaxLength(64); e.Property(p => p.AppKey).HasMaxLength(64); e.Property(p => p.Address).HasMaxLength(100); });
        mb.Entity<AdminUser>(e => { e.ToTable("AdminUsers"); e.HasIndex(p => p.Username).IsUnique(); e.Property(p => p.Username).HasMaxLength(50); e.Property(p => p.PasswordHash).HasMaxLength(128); e.Property(p => p.Role).HasMaxLength(20); });
    }
}
