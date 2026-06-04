using System;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;

public sealed class BrowserTvConfig
{
    public bool EnableBrowserTv = true;
    public string BridgeInternalUrl = "http://127.0.0.1:8787";
    public string BridgePublicUrl = "http://127.0.0.1:8787";
    public string ServerSecret = "change-me-browser-tv-secret";
    public string DefaultUrl = "https://www.google.com";
    public bool SpatialAudioEnabled = true;
    public float AudioMinDistance = 2f;
    public float AudioMaxDistance = 20f;
    public float AudioRolloffPower = 1.5f;

    public static BrowserTvConfig Current { get; private set; } = new BrowserTvConfig();

    public static void Load()
    {
        BrowserTvConfig config = new BrowserTvConfig();
        string modPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        string configPath = Path.Combine(modPath, "Config", "browser-tv.json");
        if (File.Exists(configPath))
        {
            string json = File.ReadAllText(configPath);
            config.EnableBrowserTv = GetBool(json, "enableBrowserTv", config.EnableBrowserTv);
            config.BridgeInternalUrl = TrimSlash(GetString(json, "bridgeInternalUrl", config.BridgeInternalUrl));
            config.BridgePublicUrl = TrimSlash(GetString(json, "bridgePublicUrl", config.BridgePublicUrl));
            config.ServerSecret = GetString(json, "serverSecret", config.ServerSecret);
            config.DefaultUrl = GetString(json, "defaultUrl", config.DefaultUrl);
            config.SpatialAudioEnabled = GetBool(json, "spatialAudioEnabled", config.SpatialAudioEnabled);
            config.AudioMinDistance = GetFloat(json, "audioMinDistance", config.AudioMinDistance);
            config.AudioMaxDistance = GetFloat(json, "audioMaxDistance", config.AudioMaxDistance);
            config.AudioRolloffPower = GetFloat(json, "audioRolloffPower", config.AudioRolloffPower);
        }

        config.AudioMinDistance = Mathf.Max(0f, config.AudioMinDistance);
        config.AudioMaxDistance = Mathf.Max(config.AudioMinDistance + 0.1f, config.AudioMaxDistance);
        config.AudioRolloffPower = Mathf.Max(0.1f, config.AudioRolloffPower);
        Current = config;
        Debug.Log("[BrowserTV] Config loaded. BridgePublicUrl=" + config.BridgePublicUrl + ", BridgeInternalUrl=" + config.BridgeInternalUrl + ", SpatialAudio=" + config.SpatialAudioEnabled + ", AudioMaxDistance=" + config.AudioMaxDistance);
    }

    private static string TrimSlash(string value)
    {
        return string.IsNullOrEmpty(value) ? value : value.TrimEnd('/');
    }

    private static string GetString(string json, string name, string fallback)
    {
        Match match = Regex.Match(json, "\"" + Regex.Escape(name) + "\"\\s*:\\s*\"([^\"]*)\"");
        return match.Success ? match.Groups[1].Value : fallback;
    }

    private static bool GetBool(string json, string name, bool fallback)
    {
        Match match = Regex.Match(json, "\"" + Regex.Escape(name) + "\"\\s*:\\s*(true|false)", RegexOptions.IgnoreCase);
        return match.Success ? bool.Parse(match.Groups[1].Value) : fallback;
    }

    private static float GetFloat(string json, string name, float fallback)
    {
        Match match = Regex.Match(json, "\"" + Regex.Escape(name) + "\"\\s*:\\s*(-?\\d+(?:\\.\\d+)?)", RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            return fallback;
        }

        return float.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float value)
            ? value
            : fallback;
    }
}
