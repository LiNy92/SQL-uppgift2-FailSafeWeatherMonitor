using FailSafeWeatherMonitor.Core;
using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using System.Data.Common;

namespace FailSafeWeatherMonitor.Data;

public record ImportResult(int ImportedCount, int RejectedCount, List<string> Errors);
public class WeatherDataImporter(FailSafeWeatherMonitorDbContext _db)
{
    public async Task<ImportResult> ImportFromFileAsync(string filePath)
    {
        var lines = await File.ReadAllLinesAsync(filePath);
        var errors = new List<string>();
        HashSet<(string StationName, DateTime Timestamp)> existingData;

        try
        {
            existingData = (await _db.WeatherDataPoints
                .AsNoTracking()
                .Select(w => new { w.StationName, w.Timestamp })
                .ToListAsync())
                .Select(w => (w.StationName.ToUpperInvariant(), w.Timestamp))
                .ToHashSet();
        }
        catch (DbException ex)
        {
            errors.Add($"Kunde inte läsa befintliga mätningar från databasen: {ex.Message}");
            return new ImportResult(0, 0, errors);
        }
        catch(InvalidOperationException ex)
        {
            errors.Add($"Ingen åtkomst till databasen. Kontrollera databasanslutningen. Felmeddelande: {ex.GetBaseException().Message}");
            return new ImportResult(0, 0, errors);
        }
        catch(Exception ex)
        {
            errors.Add($"Ett oväntat fel uppstod när befintliga mätningar lästes från databasen: {ex.Message}");
            return new ImportResult(0, 0, errors);
        }
        var seenInFile = new HashSet<(string, DateTime)>();
        var validWeatherDataPoints = new List<WeatherDataPoint>();
        var badData = new List<string>();
        var duplicateRowsCount = 0;

        //Utgår från observationsgränser i Sverige där giltighetsintervallet är satt mellan -60 och +40 grader celsius, tar höjd med 5 grader extra åt vardera håll för framtida förändringar.
        var MinTemp = -65;
        var MaxTemp = +45;

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


            //Bryta ur metod ?
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
                errors.Add($"Rad {rowNumber}: Stationsnamn: Ogiltigt värde '{stationName}'");
                badData.Add(lines[i]);
                continue;
            }

            if (!DateTime.TryParse(timestamp, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedTimestamp))
            {
                errors.Add($"Rad {rowNumber}: Tidsstämpel: Ogiltigt värde '{timestamp}'");
                badData.Add(lines[i]);
                continue;
            }

            if (!TryParseDouble(temperature, out double parsedTemperature))
            {

                errors.Add($"Rad {rowNumber}: Temperatur: Ogiltigt värde '{temperature}'.");
                badData.Add(lines[i]);
                continue;

            }

            if (!double.IsFinite(parsedTemperature) || parsedTemperature < MinTemp || parsedTemperature > MaxTemp)
            {
                errors.Add($"Rad {rowNumber}: Temperatur: '{temperature}' är ogiltig eller ligger utanför giltigt intervall {MinTemp} - {MaxTemp} °C.");
                badData.Add(lines[i]);
                continue;
            }


            if (!TryParseDouble(humidity, out double parsedHumidity))
            {
                errors.Add($"Rad {rowNumber}: Luftfuktigheten: Ogiltigt värde '{humidity}'.");
                badData.Add(lines[i]);
                continue;
            }

            if (!double.IsFinite(parsedHumidity) || parsedHumidity < 0 || parsedHumidity > 100)
            {
                errors.Add($"Rad {rowNumber}: Luftfuktigheten: '{humidity}' är ogiltig eller ligger utanför giltigt intervall (0-100 %)");
                badData.Add(lines[i]);
                continue;
            }

            if (existingData.Contains((stationName.ToUpperInvariant(), parsedTimestamp)) || !seenInFile.Add((stationName.ToUpperInvariant(), parsedTimestamp)))
            {
                errors.Add($"Rad {rowNumber}: Stationsnamn: Dubblett av '{stationName}'");
                duplicateRowsCount++;
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

        await TryWriteBadDataLogAsync(badData, errors);

        if (!await TrySaveAsync(validWeatherDataPoints, errors))
        {
            return new ImportResult(0, badData.Count + duplicateRowsCount, errors);
        }

        return new ImportResult(validWeatherDataPoints.Count, badData.Count + duplicateRowsCount, errors);
    }





    private static async Task TryWriteBadDataLogAsync(List<string> badData, List<string> errors)
    {
        try
        {
            var logPath = Path.Combine(AppContext.BaseDirectory, "bad_data.log");
            await File.WriteAllLinesAsync(logPath, badData);
        }
        catch (Exception ex)
        {
            errors.Add($"Fel: Gick ej att skriva bad_data.log: {ex.Message}");
        }
    }
    private async Task<bool> TrySaveAsync(List<WeatherDataPoint> points, List<string> errors)
    {
        try
        {
            await _db.WeatherDataPoints.AddRangeAsync(points);
            await _db.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            errors.Add($"Fel: Gick ej att spara till databas: {ex.Message}");
            return false;
        }
    }

    private static string GetField(JsonElement root, string fieldName)
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

    private static bool TryParseDouble(string text, out double result)
    {
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out result))
            return true;
        else
        {
            return double.TryParse(text, NumberStyles.Float, new CultureInfo("sv-SE"), out result);
        }
    }

}