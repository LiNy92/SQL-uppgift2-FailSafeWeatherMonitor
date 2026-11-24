using FailSafeWeatherMonitor.Core;
using System.Globalization;
using System.Text.Json;

namespace FailSafeWeatherMonitor.Data;

public record ImportResult(int ImportedCount, List<string> Errors);
public class WeatherDataImporter(FailSafeWeatherMonitorDbContext _db)
{
    public ImportResult ImportFromTableFile(string filePath)
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

        //Utgår från observationsgränser i Sveirge där giltighetsintervallet är satt mellan -60 och +40 grader celsius, tar höjd med 10 grader åt vardera håll för framtida förändringar.
        var MinTemp = -70;
        var MaxTemp = 50;

        for (int i = 0; i < lines.Length; i++)
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
                stationName = GetField(root, "StationName").Trim();
                timestamp = GetField(root, "Timestamp").Trim();
                temperature = GetField(root, "Temperature").Trim();
                humidity = GetField(root, "Humidity").Trim();


            }
            catch (JsonException ex)
            {
                errors.Add($"Rad {rowNumber} Jsonfel '{ex.Message}'");
                badData.Add(lines[i]);
                continue;
            }



            // Validera indata & dubbletter
            if (string.IsNullOrWhiteSpace(stationName) || stationName.Length > 50)
            {
                errors.Add($"Rad {rowNumber}: Stationsnamn: Ogiltigt '{stationName}'");
                badData.Add(lines[i]);
                continue;
            }

            if (!DateTime.TryParse(timestamp, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedTimestamp))
            {
                errors.Add($"Rad {rowNumber}: Tidsstämpel: Ogiltigt värde '{timestamp}'");
                badData.Add(lines[i]);
                continue;
            }

            temperature = temperature.Replace(',', '.');
            if (!double.TryParse(temperature, CultureInfo.InvariantCulture, out double parsedTemperature))
            {

                errors.Add($"Rad {rowNumber}: Temperatur: Ogiltigt värde '{temperature}'.");
                badData.Add(lines[i]);
                continue;

            }

            if (parsedTemperature < MinTemp || parsedTemperature > MaxTemp)
            {
                errors.Add($"Rad {rowNumber}: Temperatur: Ogiltigt värde '{temperature}' ligger utanför giltigt intervall.");
                badData.Add(lines[i]);
                continue;
            }

            humidity = humidity.Replace(',', '.');
            if (!double.TryParse(humidity, CultureInfo.InvariantCulture, out double parsedHumidity))
            {
                errors.Add($"Rad {rowNumber}: Luftfuktigheten: Ogiltigt värde '{humidity}'.");
                badData.Add(lines[i]);
                continue;
            }

            if (parsedHumidity < 0 || parsedHumidity > 100)
            {
                errors.Add($"Rad {rowNumber}: Luftfuktigheten: Ogiltigt värde '{humidity}'");
                badData.Add(lines[i]);
                continue;
            }

            if (existingData.Contains((stationName.ToUpper(), parsedTimestamp)) || !seenInFile.Add((stationName.ToUpper(), parsedTimestamp)))
            {
                errors.Add($"Rad {rowNumber}: Stationsnamn: Dubblett av '{stationName}'");
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


        try
        {
            _db.WeatherDataPoints.AddRange(validWeatherDataPoints);
            _db.SaveChanges();
        }
        catch (Exception ex)
        {
            errors.Add($"Fel:Gick ej att spara till databas: {ex.Message}");
            return new ImportResult(0, errors);
        }
        return new ImportResult(validWeatherDataPoints.Count, errors);

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