using System.IO;
using System.Text.Json;

namespace WarehouseInventory.Services;

public class StockPulseConfig
{
    public const string DefaultBaseUrl = "https://stockpulse-backend-production-4c30.up.railway.app";

    public string BaseUrl { get; set; } = DefaultBaseUrl;

    public static StockPulseConfig Load()
    {
        var config = new StockPulseConfig();

        // 1. Check environment variables
        var envUrl = Environment.GetEnvironmentVariable("STOCKPULSE_API_URL")
            ?? Environment.GetEnvironmentVariable("Backend__BaseUrl");
        if (!string.IsNullOrWhiteSpace(envUrl))
        {
            config.BaseUrl = envUrl.Trim().TrimEnd('/');
            return config;
        }

        // 2. Check appsettings.json in current executable directory or working directory
        try
        {
            var paths = new[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json"),
                Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json")
            };

            foreach (var path in paths)
            {
                if (File.Exists(path))
                {
                    var json = File.ReadAllText(path);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("Backend", out var backendProp) &&
                        backendProp.TryGetProperty("BaseUrl", out var urlProp) &&
                        !string.IsNullOrWhiteSpace(urlProp.GetString()))
                    {
                        config.BaseUrl = urlProp.GetString()!.Trim().TrimEnd('/');
                        return config;
                    }
                }
            }
        }
        catch
        {
            // fallback to default
        }

        return config;
    }

    public static void SaveBaseUrl(string newUrl)
    {
        try
        {
            var cleanedUrl = newUrl.Trim().TrimEnd('/');
            var paths = new[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json"),
                Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json")
            };

            var payload = new
            {
                Backend = new
                {
                    BaseUrl = cleanedUrl
                }
            };

            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
            foreach (var path in paths)
            {
                try
                {
                    File.WriteAllText(path, json);
                }
                catch
                {
                    // ignore if one path fails
                }
            }
        }
        catch
        {
            // ignore save failure
        }
    }
}
