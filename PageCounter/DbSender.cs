using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace PageCounter
{
    public static class DbSender
    {
        public static void SendToDb(List<UsbPrinter> printers, ApplicationSettings config, ILogger<Worker> logger)
        {
            try
            {
                using (ApplicationContext db = new ApplicationContext(config))
                {
                    foreach (UsbPrinter usbPrinter in printers)
                    {
                        UsbPrinter p = db.Printers.Include(x => x.Jobs)
                            .SingleOrDefault(x => x.HardwareSerial.Equals(usbPrinter.HardwareSerial));
                        if (p == null)
                        {
                            db.Printers.Add(usbPrinter);
                            db.SaveChanges();
                            logger.LogInformation(
                                $"Printer {usbPrinter.HardwareSerial} added to DB with {usbPrinter.Jobs.Count} job(s) at {DateTimeOffset.Now}");
                        }
                        else
                        {
                            if (usbPrinter.Jobs.Count != 0)
                            {
                                p.Jobs.AddRange(usbPrinter.Jobs);
                                db.SaveChanges();
                                logger.LogInformation($"Sent {usbPrinter.Jobs.Count} job(s) at {DateTimeOffset.Now}");
                            }

                            if (!p.LastKnowIp.Equals(usbPrinter.LastKnowIp))
                            {
                                p.LastKnowIp = usbPrinter.LastKnowIp;
                                db.SaveChanges();
                                logger.LogInformation(
                                    $"IP of {usbPrinter.HardwareSerial} changed to {usbPrinter.LastKnowIp} at {DateTimeOffset.Now}");
                            }
                        }
                    }

                    if (config.IsWindows)
                        WindowsHelpers.ClearLog(logger);
                    else
                        LinuxHelpers.ClearLog(logger);
                }
            }
            catch (SqlException sqlException)
            {
                foreach (SqlError sqlError in sqlException.Errors) logger.LogError(sqlError.ToString());
            }
            catch (InvalidOperationException ioException)
            {
                logger.LogError(ioException.Message);
                if (config.IsWindows)
                    WindowsHelpers.ClearLog(logger);
                else
                    LinuxHelpers.ClearLog(logger);
            }
        }
    }
}