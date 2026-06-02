using HarmonyLib;

namespace YouTubeTVMod;

[HarmonyPatch(typeof(ItemActionConnectPower))]
[HarmonyPatch("OnHoldingUpdate")]
public static class ItemActionConnectPower_OnHoldingUpdate_Patch
{
	public static bool Prefix(ItemActionConnectPower __instance, ItemActionData _actionData)
	{
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_0066: Unknown result type (might be due to invalid IL or missing references)
		//IL_0067: Unknown result type (might be due to invalid IL or missing references)
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		//IL_007e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0080: Unknown result type (might be due to invalid IL or missing references)
		ItemActionConnectPower.ConnectPowerData val = (ItemActionConnectPower.ConnectPowerData)(object)((_actionData is ItemActionConnectPower.ConnectPowerData) ? _actionData : null);
		if (val == null || !((ItemActionData)val).invData.hitInfo.bHitValid)
		{
			return true;
		}
		Vector3i blockPos = ((ItemActionData)val).invData.hitInfo.hit.blockPos;
		BlockValue block = ((WorldBase)((ItemActionData)val).invData.world).GetBlock(blockPos);
			Block block2 = block.Block;
			if (block2.isMultiBlock && block.ischild)
		{
			Vector3i parentPos = block2.multiBlockPos.GetParentPos(blockPos, block);
			((ItemActionData)val).invData.hitInfo.hit.blockPos = parentPos;
		}
		return true;
	}
}
