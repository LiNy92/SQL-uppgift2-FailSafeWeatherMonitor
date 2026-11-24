using FailSafeWeatherMonitor.Core;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace FailSafeWeatherMonitor.Data;



public class FailSafeWeatherMonitorDbContext : DbContext
{
    public DbSet<WeatherDataPoint> WeatherDataPoints { get; set; }




    //Konstruktorer
    public FailSafeWeatherMonitorDbContext()
    {

    }

    public FailSafeWeatherMonitorDbContext(DbContextOptions<FailSafeWeatherMonitorDbContext> options)
          : base(options)
    {

    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json")
                .Build();

            var connectionString = config.GetConnectionString("DefaultConnection");
            optionsBuilder.UseSqlServer(connectionString);

        }
    }


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WeatherDataPoint>()
            .HasIndex(w => new { w.StationName, w.Timestamp })
            .IsUnique();

    }
}