using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;
using UnityEngine.Video;

public class YouTubeTVInitializer : MonoBehaviour
{
	[CompilerGenerated]
	private sealed class _003CDelayedInitialize_003Ed__4 : IEnumerator<object>, IDisposable, IEnumerator
	{
		private int _003C_003E1__state;

		private object _003C_003E2__current;

		public YouTubeTVInitializer _003C_003E4__this;

		object IEnumerator<object>.Current
		{
			[DebuggerHidden]
			get
			{
				return _003C_003E2__current;
			}
		}

		object IEnumerator.Current
		{
			[DebuggerHidden]
			get
			{
				return _003C_003E2__current;
			}
		}

		[DebuggerHidden]
		public _003CDelayedInitialize_003Ed__4(int _003C_003E1__state)
		{
			this._003C_003E1__state = _003C_003E1__state;
		}

		[DebuggerHidden]
		void IDisposable.Dispose()
		{
			_003C_003E1__state = -2;
		}

		private bool MoveNext()
		{
			int num = _003C_003E1__state;
			YouTubeTVInitializer youTubeTVInitializer = _003C_003E4__this;
			switch (num)
			{
			default:
				return false;
			case 0:
				_003C_003E1__state = -1;
				_003C_003E2__current = null;
				_003C_003E1__state = 1;
				return true;
			case 1:
				_003C_003E1__state = -1;
				_003C_003E2__current = null;
				_003C_003E1__state = 2;
				return true;
			case 2:
				_003C_003E1__state = -1;
				if (!youTubeTVInitializer.isInitialized)
				{
					Debug.Log((object)("YouTubeTVInitializer.DelayedInitialize: Calling Initialize() for " + ((Object)((Component)youTubeTVInitializer).gameObject).name + " after delay."));
					Log.Warning("YouTubeTVInitializer.DelayedInitialize: Initialize() call skipped as it now requires a TileEntityYouTubeTV reference which is not available here.");
				}
				youTubeTVInitializer.delayedInitializeCoroutine = null;
				return false;
			}
		}

		bool IEnumerator.MoveNext()
		{
			//ILSpy generated this explicit interface implementation from .override directive in MoveNext
			return this.MoveNext();
		}

		[DebuggerHidden]
		void IEnumerator.Reset()
		{
			throw new NotSupportedException();
		}
	}

	private bool isInitialized;

	private YouTubeTVController controller;

	private Coroutine delayedInitializeCoroutine;

	private void Awake()
	{
		if (!isInitialized)
		{
			_ = delayedInitializeCoroutine;
		}
	}

	[IteratorStateMachine(typeof(_003CDelayedInitialize_003Ed__4))]
	private IEnumerator DelayedInitialize()
	{
		//yield-return decompiler failed: Unexpected instruction in Iterator.Dispose()
		return new _003CDelayedInitialize_003Ed__4(0)
		{
			_003C_003E4__this = this
		};
	}

	public void Initialize(TileEntityYouTubeTV parentTileEntity)
	{
		//IL_0351: Unknown result type (might be due to invalid IL or missing references)
		//IL_0356: Unknown result type (might be due to invalid IL or missing references)
		//IL_035d: Unknown result type (might be due to invalid IL or missing references)
		//IL_036f: Unknown result type (might be due to invalid IL or missing references)
		if (isInitialized && (Object)(object)controller != (Object)null)
		{
			Debug.Log((object)("YouTubeTVInitializer.Initialize: Already initialized for " + ((Object)((Component)this).gameObject).name + ". Controller " + (((Object)(object)controller != (Object)null) ? "exists" : "is null") + "."));
			if (!((Object)(object)controller != (Object)null))
			{
				return;
			}
			Transform val = FindScreenObject();
			if ((Object)(object)val != (Object)null)
			{
				Renderer component = ((Component)val).GetComponent<Renderer>();
				if ((Object)(object)component != (Object)null)
				{
					controller.Initialize(component, forceReinit: true);
				}
			}
			return;
		}
		Debug.Log((object)$"YouTubeTVInitializer.Initialize: Starting initialization for {((Object)((Component)this).gameObject).name}. Current isInitialized: {isInitialized}");
		try
		{
			Transform val2 = FindScreenObject();
			if ((Object)(object)val2 == (Object)null)
			{
				Log.Error("YouTubeTVInitializer.Initialize: Could not find designated screen object within prefab " + ((Object)((Component)this).gameObject).name + ". TV cannot function.");
				isInitialized = false;
				return;
			}
			Debug.Log((object)("YouTubeTVInitializer.Initialize: Found screen object: " + ((Object)val2).name + ". Proceeding with cleanup and setup."));
			Component[] components = ((Component)val2).GetComponents<Component>();
			foreach (Component val3 in components)
			{
				if ((Object)(object)val3 == (Object)null)
				{
					Log.Warning("YouTubeTVInitializer.Initialize: Found and skipping a null (missing) script reference on screen object " + ((Object)val2).name + ". This indicates a prefab issue.");
					continue;
				}
				string fullName = ((object)val3).GetType().FullName;
				if (fullName.Contains("YouTubeTVController") || fullName.Contains("YouTubeTVController"))
				{
					Log.Warning("YouTubeTVInitializer.Initialize: Removing existing/broken TV controller component '" + fullName + "' from screen object " + ((Object)val2).name + " before adding a new one.");
					Object.DestroyImmediate((Object)(object)val3);
				}
			}
			if ((Object)(object)((Component)val2).gameObject.GetComponent<VideoPlayer>() == (Object)null)
			{
				((Component)val2).gameObject.AddComponent<VideoPlayer>();
				Debug.Log((object)("YouTubeTVInitializer.Initialize: Added VideoPlayer component to " + ((Object)val2).name + "."));
			}
			if ((Object)(object)((Component)val2).gameObject.GetComponent<AudioSource>() == (Object)null)
			{
				((Component)val2).gameObject.AddComponent<AudioSource>();
				Debug.Log((object)("YouTubeTVInitializer.Initialize: Added AudioSource component to " + ((Object)val2).name + "."));
			}
			controller = ((Component)val2).gameObject.GetComponent<YouTubeTVController>();
			if ((Object)(object)controller == (Object)null)
			{
				Debug.Log((object)("YouTubeTVInitializer.Initialize: Adding YouTubeTVController to screen object " + ((Object)val2).name + "."));
				controller = ((Component)val2).gameObject.AddComponent<YouTubeTVController>();
				if ((Object)(object)controller == (Object)null)
				{
					Log.Error("YouTubeTVInitializer.Initialize: FAILED to add YouTubeTVController to " + ((Object)val2).name + "! The TV will not function.");
					isInitialized = false;
					return;
				}
			}
			else
			{
				Debug.Log((object)("YouTubeTVInitializer.Initialize: Found an existing YouTubeTVController on " + ((Object)val2).name + " (unexpected after cleanup, but proceeding)."));
			}
			Renderer component2 = ((Component)val2).GetComponent<Renderer>();
			if ((Object)(object)component2 == (Object)null)
			{
				Log.Error("YouTubeTVInitializer.Initialize: No Renderer component found on screen object " + ((Object)val2).name + ". YouTubeTVController needs a renderer to display video.");
				isInitialized = false;
				return;
			}
			controller.Initialize(component2, forceReinit: true);
			if (parentTileEntity != null)
			{
				controller.ParentTileEntity = parentTileEntity;
				Debug.Log((object)("YouTubeTVInitializer.Initialize: ParentTileEntity set on YouTubeTVController for " + ((Object)val2).name + "."));
				Vector3i val4 = ((TileEntity)parentTileEntity).ToWorldPos();
				YouTubeTVManager.Instance.RegisterTV(val4, controller);
				Debug.Log((object)$"YouTubeTVInitializer.Initialize: Registered TV with manager at block position {val4}");
			}
			else
			{
				Log.Warning("YouTubeTVInitializer.Initialize: parentTileEntity was null. ParentTileEntity not set on controller for " + ((Object)val2).name + ".");
			}
			Debug.Log((object)("YouTubeTVInitializer.Initialize: YouTubeTVController on " + ((Object)val2).name + " has been configured with its renderer."));
			isInitialized = true;
			Debug.Log((object)("YouTubeTVInitializer.Initialize: Successfully completed for " + ((Object)((Component)this).gameObject).name + ". TV screen: " + ((Object)val2).name));
		}
		catch (Exception ex)
		{
			Log.Error("YouTubeTVInitializer.Initialize: CRITICAL EXCEPTION during initialization for " + ((Object)((Component)this).gameObject).name + ": " + ex.Message + "\n" + ex.StackTrace);
			isInitialized = false;
		}
	}

	private Transform FindScreenObject()
	{
		string[] array = new string[6] { "FilePlane", "Screen", "TVScreen", "DisplayPanel", "VideoScreen", "MonitorScreen" };
		string[] array2 = array;
		foreach (string text in array2)
		{
			Transform val = FindChildRecursive(((Component)this).transform, text);
			if ((Object)(object)val != (Object)null)
			{
				Debug.Log((object)("YouTubeTVInitializer.FindScreenObject: Found screen object by name: '" + ((Object)val).name + "' (searched for '" + text + "')."));
				return val;
			}
		}
		MeshRenderer componentInChildren = ((Component)this).GetComponentInChildren<MeshRenderer>();
		if ((Object)(object)componentInChildren != (Object)null && (Object)(object)((Component)componentInChildren).transform != (Object)(object)((Component)this).transform)
		{
			Log.Warning("YouTubeTVInitializer.FindScreenObject: No specifically named screen found. Using fallback: first child with MeshRenderer: " + ((Object)((Component)componentInChildren).gameObject).name + ".");
			return ((Component)componentInChildren).transform;
		}
		Log.Error("YouTubeTVInitializer.FindScreenObject: Could not find a suitable screen object within " + ((Object)((Component)this).gameObject).name + ". Searched names: " + string.Join(", ", array) + ", and fallback MeshRenderer.");
		return null;
	}

	private Transform FindChildRecursive(Transform parent, string targetName)
	{
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Expected O, but got Unknown
		if (((Object)parent).name.Equals(targetName, StringComparison.OrdinalIgnoreCase) || ((Object)parent).name.Contains(targetName))
		{
			return parent;
		}
		foreach (Transform item in parent)
		{
			Transform parent2 = item;
			Transform val = FindChildRecursive(parent2, targetName);
			if ((Object)(object)val != (Object)null)
			{
				return val;
			}
		}
		return null;
	}

	public YouTubeTVController GetController()
	{
		if (!isInitialized || (Object)(object)controller == (Object)null)
		{
			Log.Warning($"YouTubeTVInitializer.GetController: Controller not ready for {((Object)((Component)this).gameObject).name}. isInitialized: {isInitialized}, controller is null: {(Object)(object)controller == (Object)null}. Initialize requires a TileEntity and cannot be called from here directly without one.");
		}
		return controller;
	}

	private void OnDestroy()
	{
		if (delayedInitializeCoroutine != null)
		{
			((MonoBehaviour)this).StopCoroutine(delayedInitializeCoroutine);
			delayedInitializeCoroutine = null;
		}
	}
}
