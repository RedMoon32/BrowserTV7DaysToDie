using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

public class YouTubeTVManager : MonoBehaviour
{
	private static YouTubeTVManager instance;

	private Dictionary<Vector3i, YouTubeTVController> tvControllers = new Dictionary<Vector3i, YouTubeTVController>();

	public static YouTubeTVManager Instance
	{
		get
		{
			//IL_0012: Unknown result type (might be due to invalid IL or missing references)
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			//IL_0027: Expected O, but got Unknown
			if ((Object)(object)instance == (Object)null)
			{
				GameObject val = new GameObject("YouTubeTVManager");
				instance = val.AddComponent<YouTubeTVManager>();
				Object.DontDestroyOnLoad((Object)val);
			}
			return instance;
		}
	}

	public void RegisterTV(Vector3i worldPos, YouTubeTVController controller)
	{
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)controller != (Object)null)
		{
			tvControllers[worldPos] = controller;
			Debug.Log((object)$"Registered YouTube TV at position {worldPos}");
		}
	}

	public void UnregisterTV(Vector3i worldPos)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0058: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		if (tvControllers.ContainsKey(worldPos))
		{
			YouTubeTVController youTubeTVController = tvControllers[worldPos];
			if ((Object)(object)youTubeTVController != (Object)null)
			{
				youTubeTVController.StopAllPlayback();
				youTubeTVController.SetScreenBlack(isBlack: true);
				Debug.Log((object)$"Stopped playback and set screen black for TV at {worldPos}");
			}
			tvControllers.Remove(worldPos);
			Debug.Log((object)$"Unregistered YouTube TV at position {worldPos}");
			CleanupTempDirectory();
		}
	}

	public void PlayVideo(Vector3i worldPos, string youtubeURL)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		if (tvControllers.TryGetValue(worldPos, out var value))
		{
			if ((Object)(object)value != (Object)null)
			{
				value.PlayYouTubeVideo(youtubeURL);
			}
		}
		else
		{
			Debug.LogWarning((object)$"No YouTube TV controller found at position {worldPos}");
		}
	}

	public void StopVideo(Vector3i worldPos)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		if (tvControllers.TryGetValue(worldPos, out var value) && (Object)(object)value != (Object)null)
		{
			value.StopAllPlayback();
			value.SetScreenBlack(isBlack: true);
		}
	}

	public YouTubeTVController GetController(Vector3i worldPos)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		tvControllers.TryGetValue(worldPos, out var value);
		return value;
	}

	public YouTubeTVController GetNearestTVController(Vector3 position, float maxDistance)
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_0082: Unknown result type (might be due to invalid IL or missing references)
		//IL_0096: Unknown result type (might be due to invalid IL or missing references)
		//IL_009b: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_006d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e5: Unknown result type (might be due to invalid IL or missing references)
		Debug.Log((object)$"[YouTubeTVManager] GetNearestTVController: Searching for TVs near {position} within {maxDistance}m. Registered TV count: {tvControllers.Count}");
		YouTubeTVController youTubeTVController = null;
		float num = float.MaxValue;
		foreach (KeyValuePair<Vector3i, YouTubeTVController> tvController in tvControllers)
		{
			Vector3i key = tvController.Key;
			YouTubeTVController value = tvController.Value;
			if ((Object)(object)value == (Object)null)
			{
				Debug.Log((object)$"[YouTubeTVManager] GetNearestTVController: TV at {key} has null controller, skipping");
				continue;
			}
				Vector3 val = key.ToVector3() + new Vector3(0.5f, 0.5f, 0.5f);
			float num2 = Vector3.Distance(position, val);
			Debug.Log((object)$"[YouTubeTVManager] GetNearestTVController: TV at {key} (block world pos {val}) is {num2:F2}m away");
			if (num2 <= maxDistance && num2 < num)
			{
				num = num2;
				youTubeTVController = value;
				Debug.Log((object)$"[YouTubeTVManager] GetNearestTVController: TV at {key} is new nearest candidate ({num2:F2}m)");
			}
		}
		if ((Object)(object)youTubeTVController != (Object)null)
		{
			Debug.Log((object)$"[YouTubeTVManager] GetNearestTVController: Found nearest TV at {num:F2}m");
		}
		else
		{
			Debug.Log((object)$"[YouTubeTVManager] GetNearestTVController: No TV found within {maxDistance}m");
		}
		return youTubeTVController;
	}

	public int GetRegisteredTVCount()
	{
		return tvControllers.Count;
	}

	public void CleanupAllTVs()
	{
		foreach (KeyValuePair<Vector3i, YouTubeTVController> tvController in tvControllers)
		{
			if ((Object)(object)tvController.Value != (Object)null)
			{
				tvController.Value.StopAllPlayback();
				tvController.Value.SetScreenBlack(isBlack: true);
			}
		}
		tvControllers.Clear();
		CleanupTempDirectory();
	}

	public bool AnyTVsPlayingShorts()
	{
		foreach (KeyValuePair<Vector3i, YouTubeTVController> tvController in tvControllers)
		{
			if ((Object)(object)tvController.Value != (Object)null && tvController.Value.IsPlayingShorts())
			{
				return true;
			}
		}
		return false;
	}

	public void CleanupTempDirectory()
	{
		if (!AnyTVsPlayingShorts())
		{
			try
			{
				string path = Path.Combine(Application.persistentDataPath, "shorts_temp");
				if (Directory.Exists(path))
				{
					string[] files = Directory.GetFiles(path);
					string[] array = files;
					foreach (string text in array)
					{
						try
						{
							File.Delete(text);
						}
						catch (Exception ex)
						{
							Debug.LogWarning((object)("Failed to delete temp file " + text + ": " + ex.Message));
						}
					}
					Debug.Log((object)$"Cleaned up {files.Length} files from shorts temp directory");
				}
				return;
			}
			catch (Exception ex2)
			{
				Debug.LogError((object)("Failed to cleanup temp directory: " + ex2.Message));
				return;
			}
		}
		Debug.Log((object)"Skipping temp directory cleanup - TVs are still playing shorts");
	}

	private void OnDestroy()
	{
		CleanupAllTVs();
	}
}
