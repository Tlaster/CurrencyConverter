using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CurrencyConverter;

public class Converter : IDisposable
{
    private readonly HttpClient _httpClient = new();

    public void Dispose()
    {
        _httpClient.Dispose();
    }

    public async Task<CurrencyResult?> Exchange(decimal amount, string source, string[] target, CancellationToken token)
    {
        // fetch exchange rate from https://cdn.jsdelivr.net/npm/@fawazahmed0/currency-api@latest/v1/currencies/{source}.min.json
        var response = await _httpClient.GetAsync(
            $"https://cdn.jsdelivr.net/npm/@fawazahmed0/currency-api@latest/v1/currencies/{source.ToLowerInvariant()}.min.json", token);
        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync(token);
            var (date, currencyRates) = ParseCurrencyData(content);
            var result = currencyRates
                .Where(x => target.Contains(x.Key, StringComparer.OrdinalIgnoreCase))
                // keep the same order as the target array
                .OrderBy(x => Array.FindIndex(target, y => y.Equals(x.Key, StringComparison.OrdinalIgnoreCase)))
                .Select(x => new CurrencyRate(amount * x.Value, x.Key, date))
                .ToList();
            return new CurrencyResult(date, result);
        }

        ExtensionHost.LogMessage($"Failed to fetch exchange rate for '{source}'.");

        return null;
    }

    /// <summary>
    ///     Parse the JSON and return a CurrencyData object containing date and rates
    /// </summary>
    /// <param name="jsonString">The JSON string to parse</param>
    /// <returns>CurrencyData object with date and rates</returns>
    public static CurrencyData ParseCurrencyData(string jsonString)
    {
        var date = string.Empty;
        var rates = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

        using (var doc = JsonDocument.Parse(jsonString))
        {
            var root = doc.RootElement;

            // Extract the date
            if (root.TryGetProperty("date", out var dateElement)) date = dateElement.GetString() ?? string.Empty;

            // Find the first non-date property (assumed to be the currency rates object)
            foreach (var property in root.EnumerateObject().Where(property => property.Name != "date"))
            {
                // We found the rates object, now extract all rates
                foreach (var rateProp in property.Value.EnumerateObject())
                {
                    var code = rateProp.Name;
                    var rate = rateProp.Value.GetDecimal();
                    rates[code] = rate;
                }

                // We only need the first non-date property
                break;
            }
        }

        return new CurrencyData(date, rates);
    }
}

public record CurrencyRate(decimal Rate, string Symbol, string? Date)
{
    public string RateHumanized => FormatNumber(Rate);

    private static string FormatNumber(decimal number)
    {
        if (number == Math.Floor(number)) return number.ToString("0");

        var absNumber = Math.Abs(number);
        var fractionalPart = absNumber - Math.Floor(absNumber);

        if (fractionalPart is < 0.01m and > 0) return number.ToString();

        return number.ToString("0.##");
    }
}

[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(CurrencyData))]
public partial class DictionaryJsonContext : JsonSerializerContext
{
}

/// <summary>
///     Record to hold both date and currency rates
/// </summary>
public record CurrencyData(string Date, IReadOnlyDictionary<string, decimal> Rates);

public record CurrencyResult(string Date, IReadOnlyList<CurrencyRate> Rates);