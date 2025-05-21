using EchoPhase.Models;

namespace EchoPhase.Funcs
{
	public static class FuncExtensions
	{
		public static Task<T> ToTask<T>(
				this Func<T> func) =>
			Task.Run(func);

		public static Func<Task<T>> ToAsync<T>(
				this Func<T> func) =>
			() => ToTask(func);
	}
}
