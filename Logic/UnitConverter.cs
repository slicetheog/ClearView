using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SpotlightClean.Logic
{
    public static class UnitConverter
    {
        private static readonly HttpClient httpClient = new HttpClient();
        private static readonly Dictionary<string, double> lengthConversions = new Dictionary<string, double>
        {
            { "m", 1 },
            { "cm", 0.01 },
            { "km", 1000 },
            { "in", 0.0254 },
            { "ft", 0.3048 },
            { "yd", 0.9144 },
            { "mi", 1609.34 }
        };

        public static async Task<string?> Convert(string query)
        {
            // Regex to parse queries like "10 usd to eur" or "10m to cm"
            var match = Regex.Match(query, @"([\d\.]+) ?([a-zA-Z]+) to ([a-zA-Z]+)"); // CHANGED: Made the space between number and unit optional.
            if (!match.Success) return null;

            if (double.TryParse(match.Groups[1].Value, out double value))
            {
                string fromUnit = match.Groups[2].Value.ToLower();
                string toUnit = match.Groups[3].Value.ToLower();

                // Length Conversion
                if (lengthConversions.ContainsKey(fromUnit) && lengthConversions.ContainsKey(toUnit))
                {
                    double valueInMeters = value * lengthConversions[fromUnit];
                    double result = valueInMeters / lengthConversions[toUnit];
                    return $"{result:F2} {toUnit}";
                }

                // Currency Conversion
                try
                {
                    string url = $"https://api.exchangerate-api.com/v4/latest/{fromUnit.ToUpper()}";
                    var response = await httpClient.GetStringAsync(url);
                    using (JsonDocument doc = JsonDocument.Parse(response))
                    {
                        var rates = doc.RootElement.GetProperty("rates");
                        if (rates.TryGetProperty(toUnit.ToUpper(), out JsonElement rateElement))
                        {
                            double rate = rateElement.GetDouble();
                            double result = value * rate;
                            return $"{result:F2} {toUnit.ToUpper()}";
                        }
                    }
                }
                catch
                {
                    // API might fail, or currency not found
                    return "Could not fetch currency conversion.";
                }
            }

            return null;
        }
    }
}