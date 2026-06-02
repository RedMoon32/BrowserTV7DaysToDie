using System;
using HarmonyLib;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace YouTubeTVMod;

[HarmonyPatch(typeof(TileEntity))]
[HarmonyPatch("Instantiate")]
[HarmonyPatch(new Type[]
{
	typeof(TileEntityType),
	typeof(Chunk)
})]
public class TileEntity_Instantiate_Patch
{
	private static void Postfix(ref TileEntity __result, TileEntityType type, Chunk _chunk)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Invalid comparison between Unknown and I4
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Expected I4, but got Unknown
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0072: Expected I4, but got Unknown
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Expected I4, but got Unknown
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Expected I4, but got Unknown
		if ((int)type != 250)
		{
			return;
		}
		if (__result == null)
		{
			if (_chunk == null)
			{
				Log.Error($"[YouTubeTVHarmony] TileEntity.Instantiate Postfix: Chunk is null when trying to create TileEntityYouTubeTV for type ID {(int)type}. This is a critical error.");
				return;
			}
			Debug.Log((object)$"[YouTubeTVHarmony] TileEntity.Instantiate Postfix: Detected CustomTileEntityTypeId ({(int)type}). Original result was null. Creating TileEntityYouTubeTV.");
			__result = (TileEntity)(object)new TileEntityYouTubeTV(_chunk);
		}
		else
		{
			Log.Warning($"[YouTubeTVHarmony] TileEntity.Instantiate Postfix: Detected CustomTileEntityTypeId ({(int)type}), but original method returned a non-null TE: {((object)__result).GetType().FullName}. This is unexpected. Overwriting with TileEntityYouTubeTV.");
			if (_chunk == null)
			{
				Log.Error($"[YouTubeTVHarmony] TileEntity.Instantiate Postfix: Chunk is null when trying to overwrite with TileEntityYouTubeTV for type ID {(int)type}. This is a critical error.");
			}
			else
			{
				__result = (TileEntity)(object)new TileEntityYouTubeTV(_chunk);
			}
		}
	}
}
