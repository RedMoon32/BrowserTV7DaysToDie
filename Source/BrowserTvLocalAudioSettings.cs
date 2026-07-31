using UnityEngine;

public static class BrowserTvLocalAudioSettings
{
    private const string PlayerPrefsKey = "BrowserTV.LocalVolume";
    private const float DefaultVolume = 1f;

    private static bool loaded;
    private static bool dirty;
    private static float volume = DefaultVolume;

    public static float Volume
    {
        get
        {
            Load();
            return volume;
        }
    }

    public static void Load()
    {
        if (loaded)
        {
            return;
        }

        volume = Mathf.Clamp01(PlayerPrefs.GetFloat(PlayerPrefsKey, DefaultVolume));
        loaded = true;
        dirty = false;
        Debug.Log("[BrowserTV] Local player volume loaded: " + Mathf.RoundToInt(volume * 100f) + "%");
    }

    public static void SetVolume(float value)
    {
        Load();
        float clamped = Mathf.Clamp01(value);
        if (Mathf.Approximately(volume, clamped))
        {
            return;
        }

        volume = clamped;
        dirty = true;
    }

    public static void Save()
    {
        Load();
        if (!dirty)
        {
            return;
        }

        PlayerPrefs.SetFloat(PlayerPrefsKey, volume);
        PlayerPrefs.Save();
        dirty = false;
        Debug.Log("[BrowserTV] Local player volume saved: " + Mathf.RoundToInt(volume * 100f) + "%");
    }
}
