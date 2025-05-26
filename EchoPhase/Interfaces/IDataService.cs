
namespace EchoPhase.Interfaces
{
	public interface IDataService<TC, TO>
	{
		public TC WithOptions(TO options);
		public TC WithOptions(Action<TO> configure);
	}
}
