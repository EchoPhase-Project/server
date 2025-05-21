using EchoPhase.Interfaces;
using EchoPhase.Clients.Models;

namespace EchoPhase.Dtos
{
	public class TwitchApiResponse<T> : ClientResponse<ITwitchApiResponseDto<T>, ITwitchApiError>, ITwitchApiResponse<T>
	{
		public TwitchApiResponse()
		{
		}

		public TwitchApiResponse(IClientResponse<ITwitchApiResponseDto<T>, ITwitchApiError> inner)
		{
			this.Data = inner.Data;
			this.Error = inner.Error;
			this.StatusCode = inner.StatusCode;
		}
	}
}
