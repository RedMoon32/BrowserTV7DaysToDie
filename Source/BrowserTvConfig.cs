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
        }

        Current = config;
        Debug.Log("[BrowserTV] Config loaded. BridgePublicUrl=" + config.BridgePublicUrl + ", BridgeInternalUrl=" + config.BridgeInternalUrl);
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

}
