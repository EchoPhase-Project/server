using EchoPhase.Configuration.Database;
using EchoPhase.DAL.Abstractions;
using EchoPhase.DAL.Postgres.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EchoPhase.DAL.Postgres
{
    public class PostgresContext : IdentityDbContext<User, UserRole, Guid>
    {
        public const string DefaultSchema = "EchoPhase";
        public string Schema
        {
            get;
        }

        public PostgresContext(DbContextOptions<PostgresContext> options, IOptions<DatabaseOptions> settings) : base(options)
        {
            Schema = DefaultSchema;
        }

        public override DbSet<User> Users { get; set; } = default!;

        public DbSet<RefreshToken> RefreshTokens { get; set; } = default!;
        public DbSet<WebHook> WebHooks { get; set; } = default!;
        public DbSet<RefreshTokenScope>           RefreshTokenScopes           { get; set; } = default!;
        public DbSet<RefreshTokenIntent>          RefreshTokenIntents          { get; set; } = default!;
        public DbSet<RefreshTokenPermissionEntry> RefreshTokenPermissionEntries { get; set; } = default!;

        protected override void OnModelCreating(ModelBuilder builder)
        {
            builder.HasDefaultSchema(DefaultSchema);

            base.OnModelCreating(builder);

            foreach (var entityType in builder.Model.GetEntityTypes())
            {
                if (typeof(ITrackingEntity).IsAssignableFrom(entityType.ClrType))
                {
                    var clrType = entityType.ClrType;
                    var entityBuilder = builder.Entity(clrType);

                    entityBuilder.Property(nameof(ITrackingEntity.UpdatedAt))
                        .IsRequired()
                        .HasDefaultValueSql("CURRENT_TIMESTAMP");

                    entityBuilder.Property(nameof(ITrackingEntity.CreatedAt))
                        .IsRequired()
                        .HasDefaultValueSql("CURRENT_TIMESTAMP");
                }
            }

            foreach (var entityType in builder.Model.GetEntityTypes())
            {
                if (typeof(IConcurrentEntity).IsAssignableFrom(entityType.ClrType))
                {
                    var clrType = entityType.ClrType;
                    var entityBuilder = builder.Entity(clrType);

                    entityBuilder.Property(nameof(IConcurrentEntity.ConcurrencyStamp))
                        .IsRequired();
                }
            }

            builder.Entity<User>(entity =>
            {
                entity.ToTable("Users");
                entity.HasKey(e => e.Id);
                entity.HasIndex(u => u.UserName)
                    .IsUnique();

                entity.Property(u => u.Name)
                    .HasMaxLength(36)
                    .IsRequired();
                entity.Property(u => u.UserName)
                    .HasMaxLength(36)
                    .IsRequired();
                entity.Property(u => u.ProfileImageName)
                    .HasMaxLength(64);
            });

            builder.Entity<WebHook>(entity =>
            {
                entity.ToTable("WebHooks");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.Url, e.UserId })
                    .IsUnique();

                entity.HasOne(e => e.User)
                    .WithMany(u => u.WebHooks)
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<UserRole>(entity =>
            {
                entity.ToTable("Roles");

                entity.HasIndex(it => it.Name).IsUnique();
                entity.Property(it => it.Name).IsRequired();
            });

            builder.Entity<RefreshToken>(entity =>
            {
                entity.ToTable("RefreshTokens");
                entity.HasKey(e => e.Id);
                entity.HasIndex(t => new { t.RefreshValue, t.UserId }).IsUnique();
                entity.HasOne(t => t.User)
                      .WithMany(u => u.RefreshTokens)
                      .HasForeignKey(t => t.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(t => t.Scopes)
                      .WithOne(s => s.RefreshToken)
                      .HasForeignKey(s => s.RefreshTokenId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(t => t.Intents)
                      .WithOne(i => i.RefreshToken)
                      .HasForeignKey(i => i.RefreshTokenId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(t => t.Permissions)
                      .WithOne(p => p.RefreshToken)
                      .HasForeignKey(p => p.RefreshTokenId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<RefreshTokenScope>(entity =>
            {
                entity.ToTable("RefreshTokenScopes");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.RefreshTokenId, e.Value }).IsUnique();
            });

            builder.Entity<RefreshTokenIntent>(entity =>
            {
                entity.ToTable("RefreshTokenIntents");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.RefreshTokenId, e.Value }).IsUnique();
            });

            builder.Entity<RefreshTokenPermissionEntry>(entity =>
            {
                entity.ToTable("RefreshTokenPermissionEntries");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.RefreshTokenId, e.Resource, e.Permission }).IsUnique();
            });
        }

        public override int SaveChanges()
        {
            ChangeOnSave();

            return base.SaveChanges();
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            ChangeOnSave();

            return await base.SaveChangesAsync(cancellationToken);
        }

        private void ChangeOnSave()
        {
            ChangeTracker.DetectChanges();

            var concurrentEntries = ChangeTracker.Entries<IConcurrentEntity>()
                .Where(e => e.State == EntityState.Modified || e.State == EntityState.Added);

            var trackedEntries = ChangeTracker.Entries<ITrackingEntity>()
                .Where(e => e.State == EntityState.Modified || e.State == EntityState.Added);

            var now = DateTime.UtcNow;

            foreach (var entry in trackedEntries)
            {
                if (entry.State == EntityState.Added)
                    entry.Entity.CreatedAt = now;
                else if (entry.State == EntityState.Modified)
                    entry.Entity.UpdatedAt = now;
            }

            foreach (var entry in concurrentEntries)
            {
                entry.Entity.ConcurrencyStamp = Guid.NewGuid();
            }
        }
    }
}
