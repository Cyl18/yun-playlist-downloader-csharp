using Microsoft.Extensions.Configuration;

namespace YunPlaylistDownloader.Services;

public class ConfigService
{
    private readonly IConfiguration _configuration;

    public ConfigService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public T GetValue<T>(string key, T defaultValue)
    {
        return _configuration.GetValue(key, defaultValue) ?? defaultValue;
    }

    public void SaveConfig(string key, object value)
    {
        // For simplicity, we'll just use in-memory configuration
        // In a full implementation, you might want to save to a file
        _configuration[key] = value?.ToString();
    }
}
