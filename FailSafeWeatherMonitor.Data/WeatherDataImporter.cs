using FailSafeWeatherMonitor.Core;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace FailSafeWeatherMonitor.Data;

public class WeatherDataImporter(FailSafeWeatherMonitorDbContext _db)
{
    public void ImportFromTableFile(string filePath)
    {
        var lines = File.ReadAllLines(filePath);
        var existingData = _db.WeatherDataPoints
            .AsEnumerable()
            .Select(w => (w.StationName.ToUpper(), w.Timestamp))
            .ToHashSet();

        var seenInFile = new HashSet<(string, DateTime)>();
        var validWeatherDataPoints = new List<WeatherDataPoint>();
        var errors = new List<string>();
        var badData = new List<string>();

        //Utgår från observationsgränser i Sveirge där giltighetsintervallet är satt mellan -60 och +40 grader celsius.
        var MinTemp = -60;
        var MaxTemp = 40;

        for (int i = 1; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (line == "[" || line == "]" || string.IsNullOrWhiteSpace(line))
            {
                continue;
            }
            string json = line.TrimEnd(',');
            
            int rowNumber = i + 1;
            string stationName = "";
            string timestamp = "";
            string temperature = "";
            string humidity = "";

            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                stationName = GetField(root, "StationName");
                timestamp = GetField(root, "Timestamp");
                temperature = GetField(root, "Temperature");
                humidity = GetField(root, "Humidity");

                
            }
            catch (JsonException ex)
            {
                errors.Add($"Rad {rowNumber} är ogiltig - {ex.Message}");
                badData.Add(lines[i]);
                continue;
            }




            // Validera stationsnamn & dubbletter
            if (string.IsNullOrWhiteSpace(stationName))
            {
                errors.Add($"Rad {rowNumber}: Ogiltigt Stationsnamn: '{stationName}'");
                badData.Add(lines[i]);
                continue;
            }

            if (!DateTime.TryParse(timestamp, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedTimestamp))
            {
                errors.Add($"Rad {rowNumber}: Tidsstämpeln '{timestamp}' är ogiltig.");
                badData.Add(lines[i]);
                continue;
            }

            if (!double.TryParse(temperature, CultureInfo.InvariantCulture, out double parsedTemperature))
            {

                errors.Add($"Rad {rowNumber}: Ogiltigt värde '{temperature}'.");
                badData.Add(lines[i]);
                continue;

            }

            if (parsedTemperature < MinTemp || parsedTemperature > MaxTemp)
            {
                errors.Add($"Rad {rowNumber}: Ogiltigt värde '{temperature}' ligger utanför giltigt intervall.");
                badData.Add(lines[i]);
                continue;
            }

            if (!double.TryParse(humidity, CultureInfo.InvariantCulture, out double parsedHumidity))
            {
                    errors.Add($"Rad {rowNumber}: Luftfuktigheten '{humidity}' är ogiltig.");
                    badData.Add(lines[i]);
                    continue;
            }

            if (parsedHumidity < 0 || parsedHumidity > 100)
            {
                errors.Add($"Rad {rowNumber}: Luftfuktigheten '{humidity}' är ogiltig.");
                badData.Add(lines[i]);
                continue;
            }

            if (existingData.Contains((stationName, parsedTimestamp)) || !seenInFile.Add((stationName, parsedTimestamp)))
            {
                errors.Add($"Rad {rowNumber}: Dubblett av stationen: '{stationName}'");
                badData.Add(lines[i]);
                continue;
            }


            // godkänd datarad
            validWeatherDataPoints.Add(new WeatherDataPoint
            {
                StationName = stationName,
                Timestamp = parsedTimestamp,
                Temperature = parsedTemperature,
                Humidity = parsedHumidity,
                DataSource = "JSON-File"
            });
        }

        File.WriteAllLines("bad_data.log", badData);
        _db.WeatherDataPoints.AddRange(validWeatherDataPoints);
        _db.SaveChanges();

        Console.WriteLine($"Import klar! Godkända: {validWeatherDataPoints.Count} | Avvisade: {errors.Count}");
        foreach (var err in errors)
        {
            Console.WriteLine($"  - {err}");
        }
    }






    private static string GetField(JsonElement root,string fieldName)
    {
        if (root.TryGetProperty(fieldName, out var name))
        {
            if (name.ValueKind == JsonValueKind.String)
            {
                return name.GetString() ?? "";
            }
            else if (name.ValueKind == JsonValueKind.Number)
            {
                return name.GetRawText();
            }
            else return "";
        }
        return "";
    }
}