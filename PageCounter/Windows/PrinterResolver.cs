#pragma warning disable CA1416 // Validate platform compatibility
using System;
using System.Collections.Generic;
using System.Drawing.Printing;
using System.Linq;
using System.Security.AccessControl;
using Microsoft.Win32;
using Vurdalakov.UsbDevicesDotNet;

namespace PageCounter.Windows
{
    public static class PrinterResolver
    {
        public static List<UsbPrinter> GetUsbPrintersData()
        {
            List<UsbPrinter> usbPrinters = new List<UsbPrinter>();
            PrinterSettings.StringCollection installedPrinters = PrinterSettings.InstalledPrinters;
            const string keyTemplate = @"SYSTEM\CurrentControlSet\Control\Print\Printers\{0}\PNPData";
            foreach (string printer in installedPrinters)
                using (RegistryKey hk = Registry.LocalMachine.OpenSubKey(string.Format(keyTemplate, printer),
                    RegistryKeyPermissionCheck.Default, RegistryRights.QueryValues))
                {
                    if (hk != null)
                    {
                        Guid guid = new Guid(hk.GetValue("DeviceContainerId") as byte[] ?? Guid.Empty.ToByteArray());
                        if (!guid.Equals(Guid.Empty))
                        {
                            string[] result = GetDeviceData(guid);
                            if (result.Length != 0) usbPrinters.Add(new UsbPrinter(result[0], result[1], printer));
                        }
                    }
                }

            return usbPrinters;
        }

        private static string[] GetDeviceData(Guid printerGuid)
        {
            UsbDevice selectedDevice = null;
            UsbDevice[] usbDevices = UsbDevice.GetDevices(new Guid("A5DCBF10-6530-11D2-901F-00C04FB951ED"));

            if (usbDevices.Length != 0)
            {
                foreach (UsbDevice device in usbDevices)
                {
                    Guid deviceGuid = new Guid(device.RegistryProperties[22].Value as string ?? Guid.Empty.ToString());
                    if (deviceGuid.Equals(printerGuid))
                    {
                        selectedDevice = device;
                        break;
                    }
                }

                if (!string.IsNullOrWhiteSpace(selectedDevice?.DeviceId.Split('\\').Last()))
                {
                    string[] result = new string[2];
                    result[0] = selectedDevice.DeviceId.Split('\\').Last();
                    result[1] = selectedDevice.BusReportedDeviceDescription;
                    return result;
                }
            }

            return new string[] { };
        }
    }
}