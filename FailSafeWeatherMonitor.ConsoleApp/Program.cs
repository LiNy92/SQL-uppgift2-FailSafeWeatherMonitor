using Spectre.Console;

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
                case "Importera histortik":
                    {
                        break;
                    }

                case "Hämta live-data":
                    {
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
    }
}