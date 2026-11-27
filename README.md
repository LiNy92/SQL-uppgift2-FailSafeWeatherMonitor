# Inlämningsuppgif 2
##  C#-konsolapplikation med en relationsdatabas via Entity Framework Core. 
Programmet importerar väderdata från fil och Open-Meteo API till en SQL-databas och visar statistik med Spectre.Console.

Körning:
Klona repot och öppna lösningen.
Databasen skapas lokalt med Update-Database (Default project: Data, startup: ConsoleApp).
Starta ConsoleApp och välj i menyn.
Validering och felhantering: kort om TryParse, rimlighetsgränser (-65 till 45 °C, 0 till 100 % luftfuktighet), dubblettkoll på station + tid, och att råa felrader skrivs till bad_data.log (9 rader: 14 minus de 5 räddade kommatalen).
Molndatabas (Azure SQL):
Databasen ligger i Azure SQL och tabellerna är migrerade med EF Core (Update-Database).
Appen kör lokalt som standard mot LocalDB via appsettings.json.

Skärmdumpar från Azure:
Vid skapandet av databasen i molnet kördes de två avkommenterade raderna i FailSafeWeatherMonitorDbContext.
Raderna innehåller "appsettings.Production.json" och tillägg av User Secrets för att ej synliggöra lösenordet till GitHub. Detta är därmed ej upprepningsbart pga att lösenordet finns i User Secrets och molndatabasen är raderad.
I bilderna syns det att det finns data på 192 rader. De 191 som är inlästa från fil och 1 från API.

