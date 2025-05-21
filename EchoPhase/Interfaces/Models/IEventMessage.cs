using EchoPhase.Processors.Enums;

namespace EchoPhase.Interfaces
{
	public interface IEventMessage
	{
		OpCodes Op { get; set; }
		object D { get; set; }
	}
}
