using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PageCounter.Windows;

namespace PageCounter
{
    public class Worker : BackgroundService
    {
        private readonly ApplicationSettings config;
        private readonly ILogger<Worker> logger;

        public Worker(ILogger<Worker> logger, ApplicationSettings config)
        {
            this.config = config;
            this.logger = logger;
        }

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation($"Service started at: {DateTimeOffset.Now}");
            return base.StartAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                if (config.IsWindows)
                    while (!stoppingToken.IsCancellationRequested)
                    {
                        Windows();
                        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                    }
                else
                    while (!stoppingToken.IsCancellationRequested)
                    {
                        Linux();
                        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                    }
            }
            catch (TaskCanceledException)
            {
            }
            catch (UnauthorizedAccessException e)
            {
                logger.LogError(e.Message);
            }
            catch (Exception e)
            {
                logger.LogCritical(e.ToString());
                throw;
            }

            void Windows()
            {
                WindowsHelpers.CheckAndEnableLogging(logger);
                List<UsbPrinter> printers = PrinterResolver.GetUsbPrintersData();
                if (printers.Count != 0)
                    foreach (UsbPrinter usbPrinter in printers)
                    {
                        usbPrinter.Jobs = JobsParser.ParseJobsForPrinter(usbPrinter);
                        if (usbPrinter.Jobs.Count != 0) DbSender.SendToDb(printers, config, logger);
                    }
                else
                    logger.LogWarning($"No USB printer found at:{DateTimeOffset.Now}");
            }

            void Linux()
            {
                LinuxHelpers.CheckAndEnableLogging(logger);
                List<UsbPrinter> printers = PageCounter.Linux.PrinterResolver.GetUsbPrintersData();
                if (printers.Count != 0)
                    foreach (UsbPrinter usbPrinter in printers)
                    {
                        usbPrinter.Jobs = PageCounter.Linux.JobsParser.ParseJobsForPrinter(usbPrinter);
                        if (usbPrinter.Jobs.Count != 0) DbSender.SendToDb(printers, config, logger);
                    }
                else
                    logger.LogWarning($"No USB printer found at:{DateTimeOffset.Now}");
            }
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation($"Service stopped at: {DateTimeOffset.Now}");
            return base.StopAsync(cancellationToken);
        }
    }
}