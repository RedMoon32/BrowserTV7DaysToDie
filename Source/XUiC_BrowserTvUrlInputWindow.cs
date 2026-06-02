using System;
using UnityEngine;
using UnityEngine.Scripting;

[Preserve]
public class XUiC_BrowserTvUrlInputWindow : XUiController
{
    private XUiC_TextInput textInput;
    private bool handlersRegistered;

    public static Vector3i BlockPos { get; set; }
    public static string CurrentUrl { get; set; }
    public static Action<string> OnUrlEntered { get; set; }

    public override void Init()
    {
        base.Init();
        textInput = GetTextInput();
        if (textInput == null)
        {
            Debug.LogError("[BrowserTV] URL input window has no XUiC_TextInput child.");
            return;
        }

        RegisterHandlers();
        textInput.UIInput.validation = 0;
        textInput.characterLimit = 512;
    }

    public override void OnOpen()
    {
        base.OnOpen();
        if (textInput == null)
        {
            textInput = GetTextInput();
        }

        if (textInput == null)
        {
            return;
        }

        RegisterHandlers();
        textInput.Text = CurrentUrl ?? "";
        textInput.SetSelected(true, true);
    }

    public override void OnClose()
    {
        base.OnClose();
        if (textInput != null)
        {
            textInput.SetSelected(false, false);
        }
    }

    private void RegisterHandlers()
    {
        if (textInput == null || handlersRegistered)
        {
            return;
        }

        textInput.OnSubmitHandler += TextInput_OnSubmitHandler;
        textInput.OnInputAbortedHandler += TextInput_OnInputAbortedHandler;
        handlersRegistered = true;
    }

    private XUiC_TextInput GetTextInput()
    {
        XUiController byContent = GetChildById("content");
        if (byContent is XUiC_TextInput contentInput)
        {
            return contentInput;
        }

        XUiController byOldId = GetChildById("textInputBrowserTvUrl");
        if (byOldId is XUiC_TextInput oldInput)
        {
            return oldInput;
        }

        return GetChildByType<XUiC_TextInput>();
    }

    private void TextInput_OnSubmitHandler(XUiController sender, string text)
    {
        string url = NormalizeUrl(text);
        Debug.Log("[BrowserTV] URL submitted: " + url);
        OnUrlEntered?.Invoke(url);
        xui.playerUI.windowManager.Close(WindowGroup.ID);
    }

    private void TextInput_OnInputAbortedHandler(XUiController sender)
    {
        xui.playerUI.windowManager.Close(WindowGroup.ID);
    }

    private static string NormalizeUrl(string value)
    {
        string url = (value ?? "").Trim();
        if (url.Length == 0 || url.Contains("://"))
        {
            return url;
        }

        return "https://" + url;
    }
}
