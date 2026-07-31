using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Scripting;

[Preserve]
public class XUiC_BrowserTvControlWindow : XUiController
{
    private readonly HashSet<string> registeredButtonIds = new HashSet<string>();

    private XUiController panelUrl;
    private XUiController panelVolume;
    private XUiC_TextInput urlInput;
    private XUiC_Slider volumeSlider;
    private bool urlInputHandlersRegistered;
    private bool volumeSliderHandlerRegistered;

    public static Vector3i BlockPos { get; set; }
    public static string CurrentUrl { get; set; }
    public static EntityPlayerLocal Player { get; set; }
    public static Action<string> OnUrlEntered { get; set; }

    public override void OnOpen()
    {
        base.OnOpen();
        BindControls();
        RegisterHandlers();

        if (urlInput != null)
        {
            urlInput.Text = CurrentUrl ?? string.Empty;
        }

        ConfigureVolumeSlider();
        ShowUrlPanel();
    }

    public override void OnClose()
    {
        SetSelected(urlInput, false);
        BrowserTvLocalAudioSettings.Save();
        base.OnClose();
    }

    private void BindControls()
    {
        panelUrl = GetChildById("panelUrl");
        panelVolume = GetChildById("panelVolume");
        urlInput = GetChildById("txtBrowserTvUrl") as XUiC_TextInput;
        volumeSlider = GetChildById("browserTvVolumeSlider") as XUiC_Slider;

        if (urlInput != null)
        {
            urlInput.UIInput.validation = 0;
            urlInput.characterLimit = 512;
        }
    }

    private void ConfigureVolumeSlider()
    {
        if (volumeSlider == null)
        {
            Debug.LogError("[BrowserTV] Control window has no volume slider.");
            return;
        }

        volumeSlider.Label = "Volume";
        volumeSlider.Step = 0.01f;
        volumeSlider.ValueFormatter = value => Mathf.RoundToInt(value * 100f) + "%";
        volumeSlider.Value = BrowserTvLocalAudioSettings.Volume;
    }

    private void RegisterHandlers()
    {
        RegisterButton("btnBrowserTvVolume", ShowVolumePanel);
        RegisterButton("btnBrowserTvClose", CloseWindow);
        RegisterButton("btnBrowserTvUrlSubmit", SubmitUrl);
        RegisterButton("btnBrowserTvVolumeBack", ShowUrlPanel);

        if (!urlInputHandlersRegistered && urlInput != null)
        {
            urlInput.OnSubmitHandler += UrlInput_OnSubmitHandler;
            urlInput.OnInputAbortedHandler += UrlInput_OnInputAbortedHandler;
            urlInputHandlersRegistered = true;
        }

        if (!volumeSliderHandlerRegistered && volumeSlider != null)
        {
            volumeSlider.OnValueChanged += VolumeSlider_OnValueChanged;
            volumeSliderHandlerRegistered = true;
        }
    }

    private void RegisterButton(string id, Action action)
    {
        if (registeredButtonIds.Contains(id))
        {
            return;
        }

        XUiController button = GetChildById(id);
        if (button == null)
        {
            Debug.LogWarning("[BrowserTV] UI button missing: " + id);
            return;
        }

        XUiController clickable = button.GetChildById("clickable");
        int lastPressFrame = -1;
        XUiEvent_OnPressEventHandler handler = (_, __) =>
        {
            if (lastPressFrame == Time.frameCount)
            {
                return;
            }

            lastPressFrame = Time.frameCount;
            action();
        };

        button.OnPress += handler;
        if (clickable != null && clickable != button)
        {
            clickable.OnPress += handler;
        }

        registeredButtonIds.Add(id);
    }

    private void ShowUrlPanel()
    {
        SetVisible(panelUrl, true);
        SetVisible(panelVolume, false);
        if (urlInput != null)
        {
            urlInput.Text = CurrentUrl ?? string.Empty;
            SetSelected(urlInput, true);
        }
    }

    private void ShowVolumePanel()
    {
        SetVisible(panelUrl, false);
        SetVisible(panelVolume, true);
        SetSelected(urlInput, false);
        if (volumeSlider != null)
        {
            volumeSlider.Value = BrowserTvLocalAudioSettings.Volume;
            volumeSlider.IsDirty = true;
        }
    }

    private void SubmitUrl()
    {
        string url = NormalizeUrl(urlInput?.Text);
        CurrentUrl = url;
        OnUrlEntered?.Invoke(url);
        CloseWindow();
    }

    private void UrlInput_OnSubmitHandler(XUiController sender, string text)
    {
        SubmitUrl();
    }

    private void UrlInput_OnInputAbortedHandler(XUiController sender)
    {
        CloseWindow();
    }

    private void VolumeSlider_OnValueChanged(XUiC_Slider slider)
    {
        BrowserTvLocalAudioSettings.SetVolume(slider.Value);
        BrowserTvWebRtcViewerHost.Ensure().RefreshLocalVolume();
    }

    private void CloseWindow()
    {
        xui?.playerUI?.windowManager?.Close(WindowGroup.ID);
    }

    private static void SetSelected(XUiC_TextInput input, bool selected)
    {
        input?.SetSelected(selected, false);
    }

    private static void SetVisible(XUiController controller, bool visible)
    {
        if (controller == null)
        {
            return;
        }

        if (!TrySetBoolProperty(controller, "Visible", visible) &&
            !TrySetBoolProperty(controller, "IsVisible", visible))
        {
            object viewComponent = GetPropertyValue(controller, "ViewComponent");
            TrySetBoolProperty(viewComponent, "Visible", visible);
            TrySetBoolProperty(viewComponent, "IsVisible", visible);
        }

        controller.IsDirty = true;
    }

    private static object GetPropertyValue(object target, string propertyName)
    {
        if (target == null)
        {
            return null;
        }

        try
        {
            return target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(target, null);
        }
        catch
        {
            return null;
        }
    }

    private static bool TrySetBoolProperty(object target, string propertyName, bool value)
    {
        if (target == null)
        {
            return false;
        }

        try
        {
            PropertyInfo property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property == null || !property.CanWrite || property.PropertyType != typeof(bool))
            {
                return false;
            }

            property.SetValue(target, value, null);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string NormalizeUrl(string value)
    {
        string url = (value ?? string.Empty).Trim();
        if (url.Length == 0 || url.Contains("://"))
        {
            return url;
        }

        return "https://" + url;
    }
}
