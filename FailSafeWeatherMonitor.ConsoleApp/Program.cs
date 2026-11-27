using FailSafeWeatherMonitor.Data;
using Microsoft.EntityFrameworkCore;
using Spectre.Console;

namespace FailSafeWeatherMonitor.ConsoleApp;


internal class Program
{

    static async Task Main(string[] args)
    {
        using var db = new FailSafeWeatherMonitorDbContext();
        while (true)
        {
            var choice = ShowMainMenu();

            AnsiConsole.MarkupLine($"[NavajoWhite1]{choice}[/]");

            if (!await RunChoiceAsync(choice, db))
                return;

            Console.WriteLine();
            AnsiConsole.Prompt(
    new TextPrompt<string>("Tryck [PaleGreen3_1]Enter[/] för att återgå till meny.")
    .AllowEmpty());
            Console.WriteLine();

        }
    }





    private static string ShowMainMenu()
    {
        return AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[bold][NavajoWhite1]=== Weather Monitor ===[/][/]")
                .HighlightStyle(new Style(foreground: Color.LightPink3))
                .AddChoices(
                    MenuChoices.ImportFile,
                    MenuChoices.FetchApi,
                    MenuChoices.ShowStats,
                    MenuChoices.Exit));
    }

    private static async Task<bool> RunChoiceAsync(string choice, FailSafeWeatherMonitorDbContext db)
    {
        switch (choice)
        {
            case MenuChoices.ImportFile:
                await ImportJsonFile(db);
                return true;
            case MenuChoices.FetchApi:
                await ShowWeatherApiImport(db);
                return true;
            case MenuChoices.ShowStats:
                await ShowWeatherStatistics(db);
                return true;
            case MenuChoices.Exit:
                return false;
            default:
                return true;
        }
    }


    private static async Task ShowWeatherApiImport(FailSafeWeatherMonitorDbContext _db)
    {
        try
        {
            var apiImporter = new WeatherApiImporter(_db);

            var result = await apiImporter.ImportGothenburgWeatherAsync();
            if (result.ImportedCount > 0)
            {
                AnsiConsole.MarkupLine($"[PaleGreen3_1]Import klar![/] Sparade: [bold]{result.ImportedCount}[/]");
            }
            else if (result.RejectedCount > 0)
            {
                AnsiConsole.MarkupLine($"[yellow]Ingen ny mätning sparades.[/] Avvisade: [bold]{result.RejectedCount}[/]");
            }
            else
            {
                AnsiConsole.MarkupLine("[red]Importen misslyckades.[/]");
            }
            foreach (var err in result.Errors)
            {
                AnsiConsole.MarkupLine($"  - {Markup.Escape(err)}");
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Oväntat fel vid API-anrop:[/] {Markup.Escape(ex.Message)}");
        }
    }

    private static async Task ImportJsonFile(FailSafeWeatherMonitorDbContext _db)
    {
        var importFile = new WeatherDataImporter(_db);
        try
        {
            var result = await importFile.ImportFromFileAsync("weather_data.json");
            if (result.ImportedCount > 0)
            {
                AnsiConsole.MarkupLine($"[PaleGreen3_1]Import klar![/] Godkända: [bold]{result.ImportedCount}[/] | Avvisade: [bold]{result.RejectedCount}[/]");
            }
            else if (result.RejectedCount > 0)
            {
                AnsiConsole.MarkupLine($"[yellow]Import genomförd men inga rader godkändes. Antal avvisade: {result.RejectedCount}[/]");
            }
            else
            {
                AnsiConsole.MarkupLine($"[red] Ingen import genomfördes.[/]");
            }
            foreach (var err in result.Errors)
            {
                AnsiConsole.MarkupLine($"  - {Markup.Escape(err)}");
            }

        }
        catch (IOException ex)
        {
            AnsiConsole.MarkupLine($"[red]Kunde inte läsa JSON-filen:[/] {Markup.Escape(ex.Message)}");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Oväntat fel.[/] {Markup.Escape(ex.Message)}");
        }

    }


    private static async Task<string?> ShowStations(FailSafeWeatherMonitorDbContext _db)
    {
        var stations = await _db.WeatherDataPoints
            .Select(p => p.StationName)
            .Distinct()
            .OrderBy(name => name)
            .ToListAsync();

        if (stations.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]Det finns inga sparade mätningar ännu[/]");
            return null;
        }
        return AnsiConsole.Prompt(
            new SelectionPrompt<string>()
            .Title("[bold][NavajoWhite1]=== Välj mätstation ===[/][/]")
               .HighlightStyle(new Style(foreground: Color.LightPink3))
                .AddChoices(stations));
    }

    private static async Task CreateTablesWeatherStats(string stationName, FailSafeWeatherMonitorDbContext _db)
    {
        var points = await _db.WeatherDataPoints
            .AsNoTracking()
            .Where(p => p.StationName == stationName)
            .OrderBy(p => p.Timestamp)
            .ToListAsync();

        if (points.Count == 0)
        {
            AnsiConsole.MarkupLine($"[yellow]Det finns inga mätningar för {Markup.Escape(stationName)}.[/]");
            return;
        }

        var minTemp = points.Min(p => p.Temperature);
        var maxTemp = points.Max(p => p.Temperature);
        var avgTemp = points.Average(p => p.Temperature);

        var summaryTable = new Table()
            .Title($"[bold]Statistik för {Markup.Escape(stationName)}[/]")
            .Border(TableBorder.Rounded)
            .AddColumn("Mått")
            .AddColumn(new TableColumn("Värde").RightAligned())
            .AddRow("Antal mätningar", points.Count.ToString())
            .AddRow("Lägsta temperatur", $"{minTemp:0.0} °C")
            .AddRow("Högsta temperatur", $"{maxTemp:0.0} °C")
            .AddRow("Medeltemperatur", $"{avgTemp:0.0} °C");

        var table = new Table()
            .Title($"[bold]Mätningar för {Markup.Escape(stationName)}[/]")
            .Border(TableBorder.Rounded)
            .AddColumn("Tidpunkt")
            .AddColumn(new TableColumn("Temperatur").RightAligned())
            .AddColumn(new TableColumn("Luftfuktighet").RightAligned())
            .AddColumn("Källa");

        foreach (var p in points)
        {
            table.AddRow(
                p.Timestamp.ToString("yyyy-MM-dd HH:mm"),
                $"{p.Temperature:0.0} °C",
                $"{p.Humidity:0.0} %",
                Markup.Escape(p.DataSource ?? "-")
            );
        }

        AnsiConsole.Write(new Columns(table, summaryTable));
    }


    private static async Task ShowWeatherStatistics(FailSafeWeatherMonitorDbContext db)
    {
        try
        {
            var selectedStation = await ShowStations(db);

            if (!string.IsNullOrWhiteSpace(selectedStation))
            {
                await CreateTablesWeatherStats(selectedStation, db);
            }
        }
        catch (InvalidOperationException ex)
        {
            AnsiConsole.MarkupLine(
                $"[red]Kunde inte läsa statistik från databasen:[/] " +
                $"{Markup.Escape(ex.GetBaseException().Message)}");
        }
        catch (System.Data.Common.DbException ex)
        {
            AnsiConsole.MarkupLine(
                $"[red]Databasfel när statistiken lästes:[/] " +
                $"{Markup.Escape(ex.Message)}");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Oväntat fel:[/] {Markup.Escape(ex.Message)}");
        }
    }

}