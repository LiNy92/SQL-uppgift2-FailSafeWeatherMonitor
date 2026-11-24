using FailSafeWeatherMonitor.Data;
using Spectre.Console;
using System.Text.Json;

namespace FailSafeWeatherMonitor.ConsoleApp;


internal class Program
{
    static void Main(string[] args)
    {
        while (true)
        {
            var choice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                .Title("[bold][NavajoWhite1]=== Weather Monitor ===[/][/]")
               .HighlightStyle(new Style(foreground: Color.LightPink3))
                .AddChoices(
                "Importera historik",
                "Hämta live-data",
                "Visa väderstatistik",
                "Avsluta program"));

            AnsiConsole.MarkupLine($"[NavajoWhite1]{choice}[/]");


            switch (choice)
            {
                case "Importera historik":
                    {
                        using var db = new FailSafeWeatherMonitorDbContext();
                        var importFile = new WeatherDataImporter(db);

                        ImporteraJsonFil(importFile);


                        //importFile.ImportFromTableFile("weather_data.json");
                        break;
                    }

                case "Hämta live-data":
                    {
                        //Metod för live-data
                        break;
                    }

                case "Visa väderstatistik":
                    {
                        break;
                    }
                case "Avsluta program":
                    {
                        return;
                    }
            }
            Console.WriteLine();
            AnsiConsole.Prompt(
    new TextPrompt<string>("Tryck [PaleGreen3_1]Enter[/] för att återgå till meny.")
    .AllowEmpty());
            Console.WriteLine();

        }




        static void ImporteraJsonFil(WeatherDataImporter importFile)
        {

            try
            {
                var result = importFile.ImportFromTableFile("weather_data.json");

                AnsiConsole.MarkupLine($"[PaleGreen3_1]Import klar![/] Godkända: [bold]{result.ImportedCount}[/] | Avvisade: [bold]{result.Errors.Count}[/]");
                foreach (var err in result.Errors)
                {
                    AnsiConsole.MarkupLine($"  - {Markup.Escape(err)}");
                }

            }
            catch (IOException ex)
            {
                AnsiConsole.MarkupLine($"[red]Kunde inte läsa JSON-filen:[/] {Markup.Escape(ex.Message)}");
            }
            catch (JsonException ex)
            {
                AnsiConsole.MarkupLine($"[red]Ogiltig Json-fil[] '{Markup.Escape(ex.Message)}'");
            }
        }

    }
}