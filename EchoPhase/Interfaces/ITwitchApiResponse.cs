namespace EchoPhase.Interfaces
{
	public interface ITwitchApiResponse<out T> : IClientResponse<ITwitchApiResponseDto<T>, ITwitchApiError>
	{
	}
}
