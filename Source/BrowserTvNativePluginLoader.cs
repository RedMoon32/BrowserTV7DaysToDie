using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEngine;

public static class BrowserTvNativePluginLoader
{
    private static bool loaded;

    public static void Load()
    {
        if (loaded)
        {
            return;
        }

        string modPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        string pluginPath = GetPluginPath(modPath);
        if (string.IsNullOrEmpty(pluginPath) || !File.Exists(pluginPath))
        {
            Debug.LogError("[BrowserTV] WebRTC native plugin not found. Expected path: " + pluginPath);
            return;
        }

        if (Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor)
        {
            IntPtr handle = LoadLibrary(pluginPath);
            if (handle == IntPtr.Zero)
            {
                Debug.LogError("[BrowserTV] LoadLibrary failed for " + pluginPath + ", error=" + Marshal.GetLastWin32Error());
                return;
            }
        }

        loaded = true;
        Debug.Log("[BrowserTV] WebRTC native plugin loaded from " + pluginPath);
    }

    private static string GetPluginPath(string modPath)
    {
        if (Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor)
        {
            return Path.Combine(modPath, "Plugins", "Windows", "x86_64", "webrtc.dll");
        }

        if (Application.platform == RuntimePlatform.LinuxPlayer || Application.platform == RuntimePlatform.LinuxEditor)
        {
            return Path.Combine(modPath, "Plugins", "Linux", "x86_64", "libwebrtc.so");
        }

        return "";
    }

    [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadLibrary(string lpFileName);
}
