using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace PageCounter
{
    public class UsbPrinter
    {
        public UsbPrinter()
        {
            LastKnowIp = Helpers.GetCurrentIPv4();
            Jobs = new List<Job>();
        }

        public UsbPrinter(string hardwareSerial, string product, string systemName)
        {
            LastKnowIp = Helpers.GetCurrentIPv4();
            Jobs = new List<Job>();
            HardwareSerial = hardwareSerial;
            Product = product;
            SystemName = systemName;
        }

        [Key] public string HardwareSerial { get; set; }

        public string VisualSerial { get; set; }
        public string Product { get; set; }
        public string LastKnowIp { get; set; }
        public List<Job> Jobs { get; set; }

        [NotMapped] public string SystemName { get; set; }
    }

    public class Job
    {
        public Job()
        {
        }

        public Job(DateTimeOffset dateTime, uint pagesCount, UsbPrinter printer)
        {
            DateTime = dateTime;
            PagesCount = pagesCount;
            Printer = printer;
        }

        [Key] public DateTimeOffset DateTime { get; set; }

        public uint PagesCount { get; set; }
        public UsbPrinter Printer { get; set; }
    }

    public sealed class ApplicationContext : DbContext
    {
        private readonly ApplicationSettings config;

        public ApplicationContext(ApplicationSettings config)
        {
            this.config = config;
            Database.EnsureCreated();
        }

        public DbSet<UsbPrinter> Printers { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.EnableSensitiveDataLogging();
            optionsBuilder.UseSqlServer(config.IntegratedSecurity
                ? $"Data Source={config.DataSource};Initial Catalog={config.InitialCatalog};Integrated Security=True"
                : $"Data Source={config.DataSource};Initial Catalog={config.InitialCatalog};Integrated Security=False;User ID={config.Id};Password={config.Password}");
            base.OnConfiguring(optionsBuilder);
        }
    }
}