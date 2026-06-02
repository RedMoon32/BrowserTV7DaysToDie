using HarmonyLib;

namespace YouTubeTVMod;

[HarmonyPatch(typeof(Block))]
[HarmonyPatch("GetActivationDistanceSq")]
public class BlockActivationDistPatch
{
	public static bool Prefix(Block __instance, ref int __result)
	{
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		if (__instance is BlockYouTubeTV)
		{
			EntityPlayerLocal primaryPlayer = ((WorldBase)GameManager.Instance.World).GetPrimaryPlayer();
			object obj;
			if (primaryPlayer == null)
			{
				obj = null;
			}
			else
			{
				Inventory inventory = ((EntityAlive)primaryPlayer).inventory;
				obj = ((inventory != null) ? inventory.GetHoldingPrimary() : null);
			}
			if (obj is ItemActionYouTubeTVVolume)
			{
				WorldRayHitInfo hitInfo = primaryPlayer.HitInfo;
				if (hitInfo.bHitValid)
				{
						BlockValue blockValue = hitInfo.hit.blockValue;
						if (blockValue.Block is BlockYouTubeTV)
					{
						__result = 400;
						return false;
					}
				}
			}
		}
		return true;
	}
}
