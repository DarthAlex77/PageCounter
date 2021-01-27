using System;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.EventLog;

namespace PageCounter
{
    public static class Program
    {
        public static void Main()
        {
            CreateHostBuilder().Build().Run();
        }

        private static IHostBuilder CreateHostBuilder()
        {
            bool isWindows;
            IHostBuilder hostBuilder = Host.CreateDefaultBuilder();
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                isWindows = true;
                hostBuilder.UseWindowsService();
                hostBuilder.ConfigureLogging(logger =>
                {
                    logger.ClearProviders();
                    logger.AddConsole();
                    logger.AddEventLog(new EventLogSettings
                    {
                        SourceName = "Page Counter"
                    });
                    logger.AddFilter<EventLogLoggerProvider>("PageCounter", LogLevel.Information);
                    logger.AddFilter<EventLogLoggerProvider>("Microsoft", LogLevel.Warning);
                });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                isWindows = false;
                hostBuilder.UseSystemd();
            }
            else
            {
                throw new PlatformNotSupportedException();
            }

            hostBuilder.ConfigureAppConfiguration((context, builder) =>
            {
                builder.SetBasePath(AppDomain.CurrentDomain.BaseDirectory).AddJsonFile("appsettings.json", true, true);
                builder.AddEnvironmentVariables();
            });

            return hostBuilder.ConfigureServices((hostContext, services) =>
            {
                services.AddOptions();
                ApplicationSettings config = hostContext.Configuration.GetSection("DbSettings").Get<ApplicationSettings>();
                config.IsWindows = isWindows;
                services.AddSingleton(config);
                services.AddHostedService<Worker>();
            });
        }
    }
}