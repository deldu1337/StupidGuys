using System;
using UnityEngine;

public static class AuthServerConfig
{
    private const string DefaultBaseUrl = "http://localhost:5000";
    private const string EnvKey = "AUTH_SERVER_URL";

    private static string _cachedBaseUrl;

    public static string BaseUrl => _cachedBaseUrl ??= ResolveBaseUrl();

    public static void OverrideForRuntime(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        _cachedBaseUrl = NormalizeUrl(url);
    }

    private static string ResolveBaseUrl()
    {
        string fromEnv = Environment.GetEnvironmentVariable(EnvKey);
        if (!string.IsNullOrWhiteSpace(fromEnv))
        {
            string resolved = NormalizeUrl(fromEnv);
            Debug.Log($"[AuthServerConfig] use env {EnvKey}: {resolved}");
            return resolved;
        }

        string fromPlayerPrefs = PlayerPrefs.GetString(EnvKey, string.Empty);
        if (!string.IsNullOrWhiteSpace(fromPlayerPrefs))
        {
            string resolved = NormalizeUrl(fromPlayerPrefs);
            Debug.Log($"[AuthServerConfig] use PlayerPrefs {EnvKey}: {resolved}");
            return resolved;
        }

        Debug.LogWarning($"[AuthServerConfig] {EnvKey} not set. fallback: {DefaultBaseUrl}");
        return DefaultBaseUrl;
    }

    private static string NormalizeUrl(string url)
    {
        return url.Trim().TrimEnd('/');
    }
}
