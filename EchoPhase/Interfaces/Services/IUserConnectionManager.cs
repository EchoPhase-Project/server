using EchoPhase.Models;

namespace EchoPhase.Interfaces
{
	public interface IUserConnectionManager
	{
		void KeepUserConnection(Guid userId, string connectionId);
		void RemoveUserConnection(Guid userId, string connectionId);
		string? GetConnectionId(Guid userId);
		bool IsOnline(Guid userId);
		IEnumerable<Guid> GetAllOnlineUsers();
	}
}
