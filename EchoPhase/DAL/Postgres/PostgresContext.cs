using EchoPhase.Interfaces;
using EchoPhase.Models;
using EchoPhase.Roles;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace EchoPhase.DAL.Postgres
{
    public class PostgresContext : IdentityDbContext<User, UserRole, Guid>
    {
        public static readonly string DefaultSchema = "public";
        public string Schema
        {
            get;
        }

        public PostgresContext(IConfiguration configuration, DbContextOptions<PostgresContext> options) : base(options)
        {
            Schema = DefaultSchema;
        }

        public override DbSet<User> Users { get; set; } = default!;

        public DbSet<JwtToken> JwtTokens { get; set; } = default!;
        public DbSet<WebHook> WebHooks { get; set; } = default!;

        public DbSet<DiscordToken> DiscordTokens { get; set; } = default!;

        protected override void OnModelCreating(ModelBuilder builder)
        {
            builder.HasDefaultSchema(DefaultSchema);

            base.OnModelCreating(builder);

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

                entity.Property(e => e.UpdatedAt)
                    .IsRequired();
                entity.Property(e => e.CreatedAt)
                    .IsRequired();
            });

            builder.Entity<WebHook>(entity =>
            {
                entity.ToTable("WebHooks");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Url)
                    .IsUnique();

                entity.HasOne(e => e.User)
                    .WithMany(u => u.WebHooks)
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.Property(e => e.UpdatedAt)
                    .IsRequired();
                entity.Property(e => e.CreatedAt)
                    .IsRequired();
            });

            builder.Entity<UserRole>(entity =>
            {
                entity.ToTable("Roles");
                entity.HasIndex(it => it.Name)
                    .IsUnique();
                entity.Property(it => it.Name)
                    .IsRequired();
            });

            builder.Entity<JwtToken>(entity =>
            {
                entity.ToTable("JwtTokens");
                entity.HasKey(e => e.Id);
                entity.HasIndex(it => it.Token)
                    .IsUnique();

                entity.HasOne(t => t.User)
                    .WithMany(u => u.JwtTokens)
                    .HasForeignKey(t => t.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.Property(e => e.UpdatedAt)
                    .IsRequired();
                entity.Property(e => e.CreatedAt)
                    .IsRequired();
            });

            builder.Entity<DiscordToken>(entity =>
            {
                entity.ToTable("DiscordTokens");
                entity.HasKey(e => e.Id);
                entity.HasIndex(it => it.Token)
                    .IsUnique();

                entity.HasOne(t => t.User)
                    .WithMany(u => u.DiscordTokens)
                    .HasForeignKey(t => t.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.Property(e => e.UpdatedAt)
                    .IsRequired();
                entity.Property(e => e.CreatedAt)
                    .IsRequired();
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
                else if (entry.State == EntityState.Modified || entry.State == EntityState.Added)
                    entry.Entity.UpdatedAt = now;
            }

            foreach (var entry in concurrentEntries)
            {
                entry.Entity.ConcurrencyStamp = Guid.NewGuid();
            }
        }
    }
}
