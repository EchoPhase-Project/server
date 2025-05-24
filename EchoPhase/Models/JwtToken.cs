using System.ComponentModel.DataAnnotations;

using EchoPhase.Models;
using EchoPhase.Interfaces;

public class JwtToken : ITrackingEntity, IConcurrentEntity
{
    public int Id { get; set; }

	public required Guid UserId { get; set; }
	public User? User { get; set; } = default;

	public required string Token { get; set; }

    public required DateTime ExpiryDate { get; set; }

	public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

	[ConcurrencyCheck]
	public Guid ConcurrencyStamp { get; set; } = Guid.NewGuid();
}
