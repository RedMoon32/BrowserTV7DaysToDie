using System;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;
using UnityEngine.Scripting;

[Preserve]
public class XUiC_InputWindow : XUiController
{
	private XUiC_TextInput textInput;

	private bool handlersRegistered;

	public TileEntityYouTubeTV TileEntity { get; set; }

	public static string EnteredUrl { get; private set; }

	public event Action<string> OnUrlEntered;

	public override void Init()
	{
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		base.Init();
		textInput = ((XUiController)this).GetChildByType<XUiC_TextInput>();
		if (textInput != null)
		{
			RegisterTextInputHandlers();
			textInput.UIInput.validation = (UIInput.Validation)0;
			textInput.characterLimit = 200;
		}
		else
		{
			Debug.LogError((object)"XUiC_InputWindow: Could not find XUiC_TextInput child.");
		}
	}

	private void RegisterTextInputHandlers()
	{
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Expected O, but got Unknown
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Expected O, but got Unknown
		if (textInput != null && !handlersRegistered)
		{
			textInput.OnSubmitHandler += new XUiEvent_InputOnSubmitEventHandler(TextInput_OnSubmitHandler);
			textInput.OnChangeHandler += new XUiEvent_InputOnChangedEventHandler(TextInput_OnChangeHandler);
			handlersRegistered = true;
		}
	}

	private void TextInput_OnSubmitHandler(XUiController _sender, string _text)
	{
		EnteredUrl = _text;
		Debug.Log((object)("YouTube URL Submitted: " + EnteredUrl));
		this.OnUrlEntered?.Invoke(EnteredUrl);
		((XUiController)this).xui.playerUI.windowManager.Close(((XUiController)this).WindowGroup.ID);
	}

	private void TextInput_OnChangeHandler(XUiController _sender, string _text, bool _fromCode)
	{
	}

	public override void OnOpen()
	{
		base.OnOpen();
		if (textInput != null)
		{
			RegisterTextInputHandlers();
			textInput.Text = ((TileEntity != null) ? TileEntity.CurrentURL : "");
			textInput.SetSelected(true, true);
		}
	}

	public override void OnClose()
	{
		base.OnClose();
		if (textInput != null)
		{
			textInput.SetSelected(false, false);
		}
		this.OnUrlEntered = null;
		TileEntity = null;
	}

	public static string GetLastEnteredUrl()
	{
		return EnteredUrl;
	}

	public static void ClearLastEnteredUrl()
	{
		EnteredUrl = null;
	}
}
