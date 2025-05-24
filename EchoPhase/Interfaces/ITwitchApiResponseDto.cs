namespace EchoPhase.Interfaces
{
	public interface ITwitchApiResponseDto<out T>
	{
		public T? Data { get; }
		public ITwitchApiPagination? Pagination { get; }
	}
}

