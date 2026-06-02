using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;
using UnityEngine.Video;

public class YouTubeTVBlock : MonoBehaviour
{
	private Vector3i worldPos;

	private YouTubeTVController controller;

	public void Initialize(Vector3i blockWorldPos)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a1: Unknown result type (might be due to invalid IL or missing references)
		worldPos = blockWorldPos;
		controller = ((Component)this).GetComponentInChildren<YouTubeTVController>();
		if ((Object)(object)controller == (Object)null)
		{
			Renderer val = FindScreenRenderer();
			if ((Object)(object)val != (Object)null)
			{
				if ((Object)(object)((Component)val).gameObject.GetComponent<VideoPlayer>() == (Object)null)
				{
					((Component)val).gameObject.AddComponent<VideoPlayer>();
				}
				if ((Object)(object)((Component)val).gameObject.GetComponent<AudioSource>() == (Object)null)
				{
					((Component)val).gameObject.AddComponent<AudioSource>();
				}
				controller = ((Component)val).gameObject.AddComponent<YouTubeTVController>();
				controller.Initialize(val);
			}
		}
		if ((Object)(object)controller != (Object)null)
		{
			YouTubeTVManager.Instance.RegisterTV(worldPos, controller);
		}
	}

	private Renderer FindScreenRenderer()
	{
		string[] array = new string[1] { "FilePlane" };
		foreach (string text in array)
		{
			Transform val = ((Component)this).transform.Find(text);
			if ((Object)(object)val == (Object)null)
			{
				val = FindChildRecursive(((Component)this).transform, text);
			}
			if ((Object)(object)val != (Object)null)
			{
				Renderer component = ((Component)val).GetComponent<Renderer>();
				if ((Object)(object)component != (Object)null)
				{
					return component;
				}
			}
		}
		return (Renderer)(object)((Component)this).GetComponentInChildren<MeshRenderer>();
	}

	private Transform FindChildRecursive(Transform parent, string name)
	{
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Expected O, but got Unknown
		foreach (Transform item in parent)
		{
			Transform val = item;
			if (((Object)val).name.ToLower().Contains(name.ToLower()))
			{
				return val;
			}
			Transform val2 = FindChildRecursive(val, name);
			if ((Object)(object)val2 != (Object)null)
			{
				return val2;
			}
		}
		return null;
	}

	private void OnDestroy()
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		YouTubeTVManager.Instance.UnregisterTV(worldPos);
	}
}
