using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Pomelo.EntityFrameworkCore.MySql.Scaffolding.Internal;

namespace Gamesgiftshop.Models;

public partial class Csi402dbContext : DbContext
{
    public Csi402dbContext(DbContextOptions<Csi402dbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Order> Orders { get; set; }

    public virtual DbSet<Orderdetail> Orderdetails { get; set; }

    public virtual DbSet<Promotion> Promotions { get; set; }

    public virtual DbSet<Product> Products { get; set; }

    public virtual DbSet<ProductReview> ProductReviews { get; set; }

    public virtual DbSet<ProductSizeStock> ProductSizeStocks { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<UserNotification> UserNotifications { get; set; }

    public virtual DbSet<UserFavorite> UserFavorites { get; set; }

    public virtual DbSet<AccountingExpense> AccountingExpenses { get; set; }

    public virtual DbSet<RefundRequest> RefundRequests { get; set; }

    public virtual DbSet<RefundEvidence> RefundEvidence { get; set; }

    public virtual DbSet<Useraddress> Useraddresses { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .UseCollation("utf8mb4_unicode_ci")
            .HasCharSet("utf8mb4");

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(e => e.OrderId).HasName("PRIMARY");

            entity.ToTable("orders");

            entity.Property(e => e.OrderDate)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("datetime");
            entity.Property(e => e.PaymentSlipUrl).HasMaxLength(255);
            entity.Property(e => e.ShippingAddress).HasColumnType("text");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasDefaultValueSql("'Pending'");
            entity.Property(e => e.TotalAmount).HasPrecision(10, 2);
            entity.Property(e => e.LoyaltyPointsRedeemed).HasDefaultValue(0);
            entity.Property(e => e.AppliedPromoCode).HasMaxLength(40);
            entity.Property(e => e.TrackingNumber).HasMaxLength(100);
            entity.HasIndex(e => new { e.UserId, e.AppliedPromoCode });
        });

        modelBuilder.Entity<Promotion>(entity =>
        {
            entity.HasKey(e => e.PromotionId).HasName("PRIMARY");
            entity.ToTable("promotions");
            entity.HasIndex(e => e.Code).IsUnique();
            entity.Property(e => e.Name).HasMaxLength(120);
            entity.Property(e => e.Type).HasMaxLength(20);
            entity.Property(e => e.Code).HasMaxLength(40);
            entity.Property(e => e.Category).HasMaxLength(100);
            entity.Property(e => e.MinimumOrderAmount).HasPrecision(10, 2);
            entity.Property(e => e.DiscountPercent).HasPrecision(5, 2);
            entity.Property(e => e.DiscountAmount).HasPrecision(10, 2);
            entity.Property(e => e.StartsAt).HasColumnType("datetime");
            entity.Property(e => e.EndsAt).HasColumnType("datetime");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.FirstOrderOnly).HasDefaultValue(false);
        });

        modelBuilder.Entity<Orderdetail>(entity =>
        {
            entity.HasKey(e => e.OrderDetailId).HasName("PRIMARY");

            entity.ToTable("orderdetails");

            entity.HasIndex(e => e.OrderId, "OrderId");

            entity.HasIndex(e => e.ProductId, "ProductId");

            entity.Property(e => e.Price).HasPrecision(10, 2);
            entity.Property(e => e.Size).HasMaxLength(10);

            entity.HasOne(d => d.Order).WithMany(p => p.Orderdetails)
                .HasForeignKey(d => d.OrderId)
                .HasConstraintName("orderdetails_ibfk_1");

            entity.HasOne(d => d.Product).WithMany(p => p.Orderdetails)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("orderdetails_ibfk_2");
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(e => e.ProductId).HasName("PRIMARY");

            entity.ToTable("products");

            entity.Property(e => e.Category).HasMaxLength(100);
            entity.Property(e => e.BadgeType).HasMaxLength(20);
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("datetime");
            entity.Property(e => e.ImageUrl).HasMaxLength(500);
            entity.Property(e => e.IsTrending).HasDefaultValueSql("'0'");
            entity.Property(e => e.Name).HasMaxLength(255);
            entity.Property(e => e.OriginalPrice).HasPrecision(10, 2);
            entity.Property(e => e.Price).HasPrecision(10, 2);
            entity.Property(e => e.Rating)
                .HasPrecision(3, 2)
                .HasDefaultValueSql("'0.00'");
            entity.Property(e => e.ReviewCount).HasDefaultValueSql("'0'");
            entity.Property(e => e.StockQuantity).HasDefaultValueSql("'0'");
        });

        modelBuilder.Entity<ProductSizeStock>(entity =>
        {
            entity.HasKey(e => new { e.ProductId, e.Size });
            entity.ToTable("productsizestocks");
            entity.Property(e => e.Size).HasMaxLength(10);
            entity.HasOne(e => e.Product).WithMany(e => e.SizeStocks)
                .HasForeignKey(e => e.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProductReview>(entity =>
        {
            entity.HasKey(e => e.ReviewId);
            entity.ToTable("productreviews");
            entity.Property(e => e.Comment).HasMaxLength(1000);
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("datetime");
            entity.Property(e => e.UserId).HasColumnName("UserID");
            entity.HasIndex(e => e.ProductId);
            entity.HasIndex(e => new { e.UserId, e.ProductId }).IsUnique();
            entity.HasOne<Product>()
                .WithMany()
                .HasForeignKey(e => e.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PRIMARY");

            entity.ToTable("users");

            entity.HasIndex(e => e.Email, "Email").IsUnique();

            entity.Property(e => e.UserId).HasColumnName("UserID");
            entity.Property(e => e.Email).HasMaxLength(100);
            entity.Property(e => e.Password).HasMaxLength(255);
            entity.Property(e => e.Phone).HasMaxLength(20);
            entity.Property(e => e.Role)
                .HasMaxLength(20)
                .HasDefaultValueSql("'User'");
            entity.Property(e => e.Username).HasMaxLength(50);
        });

        modelBuilder.Entity<UserNotification>(entity =>
        {
            entity.HasKey(e => e.UserNotificationId).HasName("PRIMARY");
            entity.ToTable("usernotifications");
            entity.Property(e => e.UserId).HasColumnName("UserID");
            entity.Property(e => e.Title).HasMaxLength(120);
            entity.Property(e => e.Message).HasMaxLength(500);
            entity.Property(e => e.IsRead).HasDefaultValue(false);
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("datetime");
            entity.HasIndex(e => new { e.UserId, e.IsRead, e.CreatedAt });
            entity.HasOne(e => e.User).WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("usernotifications_ibfk_1");
        });

        modelBuilder.Entity<UserFavorite>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.ProductId });
            entity.ToTable("userfavorites");
            entity.Property(e => e.UserId).HasColumnName("UserID");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("datetime");
            entity.HasIndex(e => e.ProductId);
            entity.HasOne(e => e.User).WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("userfavorites_ibfk_1");
            entity.HasOne(e => e.Product).WithMany()
                .HasForeignKey(e => e.ProductId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("userfavorites_ibfk_2");
        });

        modelBuilder.Entity<AccountingExpense>(entity =>
        {
            entity.HasKey(e => e.AccountingExpenseId).HasName("PRIMARY");
            entity.ToTable("accountingexpenses");
            entity.Property(e => e.Description).HasMaxLength(200);
            entity.Property(e => e.Category).HasMaxLength(80);
            entity.Property(e => e.Amount).HasPrecision(10, 2);
            entity.Property(e => e.ExpenseDate).HasColumnType("date");
            entity.Property(e => e.CreatedBy).HasMaxLength(50);
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("datetime");
            entity.HasIndex(e => e.ExpenseDate);
        });

        modelBuilder.Entity<RefundRequest>(entity =>
        {
            entity.HasKey(e => e.RefundRequestId).HasName("PRIMARY");
            entity.ToTable("refundrequests");
            entity.Property(e => e.UserId).HasColumnName("UserID");
            entity.Property(e => e.Reason).HasMaxLength(1000);
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .HasDefaultValue(RefundRequest.PendingStatus);
            entity.Property(e => e.Feedback).HasMaxLength(1000);
            entity.Property(e => e.ReturnAddress).HasMaxLength(1000);
            entity.Property(e => e.ReturnTrackingNumber).HasMaxLength(100);
            entity.Property(e => e.RequestedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("datetime");
            entity.Property(e => e.ReviewedAt).HasColumnType("datetime");
            entity.Property(e => e.ReviewedBy).HasMaxLength(50);
            entity.HasIndex(e => new { e.Status, e.RequestedAt });
            entity.HasIndex(e => new { e.Status, e.ReviewedAt });
            entity.HasOne(e => e.Order).WithMany()
                .HasForeignKey(e => e.OrderId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("refundrequests_ibfk_1");
            entity.HasOne(e => e.User).WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("refundrequests_ibfk_2");
        });

        modelBuilder.Entity<RefundEvidence>(entity =>
        {
            entity.ToTable("refundevidence");
            entity.HasKey(e => e.RefundRequestId);
            entity.Property(e => e.RefundRequestId).ValueGeneratedNever();
            entity.Property(e => e.Data).HasColumnType("longblob");
            entity.Property(e => e.ContentType).HasMaxLength(20);
            entity.HasOne(e => e.Request).WithOne(e => e.Evidence)
                .HasForeignKey<RefundEvidence>(e => e.RefundRequestId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Useraddress>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("useraddresses");

            entity.HasIndex(e => e.UserId, "FK_UserAddresses_Users");

            entity.Property(e => e.AddressDetail).HasColumnType("text");

            entity.HasOne(d => d.User).WithMany(p => p.Useraddresses)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK_UserAddresses_Users");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
