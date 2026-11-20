using FailSafeWeatherMonitor.Core;
using Microsoft.EntityFrameworkCore;

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
            optionsBuilder.UseSqlServer(
                @"Data Source=(localdb)\MSSQLLocalDB;Initial Catalog=FailSafeWeatherDataPoint;Integrated Security=True;Connect Timeout=30;Encrypt=True;Trust Server Certificate=False;Application Intent=ReadWrite;Multi Subnet Failover=False;Command Timeout=30");
        }
    }
}
