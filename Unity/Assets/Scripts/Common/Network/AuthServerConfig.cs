using System;
using UnityEngine;

public static class AuthServerConfig
{
    private const string DefaultBaseUrl = "http://3.37.215.9:5000";
    private const string EnvKey = "AUTH_SERVER_URL";
    private const string MatchmakingEnvKey = "MATCHMAKING_SERVER_URL";

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

        string fromMatchmaking = ResolveFromMatchmakingUrl();
        if (!string.IsNullOrWhiteSpace(fromMatchmaking))
        {
            Debug.Log($"[AuthServerConfig] use {MatchmakingEnvKey} derived url: {fromMatchmaking}");
            return fromMatchmaking;
        }

        Debug.LogWarning($"[AuthServerConfig] {EnvKey} not set. fallback: {DefaultBaseUrl}");
        return DefaultBaseUrl;
    }

    private static string NormalizeUrl(string url)
    {
        return url.Trim().TrimEnd('/');
    }

    private static string ResolveFromMatchmakingUrl()
    {
        string matchmakingUrl = Environment.GetEnvironmentVariable(MatchmakingEnvKey);
        if (string.IsNullOrWhiteSpace(matchmakingUrl))
        {
            matchmakingUrl = PlayerPrefs.GetString(MatchmakingEnvKey, string.Empty);
        }

        if (string.IsNullOrWhiteSpace(matchmakingUrl) ||
            Uri.TryCreate(matchmakingUrl, UriKind.Absolute, out Uri uri) == false)
        {
            return string.Empty;
        }

        return NormalizeUrl($"{uri.Scheme}://{uri.Host}:5000");
    }
}
