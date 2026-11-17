using AppUserAPI.Models;
using Microsoft.EntityFrameworkCore;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<ApplicationUser> ApplicationUsers =>
        Set<ApplicationUser>();

    public DbSet<SecurityUser> Users =>
        Set<SecurityUser>();

    public DbSet<ApplicationLookup> Applications =>
        Set<ApplicationLookup>();

    public DbSet<RoleLookup> Roles =>
        Set<RoleLookup>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ApplicationUser>(entity =>
        {
            entity.HasNoKey();

            entity.ToView(
                "vwApplicationUsers",
                "Security");
        });

        modelBuilder.Entity<SecurityUser>(entity =>
        {
            entity.HasKey(x => x.UserID);

            entity.ToTable(
                "Users",
                "Security");

            entity.Property(x => x.UserID)
                .HasColumnName("UserID");

            entity.Property(x => x.UserName)
                .HasColumnName("UserName");

            entity.Property(x => x.Email)
                .HasColumnName("Email");

            entity.Property(x => x.FirstName)
                .HasColumnName("FirstName");

            entity.Property(x => x.LastName)
                .HasColumnName("LastName");

            entity.Property(x => x.IsActive)
                .HasColumnName("IsActive");

            entity.Property(x => x.EntraObjectId)
                .HasColumnName("EntraObjectId");
        });

        modelBuilder.Entity<ApplicationLookup>(entity =>
        {
            entity.HasKey(x => x.ApplicationID);

            entity.ToTable(
                "Applications",
                "Security");

            entity.Property(x => x.ApplicationID)
                .HasColumnName("ApplicationID");

            entity.Property(x => x.ApplicationCode)
                .HasColumnName("ApplicationCode");

            entity.Property(x => x.ApplicationName)
                .HasColumnName("ApplicationName");

            entity.Property(x => x.Description)
                .HasColumnName("Description");

            entity.Property(x => x.IsActive)
                .HasColumnName("IsActive");
        });

        modelBuilder.Entity<RoleLookup>(entity =>
        {
            entity.HasKey(x => x.RoleID);

            entity.ToTable(
                "Roles",
                "Security");

            entity.Property(x => x.RoleID)
                .HasColumnName("RoleID");

            entity.Property(x => x.ApplicationID)
                .HasColumnName("ApplicationID");

            entity.Property(x => x.RoleName)
                .HasColumnName("RoleName");

            entity.Property(x => x.Description)
                .HasColumnName("Description");

            entity.Property(x => x.IsActive)
                .HasColumnName("IsActive");
        });
    }
}