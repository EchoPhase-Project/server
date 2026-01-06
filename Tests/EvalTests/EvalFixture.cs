using Microsoft.Extensions.Configuration;
using EchoPhase.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace EvalTests
{
	public class EvalFixture
	{
		public ServiceProvider Provider { get; }
		public IConfiguration Configuration { get; }

		public EvalFixture()
		{
			var builder = new ConfigurationBuilder()
				.SetBasePath(Directory.GetCurrentDirectory())
				.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
				.AddEnvironmentVariables();

			Configuration = builder.Build();

			var services = new ServiceCollection();
			services.AddProfiler();
			services.AddExpressionEval();
			Provider = services.BuildServiceProvider();
		}
	}
}
