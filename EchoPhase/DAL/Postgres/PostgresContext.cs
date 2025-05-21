using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

using EchoPhase.Roles;
using EchoPhase.Models;
using EchoPhase.Interfaces;

namespace EchoPhase.DAL.Postgres
{
    public class PostgresContext : IdentityDbContext<User, IdentityRole<Guid>, Guid>
    {
		public static readonly string DefaultSchema = "EchoPhase";
		public string Schema { get; }

        public PostgresContext(IConfiguration configuration, DbContextOptions<PostgresContext> options) : base(options)
        {
			Schema = DefaultSchema;
        }

        public override DbSet<User> Users { get; set; } = default!;
        
		public DbSet<JwtToken> JwtTokens { get; set; } = default!;
		public DbSet<WebHook> WebHooks { get; set; } = default!;

        protected override void OnModelCreating(ModelBuilder builder)
        {
			base.OnModelCreating(builder);

            builder.Entity<User>(entity =>
			{
				entity.ToTable("User");
                entity.HasKey(e => e.Id);
				entity.HasIndex(u => u.UserName)
					.IsUnique();

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
				entity.HasData(
					new UserRole { Id = new Guid("234e8d44-83c2-48fa-9202-6da66a68d4f1"), ConcurrencyStamp = "1", RoleID = 1, Name = "Dev", NormalizedName = "DEV" },
					new UserRole { Id = new Guid("b5df24f9-c8fd-418e-881d-6647664557a4"), ConcurrencyStamp = "2", RoleID = 2, Name = "Admin", NormalizedName = "ADMIN" },
					new UserRole { Id = new Guid("023196cd-d3bc-41c5-8918-00567a39e8ab"), ConcurrencyStamp = "3", RoleID = 3, Name = "Staff", NormalizedName = "STAFF" },
					new UserRole { Id = new Guid("8693a407-4681-4b21-b6de-22cfeed15f9a"), ConcurrencyStamp = "4", RoleID = 4, Name = "APIDev", NormalizedName = "APIDEV" },
					new UserRole { Id = new Guid("f67c713c-6e67-4ff7-9597-db1155236213"), ConcurrencyStamp = "5", RoleID = 5, Name = "User", NormalizedName = "USER" }
				);
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
        }

		public override int SaveChanges()
		{
			ChangeTracker.DetectChanges();

			var now = DateTime.UtcNow;

			foreach (var entry in ChangeTracker.Entries<ITrackingEntity>())
				if (entry.State == EntityState.Modified)
					entry.Entity.UpdatedAt = now;

			return base.SaveChanges();
		}

		public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default(CancellationToken))
		{
			ChangeTracker.DetectChanges();

			var now = DateTime.UtcNow;

			foreach (var entry in ChangeTracker.Entries<ITrackingEntity>())
				if (entry.State == EntityState.Modified)
					entry.Entity.UpdatedAt = now;

			return await base.SaveChangesAsync(cancellationToken);
		}
    }
}
