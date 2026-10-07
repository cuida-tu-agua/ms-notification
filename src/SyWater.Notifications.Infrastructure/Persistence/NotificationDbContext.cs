using Microsoft.EntityFrameworkCore;
using SyWater.Notifications.Infrastructure.Persistence.Entities;

namespace SyWater.Notifications.Infrastructure.Persistence;

/// <summary>EF Core session against sy-water-db. The schema is owned by ms-notification-db (Liquibase): no EF migrations.</summary>
public class NotificationDbContext(DbContextOptions options) : DbContext(options)
{
    public DbSet<NotificationEntity> Notifications => Set<NotificationEntity>();
    public DbSet<NotificationReadEntity> Reads => Set<NotificationReadEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<NotificationReadEntity>().HasKey(r => new { r.NotificationId, r.UserId });   // PK_notification_reads

        // UX_notif_source_audience (SQL Server treats NULLs as equal in unique indexes; SQLite does not,
        // so the repository also checks before inserting)
        modelBuilder.Entity<NotificationEntity>()
            .HasIndex(n => new { n.SourceEventId, n.UserId, n.Role })
            .IsUnique().HasDatabaseName("UX_notif_source_audience");
    }
}
