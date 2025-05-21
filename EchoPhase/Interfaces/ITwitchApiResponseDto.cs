namespace EchoPhase.Interfaces
{
	public interface ITwitchApiResponseDto<TResponse>
	{
		public TResponse? Data { get; set; }
		public ITwitchApiPagination? Pagination { get; set; }
	}
}

