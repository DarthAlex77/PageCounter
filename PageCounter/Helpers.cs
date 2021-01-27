using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace PageCounter
{
    public static class Helpers
    {
        public static string GetCurrentIPv4()
        {
            NetworkInterface[] nics = NetworkInterface.GetAllNetworkInterfaces();
            foreach (NetworkInterface adapter in nics)
            {
                if (adapter.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 || adapter.NetworkInterfaceType == NetworkInterfaceType.Ethernet)
                {
                    foreach (UnicastIPAddressInformation ipAddressInformation in adapter.GetIPProperties()
                        .UnicastAddresses)
                    {
                        if (ipAddressInformation.Address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            foreach (GatewayIPAddressInformation address in adapter.GetIPProperties().GatewayAddresses)
                            {
                                string gateway = address.Address.ToString();
                                if (!string.IsNullOrWhiteSpace(gateway) && !gateway.Contains("::"))
                                {
                                    return ipAddressInformation.Address.ToString();
                                }
                            }
                        }
                    }
                }
            }
            return null;
        }
    }

    public static class WindowsHelpers
    {
        public static void CheckAndEnableLogging(ILogger<Worker> logger)
        {
            ProcessStartInfo processInfo = new ProcessStartInfo
            {
                FileName = @"powershell.exe",
                Arguments = @"Get-WinEvent -ListLog Microsoft-Windows-PrintService/Operational -OutVariable PrinterLog | Select-Object -Property IsEnabled",
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            Process process = new Process {StartInfo = processInfo};
            process.Start();
            string line;
            while ((line = process.StandardOutput.ReadLine()) != null)
            {
                if (bool.TryParse(line, out bool isEnabled) && !isEnabled)
                {
                    ProcessStartInfo psi = new ProcessStartInfo
                    {
                        FileName = @"wevtutil.exe",
                        Arguments = @"sl Microsoft-Windows-PrintService/Operational /enabled:true",
                        RedirectStandardError = false,
                        RedirectStandardOutput = false,
                        UseShellExecute = true,
                        CreateNoWindow = true,
                        Verb = "runas"
                    };
                    new Process {StartInfo = psi}.Start();
                    logger.LogInformation("EventLog Enabled");
                }
            }
        }

        public static void ClearLog(ILogger<Worker> logger)
        {
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = @"wevtutil.exe",
                Arguments = @"cl Microsoft-Windows-PrintService/Operational",
                Verb = "runas"
            };
            new Process {StartInfo = psi}.Start();
            logger.LogInformation($"Event log cleared at:{DateTimeOffset.Now}");
        }
    }

    public static class LinuxHelpers
    {
        public static void ClearLog(ILogger<Worker> logger)
        {
            Bash(@"> /var/log/cups/page_log").Start();
            logger.LogInformation($"Page log cleared at: {DateTimeOffset.Now}");
        }

        public static void CheckAndEnableLogging(ILogger<Worker> logger)
        {
            if (File.ReadAllLines(@"/etc/cups/cupsd.conf").Any(line => line.Contains(@"PageLogFormat %p %j %{job-impressions-completed} %T"))) return;
            Bash(@"sed -i -e '/PageLogFormat/d' /etc/cups/cupsd.conf").Start();
            Bash(@"grep -qxF 'PageLogFormat %p %j %{job-impressions-completed} %T' /etc/cups/cupsd.conf ||echo PageLogFormat %p %j %{job-impressions-completed} %T >> /etc/cups/cupsd.conf").Start();
            Bash(@"systemctl daemon-reload && systemctl restart cups").Start();
            logger.LogInformation("Page Log enabled");
        }

        public static Process Bash(string cmd)
        {
            Process process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = $"-c \"{cmd}\"",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            return process;
        }
    }

    public static class EncryptionHelper
    {
        public static string Encrypt(string plainText, string password)
        {
            plainText ??= string.Empty;
            password ??= string.Empty;
            byte[] bytesToBeEncrypted = Encoding.UTF8.GetBytes(plainText);
            byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
            passwordBytes = SHA256.Create().ComputeHash(passwordBytes);
            byte[] bytesEncrypted = Encrypt(bytesToBeEncrypted, passwordBytes);
            return string.Concat("[ENC] ", Convert.ToBase64String(bytesEncrypted));
        }

        public static string Decrypt(string encryptedText, string password)
        {
            encryptedText ??= string.Empty;
            password ??= string.Empty;
            encryptedText = encryptedText.Split(' ').Last();
            byte[] bytesToBeDecrypted = Convert.FromBase64String(encryptedText);
            byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
            passwordBytes = SHA256.Create().ComputeHash(passwordBytes);
            byte[] bytesDecrypted = Decrypt(bytesToBeDecrypted, passwordBytes);
            return Encoding.UTF8.GetString(bytesDecrypted);
        }

        private static byte[] Encrypt(byte[] bytesToBeEncrypted, byte[] passwordBytes)
        {
            byte[] encryptedBytes;
            byte[] saltBytes = {4, 8, 15, 16, 23, 42, 0, 0};
            using (MemoryStream ms = new MemoryStream())
            {
                using (RijndaelManaged aes = new RijndaelManaged())
                {
                    Rfc2898DeriveBytes key = new Rfc2898DeriveBytes(passwordBytes, saltBytes, 1000);

                    aes.KeySize = 256;
                    aes.BlockSize = 128;
                    aes.Key = key.GetBytes(aes.KeySize / 8);
                    aes.IV = key.GetBytes(aes.BlockSize / 8);

                    aes.Mode = CipherMode.CBC;

                    using (CryptoStream cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
                    {
                        cs.Write(bytesToBeEncrypted, 0, bytesToBeEncrypted.Length);
                        cs.Close();
                    }

                    encryptedBytes = ms.ToArray();
                }
            }

            return encryptedBytes;
        }

        private static byte[] Decrypt(byte[] bytesToBeDecrypted, byte[] passwordBytes)
        {
            byte[] decryptedBytes;
            byte[] saltBytes = {4, 8, 15, 16, 23, 42, 0, 0};

            using (MemoryStream ms = new MemoryStream())
            {
                using (RijndaelManaged aes = new RijndaelManaged())
                {
                    Rfc2898DeriveBytes key = new Rfc2898DeriveBytes(passwordBytes, saltBytes, 1000);

                    aes.KeySize = 256;
                    aes.BlockSize = 128;
                    aes.Key = key.GetBytes(aes.KeySize / 8);
                    aes.IV = key.GetBytes(aes.BlockSize / 8);
                    aes.Mode = CipherMode.CBC;

                    using (CryptoStream cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Write))
                    {
                        cs.Write(bytesToBeDecrypted, 0, bytesToBeDecrypted.Length);
                        cs.Close();
                    }

                    decryptedBytes = ms.ToArray();
                }
            }

            return decryptedBytes;
        }

        public static bool IsEncrypted(string text)
        {
            return text.Contains("[ENC]");
        }
    }
}