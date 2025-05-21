namespace EchoPhase.Interfaces
{
	public interface ITwitchApiResponse<T> : IClientResponse<ITwitchApiResponseDto<T>, ITwitchApiError>
	{
	}
}
