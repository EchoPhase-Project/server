using EchoPhase.Processors.Enums;
using EchoPhase.Interfaces;

namespace EchoPhase.Models
{
	public class EventMessage : IEventMessage
	{
		public OpCodes Op { get; set; } = OpCodes.Unknown;
		public object D { get; set; } = new {};
	}
}
