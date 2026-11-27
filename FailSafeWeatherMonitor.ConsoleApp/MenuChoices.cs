using System;
using System.Collections.Generic;
using System.Text;

namespace FailSafeWeatherMonitor.ConsoleApp;

public class MenuChoices
{
    public const string ImportFile = "Importera historisk data från fil";
    public const string FetchApi = "Hämta live-data från Api";
    public const string ShowStats = "Visa väderstatistik";
    public const string Exit = "Avsluta program";
}
