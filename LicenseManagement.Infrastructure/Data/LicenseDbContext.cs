using System;
using LicenseManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LicenseManagement.Infrastructure.Data
{
    public class LicenseDbContext : DbContext
    {
        public LicenseDbContext(DbContextOptions<LicenseDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users => Set<User>();
        public DbSet<License> Licenses => Set<License>();
        public DbSet<ActivationCode> ActivationCodes => Set<ActivationCode>();
        public DbSet<Device> Devices => Set<Device>();
        public DbSet<LicenseEvent> LicenseEvents => Set<LicenseEvent>();
        public DbSet<AdminUser> AdminUsers => Set<AdminUser>();
        public DbSet<RegistrationRequest> RegistrationRequests => Set<RegistrationRequest>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            base.OnConfiguring(optionsBuilder);
            optionsBuilder.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning));
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // User configuration
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Email).HasMaxLength(256).IsRequired();
                entity.Property(e => e.NormalizedEmail).HasMaxLength(256).IsRequired();
                entity.HasIndex(e => e.NormalizedEmail).IsUnique();

                entity.HasMany(e => e.Licenses)
                      .WithOne(l => l.User)
                      .HasForeignKey(l => l.UserId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasMany(e => e.Events)
                      .WithOne(ev => ev.User)
                      .HasForeignKey(ev => ev.UserId)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            // License configuration
            modelBuilder.Entity<License>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.MaxDevices).HasDefaultValue(1);
                entity.Property(e => e.RowVersion).IsConcurrencyToken();

                entity.HasMany(e => e.ActivationCodes)
                      .WithOne(a => a.License)
                      .HasForeignKey(a => a.LicenseId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(e => e.Devices)
                      .WithOne(d => d.License)
                      .HasForeignKey(d => d.LicenseId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(e => e.Events)
                      .WithOne(ev => ev.License)
                      .HasForeignKey(ev => ev.LicenseId)
                      .OnDelete(DeleteBehavior.SetNull);

                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => e.ExpiresAtUtc);
            });

            // ActivationCode configuration
            modelBuilder.Entity<ActivationCode>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.CodeHash).HasMaxLength(128).IsRequired();
                entity.HasIndex(e => e.CodeHash).IsUnique();
                entity.HasIndex(e => e.Status);
            });

            // Device configuration
            modelBuilder.Entity<Device>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.DeviceIdentifierHash).HasMaxLength(128).IsRequired();
                entity.Property(e => e.DeviceName).HasMaxLength(256).IsRequired();

                entity.HasIndex(e => new { e.LicenseId, e.DeviceIdentifierHash });
                entity.HasIndex(e => e.Status);
            });

            // LicenseEvent configuration
            modelBuilder.Entity<LicenseEvent>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.EventDetails).HasMaxLength(4000);
                entity.Property(e => e.IpAddress).HasMaxLength(64);

                entity.HasIndex(e => e.CreatedAtUtc);
                entity.HasIndex(e => e.EventType);
            });

            // AdminUser configuration
            modelBuilder.Entity<AdminUser>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Email).HasMaxLength(256).IsRequired();
                entity.Property(e => e.NormalizedEmail).HasMaxLength(256).IsRequired();
                entity.HasIndex(e => e.NormalizedEmail).IsUnique();
                entity.Property(e => e.PasswordHash).IsRequired();
            });

            // RegistrationRequest configuration
            modelBuilder.Entity<RegistrationRequest>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Email).HasMaxLength(256).IsRequired();
                entity.Property(e => e.NormalizedEmail).HasMaxLength(256).IsRequired();
                entity.Property(e => e.FullName).HasMaxLength(128);
                entity.Property(e => e.PhoneNumber).HasMaxLength(64);
                entity.Property(e => e.MachineId).HasMaxLength(128);
                entity.Property(e => e.DeviceName).HasMaxLength(128);
                entity.Property(e => e.Status).HasMaxLength(32).IsRequired();
                entity.HasIndex(e => e.CreatedAtUtc);
            });
        }
    }
}
