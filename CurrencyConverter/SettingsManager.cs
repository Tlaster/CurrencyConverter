using System.IO;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CurrencyConverter;

public class SettingsManager : JsonSettingsManager
{
    private const string Namespace = "CurrencyConverter";
    private static SettingsManager? _instance;

    private SettingsManager()
    {
        FilePath = SettingsJsonPath();

        Settings.Add(DefaultTargetCurrency);

        // Load settings from file upon initialization
        LoadSettings();

        Settings.SettingsChanged += (s, a) => SaveSettings();
    }

    internal static SettingsManager Instance
    {
        get
        {
            _instance ??= new SettingsManager();
            return _instance;
        }
    }

    public TextSetting DefaultTargetCurrency { get; } = new(
        Namespaced("defaultTargetCurrency"),
        "Default target currency",
        "The default target currency to use when converting. This is used when no target currency is specified in the command.",
        "USD"
    );

    private static string Namespaced(string propertyName)
    {
        return $"{Namespace}.{propertyName}";
    }

    private static string SettingsJsonPath()
    {
        var directory = Utilities.BaseSettingsPath("Microsoft.CmdPal");
        Directory.CreateDirectory(directory);

        // now, the state is just next to the exe
        return Path.Combine(directory, "CurrencyConverter.json");
    }
}