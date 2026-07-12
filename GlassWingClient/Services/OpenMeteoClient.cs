using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace GlassWingClient.Services;

// Deliberately given its own plain HttpClient (registered without GlassWingAuthHandler in
// Program.cs) — reusing GlassWingApiClient's client here would leak the player's bearer
// token to a third-party domain, since that handler attaches it to every outgoing request.
public class OpenMeteoClient(HttpClient http)
{
    public async Task<(double Latitude, double Longitude)?> GeocodeAsync(string? country, string? state)
    {
        var query = string.IsNullOrWhiteSpace(state)
            ? country
            : string.IsNullOrWhiteSpace(country) ? state : $"{state}, {country}";
        if (string.IsNullOrWhiteSpace(query)) return null;

        try
        {
            var url = $"https://geocoding-api.open-meteo.com/v1/search?name={Uri.EscapeDataString(query)}&count=1&language=en&format=json";
            var result = await http.GetFromJsonAsync<GeocodingResponse>(url);
            var first = result?.Results?.FirstOrDefault();
            return first is null ? null : (first.Latitude, first.Longitude);
        }
        catch
        {
            return null;
        }
    }

    public async Task<(double TemperatureCelsius, double RelativeHumidityPercent)?> GetCurrentWeatherAsync(double latitude, double longitude)
    {
        try
        {
            var url = $"https://api.open-meteo.com/v1/forecast?latitude={latitude}&longitude={longitude}&current=temperature_2m,relative_humidity_2m";
            var result = await http.GetFromJsonAsync<ForecastResponse>(url);
            var current = result?.Current;
            return current is null ? null : (current.Temperature2m, current.RelativeHumidity2m);
        }
        catch
        {
            return null;
        }
    }

    record GeocodingResponse([property: JsonPropertyName("results")] GeocodingResult[]? Results);
    record GeocodingResult(
        [property: JsonPropertyName("latitude")] double Latitude,
        [property: JsonPropertyName("longitude")] double Longitude);

    record ForecastResponse([property: JsonPropertyName("current")] CurrentWeather? Current);
    record CurrentWeather(
        [property: JsonPropertyName("temperature_2m")] double Temperature2m,
        [property: JsonPropertyName("relative_humidity_2m")] double RelativeHumidity2m);
}
