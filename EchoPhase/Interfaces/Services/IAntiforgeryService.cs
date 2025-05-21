namespace EchoPhase.Interfaces
{
	public interface IAntiforgeryService
	{
		bool SetAntiforgeryToken();
		string? GetAntiforgeryToken();
		Task<bool> ValidateAntiforgeryTokenAsync();
	}
}
