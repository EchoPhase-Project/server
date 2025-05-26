using EchoPhase.Interfaces;

namespace EchoPhase.Services
{
	public class IntentsService : BitMaskServiceBase, IIntentsService
	{
		public IntentsService() 
			: base("Logins") {}
	}
}
