namespace EchoPhase.Exceptions
{
	public class WebHookNotFoundException : Exception
	{
		public WebHookNotFoundException(Guid id)
			: base($"WebHook with ID {id} was not found.")
		{
		}
	}
}
