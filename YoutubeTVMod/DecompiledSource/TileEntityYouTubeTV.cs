using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;
using YoutubeTVMod;

public class TileEntityYouTubeTV : TileEntityPowered
{
	[CompilerGenerated]
	private sealed class _003CPauseAfterPrepareCoroutine_003Ed__32 : IEnumerator<object>, IDisposable, IEnumerator
	{
		private int _003C_003E1__state;

		private object _003C_003E2__current;

		public YouTubeTVController controller;

		public string expectedUrl;

		public double targetTime;

		private float _003CtimeoutStartTime_003E5__2;

		private float _003CpreparationTimeout_003E5__3;

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
		public _003CPauseAfterPrepareCoroutine_003Ed__32(int _003C_003E1__state)
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
			if (num != 0)
			{
				if (num != 1)
				{
					return false;
				}
				_003C_003E1__state = -1;
			}
			else
			{
				_003C_003E1__state = -1;
				if ((Object)(object)controller == (Object)null || (Object)(object)controller.videoPlayer == (Object)null)
				{
					Log.Warning("[YouTubeTV] PauseAfterPrepareCoroutine: Controller or videoPlayer is null. Cannot proceed for URL '" + expectedUrl + "'.");
					return false;
				}
				Debug.Log((object)$"[YouTubeTV] PauseAfterPrepareCoroutine: Started for URL '{expectedUrl}', targetTime {targetTime}. Waiting for preparation...");
				_003CtimeoutStartTime_003E5__2 = Time.time;
				_003CpreparationTimeout_003E5__3 = 20f;
			}
			if (Time.time > _003CtimeoutStartTime_003E5__2 + _003CpreparationTimeout_003E5__3)
			{
				Log.Warning("[YouTubeTV] PauseAfterPrepareCoroutine: Timeout waiting for video '" + expectedUrl + "' (current: '" + controller.videoPlayer.url + "') to prepare. Aborting corrective pause.");
				return false;
			}
			if (controller.videoPlayer.isPrepared)
			{
				string url = controller.videoPlayer.url;
				if (url == expectedUrl)
				{
					Debug.Log((object)("[YouTubeTV] PauseAfterPrepareCoroutine: Video '" + expectedUrl + "' is prepared."));
					if (controller.videoPlayer.isPlaying)
					{
						controller.PauseVideoAtTime(targetTime);
						Debug.Log((object)$"[YouTubeTV] PauseAfterPrepareCoroutine: Correctively paused video '{expectedUrl}' at {targetTime} as synced state was 'not playing'.");
					}
					else
					{
						Debug.Log((object)$"[YouTubeTV] PauseAfterPrepareCoroutine: Video '{expectedUrl}' prepared but was not playing. No corrective pause needed. Current player time: {controller.videoPlayer.time}");
						if (Math.Abs(controller.videoPlayer.time - targetTime) > 0.5 && controller.videoPlayer.canSetTime)
						{
							Debug.Log((object)$"[YouTubeTV] PauseAfterPrepareCoroutine: Adjusting time for already paused video from {controller.videoPlayer.time} to {targetTime}");
							controller.videoPlayer.time = targetTime;
						}
					}
					return false;
				}
				Log.Warning("[YouTubeTV] PauseAfterPrepareCoroutine: Video player prepared, but with unexpected URL. Expected: '" + expectedUrl + "', Got: '" + url + "'. Aborting corrective pause for original URL.");
				return false;
			}
			_003C_003E2__current = null;
			_003C_003E1__state = 1;
			return true;
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

	private static readonly HashSet<string> _allowedVideoExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".mp4", ".mkv", ".webm", ".avi", ".mov", ".flv", ".wmv", ".mpeg", ".mpg" };

	public const int CustomTileEntityTypeId = 250;

	private const int YOUTUBE_TV_VERSION = 3;

	public YouTubeTVController youtubeController;

	private string currentYouTubeURL = "";

	private YTConfig ytConfig;

	private ChunkManager.ChunkObserver tvChunkObserver;

	private bool isVideoPlaying;

	private double currentVideoTime;

	private bool isVideoLooping;

	private float lastServerTimeUpdate;

	private float currentVolume = 0.5f;

	public string CurrentURL => currentYouTubeURL;

	public TileEntityYouTubeTV(Chunk _chunk)
		: base(_chunk)
	{
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		base.PowerItemType = (PowerItem.PowerItemTypes)1;
		((TileEntityPowered)this).InitializePowerData();
		ytConfig = new YTConfig();
	}

	public void RequestSetYouTubeURL(string url, PlatformUserIdentifierAbs userID, int entityId)
	{
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_008c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		Debug.Log((object)string.Format("TileEntityYouTubeTV.RequestSetYouTubeURL: Client requesting to set URL: '{0}' for TV at {1}. User: {2}, EntityID: {3}", url ?? "null", ((TileEntity)this).localChunkPos, ((userID != null) ? userID.CombinedString : null) ?? "Unknown", entityId));
		if (!SingletonMonoBehaviour<ConnectionManager>.Instance.IsClient)
		{
			Debug.Log((object)$"TileEntityYouTubeTV.RequestSetYouTubeURL: Running on server. Directly calling ServerSetYouTubeURL for TV at {((TileEntity)this).localChunkPos}.");
			ServerSetYouTubeURL(url, userID, entityId);
		}
		else
		{
			SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer((NetPackage)(object)new NetPackageSetYouTubeURLServer().Setup(((TileEntity)this).ToWorldPos(), url, userID, entityId), false);
		}
	}

	public void ServerSetYouTubeURL(string url, PlatformUserIdentifierAbs userID, int entityId)
	{
		//IL_02b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_01fc: Unknown result type (might be due to invalid IL or missing references)
		//IL_0222: Unknown result type (might be due to invalid IL or missing references)
		Debug.Log((object)string.Format("TileEntityYouTubeTV.ServerSetYouTubeURL: Server processing URL: '{0}' from user: {1} at {2}", url ?? "null", ((userID != null) ? userID.CombinedString : null) ?? "Unknown", ((TileEntity)this).localChunkPos));
		try
		{
			string text = currentYouTubeURL;
			if (IsValidVideoURL(url))
			{
				Debug.Log((object)("TileEntityYouTubeTV.ServerSetYouTubeURL: Valid YouTube URL detected: " + url));
				currentYouTubeURL = url;
			}
			else
			{
				if (!string.IsNullOrEmpty(url))
				{
					Log.Warning("TileEntityYouTubeTV.ServerSetYouTubeURL: Invalid YouTube URL entered: " + url + ". Current URL (" + currentYouTubeURL + ") remains unchanged.");
					return;
				}
				Debug.Log((object)"TileEntityYouTubeTV.ServerSetYouTubeURL: Empty URL received. Clearing current URL.");
				currentYouTubeURL = "";
			}
			bool flag = currentYouTubeURL != text;
			bool flag2 = isVideoPlaying;
			double num = currentVideoTime;
			if (((TileEntityPowered)this).IsPowered && IsValidVideoURL(currentYouTubeURL))
			{
				Debug.Log((object)("[YouTubeTVMod] ServerSetYouTubeURL: TV is Powered and URL '" + currentYouTubeURL + "' is valid. Setting to PLAY."));
				isVideoPlaying = true;
				currentVideoTime = 0.0;
			}
			else
			{
				Debug.Log((object)("[YouTubeTVMod] ServerSetYouTubeURL: TV is OFF or URL '" + currentYouTubeURL + "' is invalid/empty. Setting to PAUSED/STOPPED."));
				isVideoPlaying = false;
				if (flag)
				{
					currentVideoTime = 0.0;
				}
			}
			bool flag3 = isVideoPlaying != flag2 || currentVideoTime != num;
			if (flag || flag3)
			{
				((TileEntity)this).SetModified();
				Debug.Log((object)$"[YouTubeTVMod] ServerSetYouTubeURL: SetModified called. URL changed: {flag}, Playback state changed: {flag3}. New URL: '{currentYouTubeURL}', IsPlaying: {isVideoPlaying}, Time: {currentVideoTime}");
			}
			else
			{
				Debug.Log((object)$"[YouTubeTVMod] ServerSetYouTubeURL: No change to URL content or essential playback state. URL: '{currentYouTubeURL}', IsPlaying: {isVideoPlaying}, Time: {currentVideoTime}");
			}
			SingletonMonoBehaviour<ConnectionManager>.Instance.SendToClientsOrServer((NetPackage)(object)new NetPackageSetYouTubeURLClient().Setup(((TileEntity)this).ToWorldPos(), currentYouTubeURL, ((TileEntityPowered)this).IsPowered));
			SingletonMonoBehaviour<ConnectionManager>.Instance.SendToClientsOrServer((NetPackage)(object)new NetPackageControlPlaybackClient().Setup(((TileEntity)this).ToWorldPos(), (!isVideoPlaying) ? PlaybackCommand.Pause : PlaybackCommand.Play, currentYouTubeURL, isVideoPlaying, currentVideoTime, isVideoLooping));
			if ((Object)(object)youtubeController != (Object)null && !GameManager.IsDedicatedServer)
			{
				ClientUpdateURLAndPower(currentYouTubeURL, ((TileEntityPowered)this).IsPowered);
				ClientControlPlayback((!isVideoPlaying) ? PlaybackCommand.Pause : PlaybackCommand.Play, currentYouTubeURL, isVideoPlaying, currentVideoTime, isVideoLooping);
			}
		}
		catch (Exception ex)
		{
			Log.Error($"Error in ServerSetYouTubeURL for YouTube TV at {((TileEntity)this).localChunkPos}: {ex.Message}\n{ex.StackTrace}");
		}
	}

	public void ClientUpdateURLAndPower(string newUrl, bool isNowPowered)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		Debug.Log((object)string.Format("TileEntityYouTubeTV.ClientUpdateURLAndPower: Client at {0} received URL: '{1}', IsPowered: {2}", ((TileEntity)this).localChunkPos, newUrl ?? "null", isNowPowered));
		currentYouTubeURL = newUrl;
		if ((Object)(object)youtubeController == (Object)null)
		{
			Log.Warning($"TileEntityYouTubeTV.ClientUpdateURLAndPower: YouTube controller not yet initialized for TV at {((TileEntity)this).localChunkPos}. URL set to '{newUrl}'.");
			return;
		}
		youtubeController.StopAllPlayback();
		if (isNowPowered)
		{
			youtubeController.SetScreenBlack(isBlack: false);
			if (IsValidVideoURL(currentYouTubeURL))
			{
				Debug.Log((object)("TileEntityYouTubeTV.ClientUpdateURLAndPower: TV is ON, URL '" + currentYouTubeURL + "' received. Controller will be cued by PlaybackControl packet. Displaying logo/standby."));
				youtubeController.DisplayYouTubeLogo();
			}
			else
			{
				Debug.Log((object)("TileEntityYouTubeTV.ClientUpdateURLAndPower: TV is ON, but URL ('" + currentYouTubeURL + "') is empty or invalid. Displaying logo."));
				youtubeController.DisplayYouTubeLogo();
			}
		}
		else
		{
			Debug.Log((object)"TileEntityYouTubeTV.ClientUpdateURLAndPower: TV is OFF. Setting screen black.");
			youtubeController.SetScreenBlack(isBlack: true);
		}
	}

	public void ServerControlPlayback(PlaybackCommand command, int entityId, double targetTime)
	{
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		//IL_015e: Unknown result type (might be due to invalid IL or missing references)
		//IL_019f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_027e: Unknown result type (might be due to invalid IL or missing references)
		if (!SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer)
		{
			Log.Warning($"TileEntityYouTubeTV.ServerControlPlayback: Attempted to run on client. Aborting. TV at {((TileEntity)this).localChunkPos}");
			return;
		}
		Debug.Log((object)$"TileEntityYouTubeTV.ServerControlPlayback: Server received command '{command}' for TV at {((TileEntity)this).localChunkPos}. TargetTime: {targetTime}");
		bool flag = false;
		switch (command)
		{
		case PlaybackCommand.Play:
			if (!((TileEntityPowered)this).IsPowered)
			{
				Log.Warning($"TileEntityYouTubeTV.ServerControlPlayback: Play command received but TV at {((TileEntity)this).localChunkPos} is not powered.");
			}
			else if (string.IsNullOrEmpty(currentYouTubeURL))
			{
				Log.Warning($"TileEntityYouTubeTV.ServerControlPlayback: Play command received but no URL is set for TV at {((TileEntity)this).localChunkPos}.");
			}
			else if (!isVideoPlaying || currentVideoTime != targetTime)
			{
				isVideoPlaying = true;
				currentVideoTime = targetTime;
				flag = true;
			}
			break;
		case PlaybackCommand.Pause:
			if (isVideoPlaying)
			{
				isVideoPlaying = false;
				currentVideoTime = targetTime;
				flag = true;
			}
			break;
		case PlaybackCommand.Stop:
			if (isVideoPlaying || currentVideoTime > 0.0)
			{
				isVideoPlaying = false;
				currentVideoTime = 0.0;
				flag = true;
			}
			break;
		case PlaybackCommand.Seek:
			if (!((TileEntityPowered)this).IsPowered || string.IsNullOrEmpty(currentYouTubeURL))
			{
				Log.Warning($"TileEntityYouTubeTV.ServerControlPlayback: Seek command received but TV at {((TileEntity)this).localChunkPos} is not powered or no URL.");
			}
			else if (currentVideoTime != targetTime)
			{
				currentVideoTime = targetTime;
				flag = true;
			}
			break;
		case PlaybackCommand.TogglePlayPause:
			if (!((TileEntityPowered)this).IsPowered)
			{
				Log.Warning($"TileEntityYouTubeTV.ServerControlPlayback: TogglePlayPause command received but TV at {((TileEntity)this).localChunkPos} is not powered.");
				break;
			}
			if (string.IsNullOrEmpty(currentYouTubeURL))
			{
				Log.Warning($"TileEntityYouTubeTV.ServerControlPlayback: TogglePlayPause command received but no URL is set for TV at {((TileEntity)this).localChunkPos}.");
				break;
			}
			isVideoPlaying = !isVideoPlaying;
			if (!isVideoPlaying)
			{
				currentVideoTime = targetTime;
			}
			flag = true;
			Debug.Log((object)$"TileEntityYouTubeTV.ServerControlPlayback: Toggled play state. IsPlaying: {isVideoPlaying}, Time: {currentVideoTime}");
			break;
		}
		lastServerTimeUpdate = Time.time;
		if (flag)
		{
			((TileEntity)this).SetModified();
			Debug.Log((object)$"TileEntityYouTubeTV.ServerControlPlayback: State changed. IsPlaying: {isVideoPlaying}, Time: {currentVideoTime}. Broadcasting.");
		}
		else
		{
			Debug.Log((object)$"TileEntityYouTubeTV.ServerControlPlayback: No state change for command '{command}'. Broadcasting current state anyway.");
		}
		SingletonMonoBehaviour<ConnectionManager>.Instance.SendToClientsOrServer((NetPackage)(object)new NetPackageControlPlaybackClient().Setup(((TileEntity)this).ToWorldPos(), command, currentYouTubeURL, isVideoPlaying, currentVideoTime, isVideoLooping));
		if ((Object)(object)youtubeController != (Object)null && !GameManager.IsDedicatedServer)
		{
			ClientControlPlayback(command, currentYouTubeURL, isVideoPlaying, currentVideoTime, isVideoLooping);
		}
	}

	public void ServerSetVolume(float volumeLevel, int entityId)
	{
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d8: Unknown result type (might be due to invalid IL or missing references)
		if (!SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer)
		{
			Log.Warning($"TileEntityYouTubeTV.ServerSetVolume: Attempted to run on client. Aborting. TV at {((TileEntity)this).localChunkPos}");
			return;
		}
		Debug.Log((object)$"TileEntityYouTubeTV.ServerSetVolume: Server received volume change to {volumeLevel:F2} for TV at {((TileEntity)this).localChunkPos}");
		float num = Mathf.Clamp01(volumeLevel);
		if (Mathf.Abs(currentVolume - num) > 0.01f)
		{
			currentVolume = num;
			((TileEntity)this).SetModified();
			Debug.Log((object)$"TileEntityYouTubeTV.ServerSetVolume: Volume changed to {currentVolume:F2}. Broadcasting to clients.");
		}
		else
		{
			Debug.Log((object)$"TileEntityYouTubeTV.ServerSetVolume: Volume unchanged ({currentVolume:F2}). No broadcast needed.");
		}
		if ((Object)(object)youtubeController != (Object)null && !GameManager.IsDedicatedServer)
		{
			youtubeController.SetVolume(currentVolume);
		}
		SingletonMonoBehaviour<ConnectionManager>.Instance.SendToClientsOrServer((NetPackage)(object)new NetPackageSetVolumeClient().Setup(((TileEntity)this).ToWorldPos(), currentVolume));
	}

	public void ClientSetVolume(float volumeLevel)
	{
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0099: Unknown result type (might be due to invalid IL or missing references)
		//IL_007e: Unknown result type (might be due to invalid IL or missing references)
		if (GameManager.IsDedicatedServer)
		{
			Debug.Log((object)$"TileEntityYouTubeTV.ClientSetVolume: Dedicated server received volume update for {((TileEntity)this).localChunkPos}. Ignoring as no controller.");
			return;
		}
		Debug.Log((object)$"TileEntityYouTubeTV.ClientSetVolume: Client at {((TileEntity)this).localChunkPos} received volume update to {volumeLevel:F2}");
		currentVolume = Mathf.Clamp01(volumeLevel);
		if ((Object)(object)youtubeController != (Object)null)
		{
			youtubeController.SetVolume(currentVolume);
			Debug.Log((object)$"TileEntityYouTubeTV.ClientSetVolume: Applied volume {currentVolume:F2} to controller at {((TileEntity)this).localChunkPos}");
		}
		else
		{
			Log.Warning($"TileEntityYouTubeTV.ClientSetVolume: YouTube controller is null for TV at {((TileEntity)this).localChunkPos}. Volume not applied.");
		}
	}

	public void ClientControlPlayback(PlaybackCommand command, string url, bool isPlayingStatusFromServer, double serverVideoTime, bool isLoopingStatusFromServer)
	{
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
		if (GameManager.IsDedicatedServer)
		{
			Debug.Log((object)$"TileEntityYouTubeTV.ClientControlPlayback: Dedicated server received playback control for {((TileEntity)this).localChunkPos}. Ignoring as no controller.");
			return;
		}
		Debug.Log((object)$"TileEntityYouTubeTV.ClientControlPlayback: Client at {((TileEntity)this).localChunkPos} received command '{command}', URL '{url}', IsPlaying: {isPlayingStatusFromServer}, ServerTime: {serverVideoTime}");
		currentYouTubeURL = url;
		isVideoPlaying = isPlayingStatusFromServer;
		currentVideoTime = serverVideoTime;
		isVideoLooping = isLoopingStatusFromServer;
		lastServerTimeUpdate = Time.time;
		if ((Object)(object)youtubeController == (Object)null)
		{
			Log.Warning($"TileEntityYouTubeTV.ClientControlPlayback: YouTube controller is null for TV at {((TileEntity)this).localChunkPos}. Cannot execute command '{command}'.");
			return;
		}
		switch (command)
		{
		case PlaybackCommand.Play:
			if (((TileEntityPowered)this).IsPowered && !string.IsNullOrEmpty(currentYouTubeURL))
			{
				youtubeController.PlayVideoAtTime(currentYouTubeURL, currentVideoTime, isVideoLooping);
			}
			else if (!((TileEntityPowered)this).IsPowered)
			{
				youtubeController.StopAllPlayback();
				youtubeController.SetScreenBlack(isBlack: true);
			}
			else
			{
				youtubeController.DisplayYouTubeLogo();
			}
			break;
		case PlaybackCommand.Pause:
			if (((TileEntityPowered)this).IsPowered && !string.IsNullOrEmpty(currentYouTubeURL))
			{
				youtubeController.PauseVideoAtTime(currentVideoTime);
			}
			else if (!((TileEntityPowered)this).IsPowered)
			{
				youtubeController.StopAllPlayback();
				youtubeController.SetScreenBlack(isBlack: true);
			}
			break;
		case PlaybackCommand.Stop:
			youtubeController.StopAllPlayback();
			if (((TileEntityPowered)this).IsPowered)
			{
				youtubeController.DisplayYouTubeLogo();
			}
			else
			{
				youtubeController.SetScreenBlack(isBlack: true);
			}
			break;
		case PlaybackCommand.Seek:
			if (((TileEntityPowered)this).IsPowered && !string.IsNullOrEmpty(currentYouTubeURL))
			{
				youtubeController.SeekVideoToTime(currentVideoTime, isVideoPlaying);
			}
			break;
		}
	}

	public void SetBlockEntityData(BlockEntityData _blockEntityData)
	{
		//IL_0476: Unknown result type (might be due to invalid IL or missing references)
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_04a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_0098: Unknown result type (might be due to invalid IL or missing references)
		//IL_0112: Unknown result type (might be due to invalid IL or missing references)
		//IL_0146: Unknown result type (might be due to invalid IL or missing references)
		//IL_03d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_0184: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_0214: Unknown result type (might be due to invalid IL or missing references)
		//IL_023a: Unknown result type (might be due to invalid IL or missing references)
		//IL_040e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0438: Unknown result type (might be due to invalid IL or missing references)
		//IL_043d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0446: Unknown result type (might be due to invalid IL or missing references)
		//IL_045d: Unknown result type (might be due to invalid IL or missing references)
		if (_blockEntityData == null)
		{
			Log.Error($"TileEntityYouTubeTV.SetBlockEntityData: Received null BlockEntityData for TE at {((TileEntity)this).localChunkPos}.");
			return;
		}
		if ((Object)(object)((TileEntityPowered)this).BlockTransform == (Object)null && !GameManager.IsDedicatedServer)
		{
			Log.Error($"TileEntityYouTubeTV.SetBlockEntityData: BlockTransform is null for TE at {((TileEntity)this).localChunkPos} (client). TV setup aborted.");
			return;
		}
		Debug.Log((object)$"TileEntityYouTubeTV.SetBlockEntityData: Processing BlockEntityData for TE at {((TileEntity)this).localChunkPos}. BlockTransform is set by BlockYouTubeTV.");
		if (_blockEntityData.bHasTransform && !GameManager.IsDedicatedServer)
		{
			try
			{
				if ((Object)(object)youtubeController != (Object)null && !GameManager.IsDedicatedServer)
				{
					Debug.Log((object)$"TileEntityYouTubeTV.SetBlockEntityData: Pre-emptively stopping and nullifying existing youtubeController instance for TE at {((TileEntity)this).localChunkPos} before re-initialization.");
					youtubeController.StopVideo();
					youtubeController = null;
				}
				if ((Object)(object)((TileEntityPowered)this).BlockTransform == (Object)null)
				{
					Log.Error($"TileEntityYouTubeTV.SetBlockEntityData: BlockTransform is unexpectedly null after initial checks for TE at {((TileEntity)this).localChunkPos}. Aborting TV specific setup.");
				}
				else
				{
					CleanupBrokenScripts(((TileEntityPowered)this).BlockTransform);
					YouTubeTVInitializer youTubeTVInitializer = ((Component)((TileEntityPowered)this).BlockTransform).GetComponent<YouTubeTVInitializer>();
					if ((Object)(object)youTubeTVInitializer == (Object)null)
					{
						Debug.Log((object)$"TileEntityYouTubeTV.SetBlockEntityData: No YouTubeTVInitializer found on transform for TE at {((TileEntity)this).localChunkPos}. Adding one.");
						youTubeTVInitializer = ((Component)((TileEntityPowered)this).BlockTransform).gameObject.AddComponent<YouTubeTVInitializer>();
					}
					if ((Object)(object)youTubeTVInitializer != (Object)null)
					{
						Debug.Log((object)$"TileEntityYouTubeTV.SetBlockEntityData: Explicitly calling Initialize() on YouTubeTVInitializer for TE at {((TileEntity)this).localChunkPos}.");
						youTubeTVInitializer.Initialize(this);
						youtubeController = youTubeTVInitializer.GetController();
					}
					if ((Object)(object)youtubeController != (Object)null)
					{
						Debug.Log((object)$"TileEntityYouTubeTV.SetBlockEntityData: YouTube TV Controller obtained/confirmed via initializer at {((TileEntity)this).localChunkPos}");
						if (ytConfig == null)
						{
							ytConfig = new YTConfig();
						}
						youtubeController.SetYTConfig(ytConfig);
						Debug.Log((object)$"TileEntityYouTubeTV.SetBlockEntityData: Passed YTConfig to YouTubeTVController for TV at {((TileEntity)this).localChunkPos}. ShortsOnly: {ytConfig.ShortsOnly}");
						youtubeController.SetShortsEnabled(enabled: true);
						youtubeController.SetVolume(currentVolume);
						Debug.Log((object)$"TileEntityYouTubeTV.SetBlockEntityData: Set volume to {currentVolume:F2} for TV at {((TileEntity)this).localChunkPos}");
						youtubeController.SetScreenBlack(isBlack: true);
						Debug.Log((object)$"TileEntityYouTubeTV.SetBlockEntityData: Set screen to black initially for TV at {((TileEntity)this).localChunkPos}");
						if (((TileEntityPowered)this).IsPowered)
						{
							youtubeController.SetScreenBlack(isBlack: false);
							if (IsValidVideoURL(currentYouTubeURL))
							{
								Debug.Log((object)$"TileEntityYouTubeTV.SetBlockEntityData: TV is ON. URL: '{currentYouTubeURL}', Synced IsPlaying: {isVideoPlaying}, Synced Time: {currentVideoTime}");
								youtubeController.PlayVideoAtTime(currentYouTubeURL, currentVideoTime, isVideoLooping);
								if (!isVideoPlaying)
								{
									if ((Object)(object)youtubeController != (Object)null && (Object)(object)((Component)youtubeController).gameObject != (Object)null)
									{
										Debug.Log((object)$"TileEntityYouTubeTV.SetBlockEntityData: Synced state is NOT playing for URL '{currentYouTubeURL}'. Starting coroutine to ensure pause after prepare at {currentVideoTime}.");
										((MonoBehaviour)youtubeController).StartCoroutine(PauseAfterPrepareCoroutine(youtubeController, currentVideoTime, currentYouTubeURL));
									}
									else
									{
										Log.Warning("TileEntityYouTubeTV.SetBlockEntityData: Cannot start PauseAfterPrepareCoroutine due to null controller or gameObject for URL '" + currentYouTubeURL + "'.");
									}
								}
								else
								{
									Debug.Log((object)("TileEntityYouTubeTV.SetBlockEntityData: Synced state IS playing for URL '" + currentYouTubeURL + "'. PlayVideoAtTime should handle resuming it."));
								}
							}
							else
							{
								Debug.Log((object)("TileEntityYouTubeTV.SetBlockEntityData: TV is ON, but no valid URL ('" + currentYouTubeURL + "'). Displaying logo."));
								youtubeController.DisplayYouTubeLogo();
							}
						}
						else
						{
							Debug.Log((object)$"TileEntityYouTubeTV.SetBlockEntityData: TV is OFF (IsPowered: {((TileEntityPowered)this).IsPowered}). Screen remains black.");
							if ((Object)(object)youtubeController != (Object)null)
							{
								youtubeController.StopAllPlayback();
							}
						}
					}
					else
					{
						Log.Warning($"TileEntityYouTubeTV.SetBlockEntityData: Could not get YouTube TV controller from initializer at {((TileEntity)this).localChunkPos}.");
					}
					if (GameManager.Instance.World != null)
					{
						if (tvChunkObserver != null)
						{
							GameManager.Instance.RemoveChunkObserver(tvChunkObserver);
							Debug.Log((object)$"[YouTubeTV] TileEntityYouTubeTV.SetBlockEntityData: Preemptively removed existing ChunkObserver for TV at {((TileEntity)this).ToWorldPos()} before re-adding.");
							tvChunkObserver = null;
						}
						if ((Object)(object)youtubeController != (Object)null)
						{
							Vector3i val = ((TileEntity)this).ToWorldPos();
								tvChunkObserver = GameManager.Instance.AddChunkObserver(val.ToVector3(), false, 1, -1);
							Debug.Log((object)$"[YouTubeTV] TileEntityYouTubeTV.SetBlockEntityData: Added new ChunkObserver for TV at {val}");
						}
					}
				}
				return;
			}
			catch (Exception ex)
			{
				Log.Error($"TileEntityYouTubeTV.SetBlockEntityData: Error during TV-specific setup for TE at {((TileEntity)this).localChunkPos}: {ex.Message}\n{ex.StackTrace}");
				return;
			}
		}
		if (!GameManager.IsDedicatedServer)
		{
			Log.Warning($"TileEntityYouTubeTV.SetBlockEntityData: BlockEntityData.bHasTransform is false for TE at {((TileEntity)this).localChunkPos} (client). Cannot perform TV setup.");
		}
	}

	private void CleanupBrokenScripts(Transform rootTransform)
	{
		//IL_00ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f5: Expected O, but got Unknown
		if ((Object)(object)rootTransform == (Object)null)
		{
			return;
		}
		Queue<Transform> queue = new Queue<Transform>();
		queue.Enqueue(rootTransform);
		while (queue.Count > 0)
		{
			Transform val = queue.Dequeue();
			if ((Object)(object)val == (Object)null)
			{
				continue;
			}
			try
			{
				Component[] components = ((Component)val).GetComponents<Component>();
				foreach (Component val2 in components)
				{
					if ((Object)(object)val2 == (Object)null)
					{
						Log.Warning("TileEntityYouTubeTV.CleanupBrokenScripts: Found null script reference on '" + ((Object)val).name + "'.");
						continue;
					}
					string fullName = ((object)val2).GetType().FullName;
					if (fullName.Contains("YouTubeTVControllerYouTubeTVController") || fullName.Contains("YouTubeTVController"))
					{
						Debug.Log((object)("TileEntityYouTubeTV.CleanupBrokenScripts: Removing '" + fullName + "' from '" + ((Object)val).name + "'."));
						Object.DestroyImmediate((Object)(object)val2);
					}
				}
				foreach (Transform item2 in val)
				{
					Transform item = item2;
					queue.Enqueue(item);
				}
			}
			catch (Exception ex)
			{
				Log.Warning("TileEntityYouTubeTV.CleanupBrokenScripts: Error processing '" + ((Object)val).name + "': " + ex.Message);
			}
		}
	}

	private bool IsValidVideoURL(string url)
	{
		if (string.IsNullOrEmpty(url))
		{
			return false;
		}
		try
		{
			if (!Uri.TryCreate(url, UriKind.Absolute, out Uri result))
			{
				return false;
			}
			switch (result.Scheme.ToLowerInvariant())
			{
			case "http":
			case "https":
				return true;
			case "file":
			{
				if (!result.IsLoopback && result.Host != "")
				{
					Log.Warning("[Security] Disallowed network file path: " + url);
					return false;
				}
				string extension = Path.GetExtension(result.LocalPath);
				if (string.IsNullOrEmpty(extension) || !_allowedVideoExtensions.Contains(extension))
				{
					Log.Warning("[Security] Disallowed file type. The extension '" + extension + "' is not in the allowed list for URL: " + url);
					return false;
				}
				return true;
			}
			default:
				Log.Warning("[Security] Disallowed URL scheme '" + result.Scheme + "' in URL: " + url);
				return false;
			}
		}
		catch (Exception ex)
		{
			Log.Error("An unexpected error occurred during URL validation for '" + url + "': " + ex.Message);
			return false;
		}
	}

	public void SetShortsEnabled(bool enabled)
	{
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)youtubeController != (Object)null)
		{
			youtubeController.SetShortsEnabled(enabled);
			Debug.Log((object)string.Format("TileEntityYouTubeTV: Shorts functionality {0} for TV at {1}", enabled ? "enabled" : "disabled", ((TileEntity)this).localChunkPos));
		}
	}

	public override void read(PooledBinaryReader _br, StreamModeRead _eStreamMode)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0243: Unknown result type (might be due to invalid IL or missing references)
		//IL_0207: Unknown result type (might be due to invalid IL or missing references)
		//IL_020c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0215: Unknown result type (might be due to invalid IL or missing references)
		//IL_022c: Unknown result type (might be due to invalid IL or missing references)
		//IL_01eb: Unknown result type (might be due to invalid IL or missing references)
		Debug.Log((object)$"[YouTubeTVDebug] TileEntityYouTubeTV.read() called for TE at {((TileEntity)this).localChunkPos}. Instance: {((object)this).GetType().FullName}. Expecting to read data for TileEntityYouTubeTV.");
		base.read(_br, _eStreamMode);
		Debug.Log((object)$"[YouTubeTVDebug] TileEntityYouTubeTV.read() POST-BASECALL for TE at {((TileEntity)this).localChunkPos}. Reading YouTubeTV specific data.");
		int num = ((BinaryReader)(object)_br).ReadInt32();
		Debug.Log((object)$"[YouTubeTVDebug] TileEntityYouTubeTV.read: Read Version: {num}");
		if (num >= 1)
		{
			currentYouTubeURL = ((BinaryReader)(object)_br).ReadString();
			Debug.Log((object)("[YouTubeTVDebug] TileEntityYouTubeTV.read (v1): Read URL '" + currentYouTubeURL + "'"));
			if (ytConfig == null)
			{
				ytConfig = new YTConfig();
			}
			ytConfig.Username = ((BinaryReader)(object)_br).ReadString();
			ytConfig.Password = ((BinaryReader)(object)_br).ReadString();
			ytConfig.ShortsOnly = ((BinaryReader)(object)_br).ReadBoolean();
			Debug.Log((object)$"[YouTubeTVDebug] TileEntityYouTubeTV.read (v1): Read YTConfig - Username: '{ytConfig.Username}', ShortsOnly: {ytConfig.ShortsOnly}");
		}
		if (num >= 2)
		{
			isVideoPlaying = ((BinaryReader)(object)_br).ReadBoolean();
			currentVideoTime = ((BinaryReader)(object)_br).ReadDouble();
			isVideoLooping = ((BinaryReader)(object)_br).ReadBoolean();
			Debug.Log((object)$"[YouTubeTVDebug] TileEntityYouTubeTV.read (v2): Read PlaybackState - IsPlaying: {isVideoPlaying}, Time: {currentVideoTime}, Looping: {isVideoLooping}");
		}
		if (num >= 3)
		{
			currentVolume = ((BinaryReader)(object)_br).ReadSingle();
			Debug.Log((object)$"[YouTubeTVDebug] TileEntityYouTubeTV.read (v3): Read Volume: {currentVolume}");
		}
		if (num < 2)
		{
			isVideoPlaying = false;
			currentVideoTime = 0.0;
			isVideoLooping = true;
		}
		if (num < 3)
		{
			currentVolume = 0.5f;
		}
		if (!SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer)
		{
			return;
		}
		if (GameManager.Instance.World != null)
		{
			if (tvChunkObserver != null)
			{
				GameManager.Instance.RemoveChunkObserver(tvChunkObserver);
				Debug.Log((object)$"[YouTubeTV] TileEntityYouTubeTV.read: Removed existing ChunkObserver for TV at {((TileEntity)this).ToWorldPos()} before re-adding.");
				tvChunkObserver = null;
			}
			Vector3i val = ((TileEntity)this).ToWorldPos();
				tvChunkObserver = GameManager.Instance.AddChunkObserver(val.ToVector3(), false, 1, -1);
			Debug.Log((object)$"[YouTubeTV] TileEntityYouTubeTV.read: Added ChunkObserver for TV at {val} on server.");
		}
		else
		{
			Log.Warning($"[YouTubeTV] TileEntityYouTubeTV.read: GameManager.Instance.World is null on server. Cannot manage ChunkObserver for TV at {((TileEntity)this).ToWorldPos()}.");
		}
	}

	public override void write(PooledBinaryWriter _bw, StreamModeWrite _eStreamMode)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Expected I4, but got Unknown
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		Debug.Log((object)$"[YouTubeTVDebug] TileEntityYouTubeTV.write() PRE-BASECALL for TE at {((TileEntity)this).localChunkPos}. Instance: {((object)this).GetType().FullName}, TE Type from GetTileEntityType(): {(int)((TileEntity)this).GetTileEntityType()} ({((TileEntity)this).GetTileEntityType()})");
		base.write(_bw, _eStreamMode);
		Debug.Log((object)$"[YouTubeTVDebug] TileEntityYouTubeTV.write() POST-BASECALL for TE at {((TileEntity)this).localChunkPos}. Writing YouTubeTV specific data.");
		((BinaryWriter)(object)_bw).Write(3);
		((BinaryWriter)(object)_bw).Write(currentYouTubeURL ?? "");
		if (ytConfig == null)
		{
			ytConfig = new YTConfig();
		}
		((BinaryWriter)(object)_bw).Write(ytConfig.Username ?? "");
		((BinaryWriter)(object)_bw).Write(ytConfig.Password ?? "");
		((BinaryWriter)(object)_bw).Write(ytConfig.ShortsOnly);
		((BinaryWriter)(object)_bw).Write(isVideoPlaying);
		((BinaryWriter)(object)_bw).Write(currentVideoTime);
		((BinaryWriter)(object)_bw).Write(isVideoLooping);
		((BinaryWriter)(object)_bw).Write(currentVolume);
		Debug.Log((object)string.Format("[YouTubeTVDebug] TileEntityYouTubeTV.write: Saved Version: {0}, URL '{1}', YTConfig - Username: '{2}', ShortsOnly: {3}, IsPlaying: {4}, Time: {5}, Looping: {6}, Volume: {7}", 3, currentYouTubeURL ?? "", ytConfig.Username, ytConfig.ShortsOnly, isVideoPlaying, currentVideoTime, isVideoLooping, currentVolume));
	}

	public override bool Activate(bool activated)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_006f: Unknown result type (might be due to invalid IL or missing references)
		//IL_011b: Unknown result type (might be due to invalid IL or missing references)
		Debug.Log((object)string.Format("TileEntityYouTubeTV.Activate: TV at {0} received base power state: {1}. Current IsPowered: {2}", ((TileEntity)this).localChunkPos, activated ? "ON" : "OFF", ((TileEntityPowered)this).IsPowered));
		bool result = base.Activate(activated);
		if (SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer)
		{
			Debug.Log((object)$"TileEntityYouTubeTV.Activate (Server Context): Power state is now {((TileEntityPowered)this).IsPowered}. Broadcasting to clients.");
			SingletonMonoBehaviour<ConnectionManager>.Instance.SendToClientsOrServer((NetPackage)(object)new NetPackageSetYouTubeURLClient().Setup(((TileEntity)this).ToWorldPos(), currentYouTubeURL, ((TileEntityPowered)this).IsPowered));
			if (!GameManager.IsDedicatedServer && (Object)(object)youtubeController != (Object)null)
			{
				Debug.Log((object)"TileEntityYouTubeTV.Activate (Host Client Context): Updating local controller.");
				ClientUpdateURLAndPower(currentYouTubeURL, ((TileEntityPowered)this).IsPowered);
				ClientControlPlayback((!isVideoPlaying) ? PlaybackCommand.Pause : PlaybackCommand.Play, currentYouTubeURL, isVideoPlaying, currentVideoTime, isVideoLooping);
				return result;
			}
		}
		else
		{
			if ((Object)(object)youtubeController != (Object)null)
			{
				Debug.Log((object)$"TileEntityYouTubeTV.Activate (Client): Power state is {((TileEntityPowered)this).IsPowered}. Controller will be updated by NetPackage or TE sync.");
				return result;
			}
			Log.Warning($"TileEntityYouTubeTV.Activate (Client): YouTube controller is null for TV at {((TileEntity)this).localChunkPos}. Cannot update local controller state directly.");
		}
		return result;
	}

	public override PowerItem CreatePowerItem()
	{
		return PowerItem.CreateItem((PowerItem.PowerItemTypes)1);
	}

	public override void SetValuesFromBlock(ushort blockID)
	{
		base.SetValuesFromBlock(blockID);
	}

	public override TileEntity Clone()
	{
		TileEntityYouTubeTV tileEntityYouTubeTV = (TileEntityYouTubeTV)(object)base.Clone();
		tileEntityYouTubeTV.currentYouTubeURL = currentYouTubeURL;
		if (ytConfig != null)
		{
			tileEntityYouTubeTV.ytConfig = new YTConfig
			{
				Username = ytConfig.Username,
				Password = ytConfig.Password,
				ShortsOnly = ytConfig.ShortsOnly
			};
		}
		else
		{
			tileEntityYouTubeTV.ytConfig = new YTConfig();
			Log.Warning("[YouTubeTVDebug] TileEntityYouTubeTV.Clone: Source ytConfig was null. Cloned TE will have default YTConfig.");
		}
		tileEntityYouTubeTV.isVideoPlaying = isVideoPlaying;
		tileEntityYouTubeTV.currentVideoTime = currentVideoTime;
		tileEntityYouTubeTV.isVideoLooping = isVideoLooping;
		tileEntityYouTubeTV.lastServerTimeUpdate = lastServerTimeUpdate;
		tileEntityYouTubeTV.currentVolume = currentVolume;
		return (TileEntity)(object)tileEntityYouTubeTV;
	}

	[IteratorStateMachine(typeof(_003CPauseAfterPrepareCoroutine_003Ed__32))]
	private IEnumerator PauseAfterPrepareCoroutine(YouTubeTVController controller, double targetTime, string expectedUrl)
	{
		//yield-return decompiler failed: Unexpected instruction in Iterator.Dispose()
		return new _003CPauseAfterPrepareCoroutine_003Ed__32(0)
		{
			controller = controller,
			targetTime = targetTime,
			expectedUrl = expectedUrl
		};
	}

	public override void CopyFrom(TileEntity _other)
	{
		//IL_00c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d5: Unknown result type (might be due to invalid IL or missing references)
		base.CopyFrom(_other);
		if (_other is TileEntityYouTubeTV tileEntityYouTubeTV)
		{
			currentYouTubeURL = tileEntityYouTubeTV.currentYouTubeURL;
			if (tileEntityYouTubeTV.ytConfig != null)
			{
				ytConfig = new YTConfig
				{
					Username = tileEntityYouTubeTV.ytConfig.Username,
					Password = tileEntityYouTubeTV.ytConfig.Password,
					ShortsOnly = tileEntityYouTubeTV.ytConfig.ShortsOnly
				};
			}
			else
			{
				ytConfig = new YTConfig();
				Log.Warning("[YouTubeTVDebug] TileEntityYouTubeTV.CopyFrom: Source otherTV.ytConfig was null. Copied TE will have default YTConfig.");
			}
			isVideoPlaying = tileEntityYouTubeTV.isVideoPlaying;
			currentVideoTime = tileEntityYouTubeTV.currentVideoTime;
			isVideoLooping = tileEntityYouTubeTV.isVideoLooping;
			lastServerTimeUpdate = tileEntityYouTubeTV.lastServerTimeUpdate;
			currentVolume = tileEntityYouTubeTV.currentVolume;
			Debug.Log((object)string.Format("TileEntityYouTubeTV.CopyFrom: Copied state from {0} to {1}. URL: {2}, IsPowered: {3}, ShortsOnly: {4}", ((TileEntity)tileEntityYouTubeTV).ToWorldPos(), ((TileEntity)this).ToWorldPos(), currentYouTubeURL, ((TileEntityPowered)this).IsPowered, (ytConfig != null) ? ytConfig.ShortsOnly.ToString() : "null/default"));
		}
		else
		{
			Log.Warning("TileEntityYouTubeTV.CopyFrom: Attempted to copy from an incompatible TileEntity type: " + (((object)_other)?.GetType().Name ?? "null"));
		}
	}

	public override TileEntityType GetTileEntityType()
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Expected I4, but got Unknown
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		TileEntityType val = (TileEntityType)250;
		Debug.Log((object)$"[YouTubeTVDebug] TileEntityYouTubeTV.GetTileEntityType() called for TE at {((TileEntity)this).localChunkPos}. Instance: {((object)this).GetType().FullName}. Returning ID: {(int)val} ({val})");
		return val;
	}

	public override void OnUnload(World world)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f7: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ab: Unknown result type (might be due to invalid IL or missing references)
		Debug.Log((object)$"[YouTubeTV] TileEntityYouTubeTV.OnUnload: Starting for TE at {((TileEntity)this).localChunkPos}.");
		if ((Object)(object)youtubeController != (Object)null && !GameManager.IsDedicatedServer)
		{
			Debug.Log((object)$"[YouTubeTV] TileEntityYouTubeTV.OnUnload: Stopping video and setting screen black for TV at {((TileEntity)this).localChunkPos} on client.");
			youtubeController.StopAllPlayback();
			youtubeController.SetScreenBlack(isBlack: true);
		}
		youtubeController = null;
		if (SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer)
		{
			object obj = world;
			if (obj == null)
			{
				GameManager instance = GameManager.Instance;
				obj = ((instance != null) ? instance.World : null);
			}
			World val = (World)obj;
			if (tvChunkObserver != null && val != null)
			{
				GameManager.Instance.RemoveChunkObserver(tvChunkObserver);
				Debug.Log((object)$"[YouTubeTV] TileEntityYouTubeTV.OnUnload: Removed ChunkObserver for TV at {((TileEntity)this).ToWorldPos()} on server.");
				tvChunkObserver = null;
			}
			else if (tvChunkObserver != null)
			{
				Log.Warning($"[YouTubeTV] TileEntityYouTubeTV.OnUnload: World instance is null on server. Cannot remove ChunkObserver for TV at {((TileEntity)this).ToWorldPos()}. This might lead to a leak if observer was active.");
			}
		}
		base.OnUnload(world);
		Debug.Log((object)$"[YouTubeTV] TileEntityYouTubeTV.OnUnload: Completed for TE at {((TileEntity)this).localChunkPos}.");
	}
}
