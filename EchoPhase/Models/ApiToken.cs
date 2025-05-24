using System.ComponentModel.DataAnnotations;

using EchoPhase.Interfaces;

namespace EchoPhase.Models
{
	public class ApiToken : IConcurrentEntity
	{
		[ConcurrencyCheck]
		public Guid ConcurrencyStamp { get; set; } = Guid.NewGuid();
	}
}
