using EchoPhase.Projection.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EchoPhase.Projection.Tests
{
    public class ConfiguredFixture
    {
        public ServiceProvider Provider
        {
            get;
        }
        public IConfiguration Configuration
        {
            get;
        }

        public ConfiguredFixture()
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables();

            Configuration = builder.Build();

            var services = new ServiceCollection();
            services.AddProjection(opts =>
                {
                    opts.IncludeOnlyExpose = false;
                });
            Provider = services.BuildServiceProvider();
        }
    }
}
