using EchoPhase.Interfaces;

namespace EchoPhase.Dtos
{
	public class TwitchApiError : ITwitchApiError
	{
		public string Error { set; get; } = string.Empty;
		public int Status { set; get; }
		public string Message { set; get; } = string.Empty;
	}
}
