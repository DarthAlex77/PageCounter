#pragma warning disable CA1416 // Validate platform compatibility
using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Linq;

namespace PageCounter.Windows
{
    public static class JobsParser
    {
        public static List<Job> ParseJobsForPrinter(UsbPrinter printer)
        {
            EventRecord entry;
            List<EventItems> items = new List<EventItems>();
            EventItems eventItems = null;
            EventLogReader reader = new EventLogReader(new EventLogQuery("Microsoft-Windows-PrintService/Operational", PathType.LogName));
            uint copies = 0;
            uint jobId = 0;

            while ((entry = reader.ReadEvent()) != null)
            {
                switch (entry.Id)
                {
                    case 805:
                    {
                        eventItems = new EventItems();
                        jobId = Convert.ToUInt32(entry.Properties[0].Value);
                        copies = Convert.ToUInt32(entry.Properties[7].Value);
                        break;
                    }
                    case 307:
                    {
                        if (jobId == Convert.ToUInt32(entry.Properties[0].Value))
                        {
                            if (eventItems != null)
                            {
                                if (entry.TimeCreated != null) eventItems.Time = (DateTimeOffset) entry.TimeCreated;
                                eventItems.TotalPage = Convert.ToUInt32(entry.Properties[7].Value) * copies;
                            }
                        }

                        if (entry.Properties[4].Value.Equals(printer.SystemName))
                        {
                            items.Add(eventItems);
                        }

                        break;
                    }
                }
            }

            return items.Select(item => new Job(item.Time, item.TotalPage, printer)).ToList();
        }

        public class EventItems
        {
            public DateTimeOffset Time { get; set; }
            public uint TotalPage { get; set; }
        }
    }
}