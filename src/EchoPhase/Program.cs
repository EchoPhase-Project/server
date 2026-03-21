// Copyright (c) 2025-2026 EchoPhase. Licensed under the BSD-3-Clause License.
// See the LICENCE file in the repository root for full licence text.

using EchoPhase.Extensions;

namespace EchoPhase
{
    public class Program
    {
        public static async Task<int> Main(string[] args)
        {
            var hostBuilder = CreateHostBuilder(args);
            var host = hostBuilder.Build();

            var result = await host.CheckArgsAsync(args);

            if (result >= 0)
                return result;

            try
            {
                await host.RunAsync();
                return 0;
            }
            catch (Exception)
            {
                return 1;
            }
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((hostingContext, config) =>
                {
                    config.SetBasePath(Directory.GetCurrentDirectory());
                    config.AddJsonFile("Configurations/appsettings.json", optional: false, reloadOnChange: true);
                    config.AddJsonFile($"Configurations/appsettings.{hostingContext.HostingEnvironment.EnvironmentName}.json", optional: true, reloadOnChange: true);
                    config.AddJsonFile("Configurations/secrets.json", optional: true, reloadOnChange: true);
                    config.AddEnvironmentVariables();
                })
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                })
                .UseSystemd()
                .UseConsoleLifetime(options =>
                {
                    options.SuppressStatusMessages = false;
                })
                .UseDefaultServiceProvider(options =>
                {
                    options.ValidateScopes = true;
                    options.ValidateOnBuild = true;
                });
    }
}
