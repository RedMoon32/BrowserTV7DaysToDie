using System;
using HarmonyLib;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

[HarmonyPatch(typeof(RenderDisplacedCube))]
public static class RenderDisplacedCubePatch
{
	[HarmonyPatch("disableAllComponents")]
	[HarmonyPrefix]
	public static bool DisableAllComponentsPrefix(Transform _transform)
	{
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Expected O, but got Unknown
		if ((Object)(object)_transform == (Object)null)
		{
			return false;
		}
		try
		{
			Component[] components = ((Component)_transform).GetComponents<Component>();
			foreach (Component val in components)
			{
				if (!((Object)(object)val == (Object)null))
				{
					Behaviour val2 = (Behaviour)(object)((val is Behaviour) ? val : null);
					if (val2 != null)
					{
						val2.enabled = false;
					}
				}
			}
			foreach (Transform item in _transform)
			{
				Transform val3 = item;
				if ((Object)(object)val3 != (Object)null)
				{
					DisableAllComponentsPrefix(val3);
				}
			}
		}
		catch (Exception ex)
		{
			Log.Warning("Error in DisableAllComponentsPrefix: " + ex.Message);
		}
		return false;
	}
}
