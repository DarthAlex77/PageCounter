using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace PageCounter.Linux
{
    public static class JobsParser
    {
        public static List<Job> ParseJobsForPrinter(UsbPrinter printer)
        {
            List<Job> jobs = new List<Job>();
            using (StreamReader reader = File.OpenText(@"/var/log/cups/page_log"))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    string[] items = line.Split(' ', 4);
                    if (items[0].Equals(printer.SystemName))
                    {
                        Job job = new Job
                        {
                            PagesCount = (uint) int.Parse(items[2]),
                            DateTime = DateTimeOffset.ParseExact(items[3], "[dd/MMM/yyyy:HH:mm:ss K]",
                                CultureInfo.InvariantCulture),
                            Printer = printer
                        };
                        jobs.Add(job);
                    }
                }
            }

            return jobs;
        }
    }
}