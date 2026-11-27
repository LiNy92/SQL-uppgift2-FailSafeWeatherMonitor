using FailSafeWeatherMonitor.Core;
using Microsoft.EntityFrameworkCore;
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
                //(Azure SQL) Använde dessa två rader nedan för att köra mot molnet.
                //La mitt lösenord i UserSecrets vid uppladdning till molnet. Kommenterar bort för funktion vid rättning.
                //.AddJsonFile("appsettings.Production.json", optional: true)
                //.AddUserSecrets("2ab4fe72-44df-4952-9719-b14901e6a2a8")

                .Build();

            var connectionString = config.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException(
                    "Anslutningssträngen 'DefaultConnection' saknas i appsettings.json.");

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