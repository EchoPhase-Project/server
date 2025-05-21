using System.Net;
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace EchoPhase
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
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
					/*webBuilder.ConfigureKestrel((context, options) =>
					{
						var env = context.HostingEnvironment;
						if (env.IsDevelopment())
						{
							options.Listen(IPAddress.Loopback, 5000, listenOptions =>
							{
								listenOptions.Protocols = HttpProtocols.Http1AndHttp2;
							});

							options.Listen(IPAddress.Loopback, 5001, listenOptions =>
							{
								listenOptions.UseHttps();
								listenOptions.Protocols = HttpProtocols.Http1AndHttp2AndHttp3;
							});
						}
						else
						{
							options.Listen(IPAddress.Any, 80, listenOptions =>
							{
								listenOptions.Protocols = HttpProtocols.Http1AndHttp2;
							});

							options.Listen(IPAddress.Any, 443, listenOptions =>
							{
								listenOptions.UseHttps("cert.pfx", "your_cert_password");
								listenOptions.Protocols = HttpProtocols.Http1AndHttp2AndHttp3;
							});
						}
					});*/
                    webBuilder.UseStartup<Startup>();
                });
    }
}
