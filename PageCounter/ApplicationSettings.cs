using System;
using System.IO;
using Newtonsoft.Json;

namespace PageCounter
{
    public class ApplicationSettings
    {
        private string dataSource;
        private string id;
        private string initialCatalog;
        private string password;

        public string DataSource
        {
            get => Get(ref dataSource, nameof(DataSource));
            set => Set(ref dataSource, nameof(DataSource), value);
        }

        public string InitialCatalog
        {
            get => Get(ref initialCatalog, nameof(InitialCatalog));
            set => Set(ref initialCatalog, nameof(InitialCatalog), value);
        }

        public string Id
        {
            get => Get(ref id, nameof(Id));
            set => Set(ref id, nameof(Id), value);
        }

        public string Password
        {
            get => Get(ref password, nameof(Password));
            set => Set(ref password, nameof(Password), value);
        }

        public bool IntegratedSecurity { get; set; }
        public bool IsWindows { get; set; }

        private static string Get(ref string field, string propertyName)
        {
            if (!string.IsNullOrWhiteSpace(field))
            {
                if (!EncryptionHelper.IsEncrypted(field))
                {
                    field = EncryptionHelper.Encrypt(field, "PASSWORD");
                    AddOrUpdateAppSetting($"DbSettings:{propertyName}", field);
                }

                field = EncryptionHelper.Decrypt(field, "PASSWORD");
            }

            return field;
        }

        private static void Set(ref string field, string propertyName, string value)
        {
            field = !EncryptionHelper.IsEncrypted(value) ? EncryptionHelper.Encrypt(value, "PASSWORD") : value;
            AddOrUpdateAppSetting($"DbSettings:{propertyName}", field);
        }

        private static void AddOrUpdateAppSetting<T>(string sectionPathKey, T value)
        {
            try
            {
                string filePath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
                string json = File.ReadAllText(filePath);
                dynamic jsonObj = JsonConvert.DeserializeObject(json);

                SetValueRecursively(sectionPathKey, jsonObj, value);

                string output = JsonConvert.SerializeObject(jsonObj, Formatting.Indented);
                File.WriteAllText(filePath, output);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error writing app settings | {0}", ex.Message);
            }
        }

        private static void SetValueRecursively<T>(string sectionPathKey, dynamic jsonObj, T value)
        {
            string[] remainingSections = sectionPathKey.Split(":", 2);

            string currentSection = remainingSections[0];
            if (remainingSections.Length > 1)
            {
                string nextSection = remainingSections[1];
                SetValueRecursively(nextSection, jsonObj[currentSection], value);
            }
            else
            {
                jsonObj[currentSection] = value;
            }
        }
    }
}