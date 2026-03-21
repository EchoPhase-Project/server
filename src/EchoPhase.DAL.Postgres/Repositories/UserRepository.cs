using EchoPhase.DAL.Postgres.Models;
using EchoPhase.DAL.Postgres.Repositories.Queries;
using EchoPhase.Types.Repository;

// ── UserRepository ────────────────────────────────────────────────────────────

namespace EchoPhase.DAL.Postgres.Repositories
{
    public class UserRepository : RepositoryBase<User>
    {
        private readonly PostgresContext _context;

        public UserRepository(PostgresContext context)
        {
            _context = context;
        }

        public new UserQuery Query() => new(Build());

        public override IQueryable<User> Build()
            => _context.Users;

        public override void Add(User entity) => _context.Users.Add(entity);
        public override void Update(User entity) => _context.Users.Update(entity);
        public override void Remove(User entity) => _context.Users.Remove(entity);
        public override Task<int> SaveAsync(CancellationToken ct = default)
            => _context.SaveChangesAsync(ct);
    }
}
