using Microsoft.EntityFrameworkCore;

using EchoPhase.DAL.Postgres;
using EchoPhase.Models;
using EchoPhase.Interfaces;

namespace EchoPhase.Repositories
{
    public class UserRepository
	{
		private readonly PostgresContext _dbContext;

		public UserRepository(PostgresContext dbContext)
		{
			_dbContext = dbContext;
		}

		public async Task<List<User>> GetUsersAsync()
		{
			IQueryable<User> usersQuery = _dbContext.Users;

			return await usersQuery.ToListAsync();
		}

		public async Task<List<User>> GetUsersAsync(Guid userId)
		{
			IQueryable<User> usersQuery = _dbContext.Users;

			return await usersQuery
						.Where(u => u.Id != userId)
						.ToListAsync();
		}
		
		public async Task<User?> FindByIdAsync(Guid userId)
		{
			IQueryable<User> userQuery = _dbContext.Users;

			return await userQuery.FirstOrDefaultAsync(u => u.Id == userId);
		}

		public async Task<User?> FindByUserNameAsync(string UserName)
		{
			IQueryable<User> userQuery = _dbContext.Users;

			return await userQuery.FirstOrDefaultAsync(u => u.UserName == UserName);
		}
	}
}
