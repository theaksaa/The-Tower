using System;
using System.IO;
using UnityEngine;

public static class ServerConfigService
{
    private const string ConfigFileName = "server_config.json";
    public const string DefaultServerBaseUrl = "http://127.0.0.1:3000";

    [Serializable]
    private sealed class ServerUrlConfig
    {
        public string serverUrl;
    }

    public static string ResolveBaseUrl()
    {
        var configPath = Path.Combine(Application.persistentDataPath, ConfigFileName);
        if (File.Exists(configPath))
        {
            var existingJson = File.ReadAllText(configPath);
            var existingConfig = JsonUtility.FromJson<ServerUrlConfig>(existingJson);
            var normalizedExistingUrl = NormalizeServerBaseUrl(existingConfig?.serverUrl);
            if (!string.IsNullOrWhiteSpace(normalizedExistingUrl))
            {
                return normalizedExistingUrl;
            }
        }

        var fallbackConfig = new ServerUrlConfig
        {
            serverUrl = DefaultServerBaseUrl
        };

        File.WriteAllText(configPath, JsonUtility.ToJson(fallbackConfig, true));
        return DefaultServerBaseUrl;
    }

    public static string NormalizeServerBaseUrl(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmedValue = value.Trim().TrimEnd('/');
        if (trimmedValue.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            trimmedValue.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return trimmedValue;
        }

        return $"http://{trimmedValue}:3000";
    }
}
