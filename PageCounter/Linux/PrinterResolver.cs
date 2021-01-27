using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace PageCounter.Linux
{
    public static class PrinterResolver
    { 
        public static List<UsbPrinter> GetUsbPrintersData()
        {
            List<UsbPrinter> usbPrinters = (List<UsbPrinter>) GetPrinterData();
            foreach (UsbPrinter usbPrinter in usbPrinters)
            {
                foreach (string[] systemPrinter in GetSystemPrinters())
                {
                    if (systemPrinter[1].Equals(usbPrinter.HardwareSerial))
                    {
                        usbPrinter.SystemName = systemPrinter[0];
                    }
                }
            }

            return usbPrinters;
        }

        private static IEnumerable<UsbPrinter> GetPrinterData()
        {
            Process shell = LinuxHelpers.Bash(@"usb-devices|awk -vRS= '/Cls=07/{print $0,0.77342}'");

            ICollection<UsbPrinter> usbPrinters = new List<UsbPrinter>();
            shell.Start();
            string line;
            UsbPrinter printer = null;
            while ((line = shell.StandardOutput.ReadLine()) != null)
                if (line.Contains("Bus"))
                {
                    printer = new UsbPrinter();
                }
                else if (line.Contains("SerialNumber"))
                {
                    if (printer != null) printer.HardwareSerial = line.Split('=').Last();
                }

                else if (line.Contains("Product"))
                {
                    if (printer != null) printer.Product = line.Split('=').Last();
                }
                else if (line.Contains("0.77342")) //0.77342 is magic number,end of item.
                {
                    usbPrinters.Add(printer);
                }

            shell.WaitForExit();
            shell.Dispose();

            return usbPrinters;
        }

        private static IEnumerable<string[]> GetSystemPrinters()
        {
            string line;
            List<string[]> systemPrinters = new List<string[]>();
            Process process = LinuxHelpers.Bash(@"lpstat -v");
            process.Start();
            while ((line = process.StandardOutput.ReadLine()) != null)
            {
                string[] results = line.Split(':', 2, StringSplitOptions.TrimEntries);
                results[0] = results[0].Substring(results[0].LastIndexOf(' ')).Trim();
                results[1] = results[1].Substring(results[1].LastIndexOf('=') + 1).Trim();
                systemPrinters.Add(results);
            }

            process.WaitForExit();
            process.Dispose();
            return systemPrinters;
        }
    }
}