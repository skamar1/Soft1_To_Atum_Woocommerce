using Microsoft.EntityFrameworkCore;
using Soft1_To_Atum.Data.Models;

namespace Soft1_To_Atum.Data;

public class SyncDbContext : DbContext
{
    public SyncDbContext(DbContextOptions<SyncDbContext> options) : base(options)
    {
    }

    public DbSet<Product> Products { get; set; }
    public DbSet<Store> Stores { get; set; }
    public DbSet<SyncLog> SyncLogs { get; set; }
    public DbSet<AppSettings> AppSettings { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.SoftOneId).IsUnique();
            entity.HasIndex(e => e.Sku);
            entity.Property(e => e.Name).HasMaxLength(255);
            entity.Property(e => e.Sku).HasMaxLength(100);
            entity.Property(e => e.Price).HasColumnType("decimal(18,2)");
            entity.Property(e => e.LastSyncStatus).HasMaxLength(50);
        });

        modelBuilder.Entity<Store>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(255);
            entity.Property(e => e.WooCommerceUrl).HasMaxLength(500);
            entity.Property(e => e.WooCommerceKey).HasMaxLength(255);
            entity.Property(e => e.WooCommerceSecret).HasMaxLength(255);
        });

        modelBuilder.Entity<SyncLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Status).HasMaxLength(50);
        });

        modelBuilder.Entity<AppSettings>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.StoreName).HasMaxLength(255);
            entity.Property(e => e.SoftOneGoBaseUrl).HasMaxLength(500);
            entity.Property(e => e.SoftOneGoAppId).HasMaxLength(50);
            entity.Property(e => e.SoftOneGoToken).HasMaxLength(500);
            entity.Property(e => e.SoftOneGoS1Code).HasMaxLength(100);
            entity.Property(e => e.SoftOneGoFilters).HasMaxLength(1000);
            entity.Property(e => e.WooCommerceUrl).HasMaxLength(500);
            entity.Property(e => e.WooCommerceConsumerKey).HasMaxLength(255);
            entity.Property(e => e.WooCommerceConsumerSecret).HasMaxLength(255);
            entity.Property(e => e.WooCommerceVersion).HasMaxLength(50);
            entity.Property(e => e.AtumLocationName).HasMaxLength(255);
            entity.Property(e => e.EmailSmtpHost).HasMaxLength(255);
            entity.Property(e => e.EmailUsername).HasMaxLength(255);
            entity.Property(e => e.EmailPassword).HasMaxLength(255);
            entity.Property(e => e.EmailFromEmail).HasMaxLength(255);
            entity.Property(e => e.EmailToEmail).HasMaxLength(255);
            entity.Property(e => e.MatchingPrimaryField).HasMaxLength(100);
            entity.Property(e => e.MatchingSecondaryField).HasMaxLength(100);
            entity.Property(e => e.FieldMappingSku).HasMaxLength(255);
            entity.Property(e => e.FieldMappingName).HasMaxLength(255);
            entity.Property(e => e.FieldMappingPrice).HasMaxLength(255);
            entity.Property(e => e.FieldMappingStockQuantity).HasMaxLength(255);
            entity.Property(e => e.FieldMappingCategory).HasMaxLength(255);
            entity.Property(e => e.FieldMappingUnit).HasMaxLength(255);
            entity.Property(e => e.FieldMappingVat).HasMaxLength(255);

            // Seed default data
            entity.HasData(new AppSettings
            {
                Id = 1,
                StoreName = "Κατάστημα Κέντρο",
                StoreEnabled = true,
                SoftOneGoBaseUrl = "https://go.s1cloud.net/s1services",
                SoftOneGoAppId = "703",
                SoftOneGoFilters = "ITEM.MTRL_ITEMTRDATA_QTY1=1&ITEM.MTRL_ITEMTRDATA_QTY1_TO=9999",
                WooCommerceVersion = "wc/v3",
                AtumLocationId = 870,
                AtumLocationName = "store1_location",
                EmailSmtpPort = 587,
                SyncIntervalMinutes = 15,
                SyncAutoSync = true,
                SyncEmailNotifications = true,
                MatchingPrimaryField = "sku",
                MatchingSecondaryField = "barcode",
                MatchingCreateMissingProducts = true,
                MatchingUpdateExistingProducts = true,
                FieldMappingSku = "ITEM.CODE1",
                FieldMappingName = "ITEM.NAME",
                FieldMappingPrice = "ITEM.PRICER",
                FieldMappingStockQuantity = "ITEM.MTRL_ITEMTRDATA_QTY1",
                FieldMappingCategory = "ITEM.MTRCATEGORY",
                FieldMappingUnit = "ITEM.MTRUNIT1",
                FieldMappingVat = "ITEM.VAT",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        });

        base.OnModelCreating(modelBuilder);
    }
}