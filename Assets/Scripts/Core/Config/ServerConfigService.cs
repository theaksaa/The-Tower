using System;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

public static class ServerConfigService
{
    private const string ConfigFileName = "server_config.json";
    private const string TrustedServerHost = "159.69.242.255";
    private const int DefaultServerPort = 3000;
    public const string DefaultServerBaseUrl = "https://159.69.242.255:3000";

    [Serializable]
    private sealed class ServerUrlConfig
    {
        public string serverUrl = null;
    }

    private sealed class TrustedServerCertificateHandler : CertificateHandler
    {
        protected override bool ValidateCertificate(byte[] certificateData)
        {
            return true;
        }
    }

    public static string ResolveBaseUrl()
    {
        var configPath = Path.Combine(Application.persistentDataPath, ConfigFileName);
        if (!File.Exists(configPath))
        {
            return PersistBaseUrl(configPath, DefaultServerBaseUrl);
        }

        var existingJson = File.ReadAllText(configPath);
        var existingConfig = JsonUtility.FromJson<ServerUrlConfig>(existingJson);
        var normalizedExistingUrl = NormalizeServerBaseUrl(existingConfig?.serverUrl);
        return !string.IsNullOrWhiteSpace(normalizedExistingUrl)
            ? normalizedExistingUrl
            : DefaultServerBaseUrl;
    }

    public static string NormalizeServerBaseUrl(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmedValue = value.Trim().TrimEnd('/');
        if (TryExtractHostAndPort(trimmedValue, out var host, out var port) &&
            string.Equals(host, TrustedServerHost, StringComparison.OrdinalIgnoreCase))
        {
            return $"https://{TrustedServerHost}:{port ?? DefaultServerPort}";
        }

        if (trimmedValue.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            trimmedValue.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return trimmedValue;
        }

        if (TryExtractHostAndPort(trimmedValue, out host, out port))
        {
            return port.HasValue
                ? $"http://{host}:{port.Value}"
                : $"http://{host}:{DefaultServerPort}";
        }

        return $"http://{trimmedValue}:{DefaultServerPort}";
    }

    public static void ApplyTrustedServerCertificatePolicy(UnityWebRequest request, string url)
    {
        if (request == null || !RequiresTrustedServerCertificateBypass(url))
        {
            return;
        }

        request.certificateHandler = new TrustedServerCertificateHandler();
        request.disposeCertificateHandlerOnDispose = true;
    }

    private static string PersistBaseUrl(string configPath, string baseUrl)
    {
        var fallbackConfig = new ServerUrlConfig
        {
            serverUrl = baseUrl
        };

        File.WriteAllText(configPath, JsonUtility.ToJson(fallbackConfig, true));
        return baseUrl;
    }

    private static bool RequiresTrustedServerCertificateBypass(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        return uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) &&
               uri.Port == DefaultServerPort &&
               string.Equals(uri.Host, TrustedServerHost, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryExtractHostAndPort(string value, out string host, out int? port)
    {
        host = value;
        port = null;

        var colonIndex = value.LastIndexOf(':');
        if (colonIndex <= 0 || colonIndex == value.Length - 1)
        {
            return !string.IsNullOrWhiteSpace(host);
        }

        var parsedHost = value[..colonIndex];
        var portText = value[(colonIndex + 1)..];
        if (!int.TryParse(portText, out var parsedPort))
        {
            return false;
        }

        host = parsedHost;
        port = parsedPort;
        return !string.IsNullOrWhiteSpace(host);
    }
}
