using FailSafeWeatherMonitor.Core;
using System.Globalization;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;

namespace FailSafeWeatherMonitor.Data;

public class WeatherApiImporter(FailSafeWeatherMonitorDbContext _db)
{
    private static readonly HttpClient _http = new();

    public async Task<ImportResult> ImportGothenburgWeatherAsync(string stationName = "Göteborg")
    {
        var errors = new List<string>();

        string url = "https://api.open-meteo.com/v1/forecast?latitude=57.7089&longitude=11.9746&current=temperature_2m,relative_humidity_2m&timezone=Europe%2FStockholm";

        WeatherApiDto? response;
        try
        {
            response = await _http.GetFromJsonAsync<WeatherApiDto>(url);
        }
        catch (Exception ex)
        {
            errors.Add($"Kunde inte hämta data från API: {ex.Message}");
            return new ImportResult(0, 0, errors);
        }

        if (response?.CurrentWeather is null)
        {
            errors.Add("API-svaret saknade aktuell väderdata.");
            return new ImportResult(0, 1, errors);
        }

        var current = response.CurrentWeather;

        if (!DateTime.TryParse(current.Time, CultureInfo.InvariantCulture, DateTimeStyles.None, out var timestamp))
        {
            errors.Add($"Kunde inte tolka tidsstämpel: '{current.Time}'");
            return new ImportResult(0, 1, errors);
        }

        if (current.Temperature is not double temperature || current.Humidity is not double humidity)
        {
            errors.Add("API:t saknade temperatur eller luftfuktighet.");
            return new ImportResult(0, 1, errors);
        }

        if (!double.IsFinite(temperature) || temperature < -65 || temperature > 45)
        {
            errors.Add($"API:t returnerade en orimlig temperatur: {temperature}.");
            return new ImportResult(0, 1, errors);
        }

        if (!double.IsFinite(humidity) || humidity < 0 || humidity > 100)
        {
            errors.Add($"API:t returnerade en orimlig luftfuktighet: {humidity}.");
            return new ImportResult(0, 1, errors);
        }

        var point = new WeatherDataPoint
        {
            StationName = stationName,
            Timestamp = timestamp,
            Temperature = temperature,
            Humidity = humidity,
            DataSource = "API"
        };

        try
        {
            var exists = await _db.WeatherDataPoints
                .AnyAsync(w => w.StationName == point.StationName && w.Timestamp == point.Timestamp);

            if (exists)
            {
                errors.Add($"Dubblett för '{point.StationName}' ({point.Timestamp})");
                return new ImportResult(0, 1, errors);
            }

            _db.WeatherDataPoints.Add(point);
            await _db.SaveChangesAsync();
            return new ImportResult(1, 0, errors);
        }
        catch (Exception ex)
        {
            errors.Add($"Kunde inte kontrollera eller spara mätningen i databasen: {ex.GetBaseException().Message}");
            return new ImportResult(0, 0, errors);
        }
    }

}

public record WeatherApiDto(
[property: JsonPropertyName("current")] CurrentDto CurrentWeather);

public record CurrentDto(
    [property: JsonPropertyName("time")] string Time,
    [property: JsonPropertyName("temperature_2m")] double? Temperature,
    [property: JsonPropertyName("relative_humidity_2m")] double? Humidity);