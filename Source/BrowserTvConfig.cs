using System;
using System.Collections.Generic;
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
    public int BrowserWidth = 1280;
    public int BrowserHeight = 720;
    public readonly Dictionary<string, float> AudioMaxDistanceByBlock = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
    {
        { "BrowserTV", 20f },
        { "BrowserTVWall", 30f },
        { "BrowserBigTV", 40f },
        { "BrowserTheaterScreen", 70f },
        { "BrowserBillboard", 70f }
    };

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
            config.BrowserWidth = GetInt(json, "browserWidth", config.BrowserWidth);
            config.BrowserHeight = GetInt(json, "browserHeight", config.BrowserHeight);
        }

        config.AudioMinDistance = Mathf.Max(0f, config.AudioMinDistance);
        config.AudioMaxDistance = Mathf.Max(config.AudioMinDistance + 0.1f, config.AudioMaxDistance);
        config.AudioRolloffPower = Mathf.Max(0.1f, config.AudioRolloffPower);
        config.BrowserWidth = Mathf.Clamp(config.BrowserWidth, 256, 7680);
        config.BrowserHeight = Mathf.Clamp(config.BrowserHeight, 144, 4320);
        ClampBlockAudioDistances(config);
        Current = config;
        Debug.Log("[BrowserTV] Config loaded. BridgePublicUrl=" + config.BridgePublicUrl + ", BridgeInternalUrl=" + config.BridgeInternalUrl + ", SpatialAudio=" + config.SpatialAudioEnabled + ", AudioMaxDistance=" + config.AudioMaxDistance);
    }

    public float GetAudioMaxDistance(string blockName)
    {
        if (!string.IsNullOrEmpty(blockName) && AudioMaxDistanceByBlock.TryGetValue(blockName, out float distance))
        {
            return distance;
        }

        return AudioMaxDistance;
    }

    private static void ClampBlockAudioDistances(BrowserTvConfig config)
    {
        foreach (string blockName in new List<string>(config.AudioMaxDistanceByBlock.Keys))
        {
            config.AudioMaxDistanceByBlock[blockName] = Mathf.Max(config.AudioMinDistance + 0.1f, config.AudioMaxDistanceByBlock[blockName]);
        }
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

    private static int GetInt(string json, string name, int fallback)
    {
        Match match = Regex.Match(json, "\"" + Regex.Escape(name) + "\"\\s*:\\s*(-?\\d+)", RegexOptions.IgnoreCase);
        if (!match.Success || !int.TryParse(match.Groups[1].Value, out int value))
        {
            return fallback;
        }

        return value;
    }
}
