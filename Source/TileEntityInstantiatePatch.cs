using HarmonyLib;
using UnityEngine;

[HarmonyPatch(typeof(TileEntity), "Instantiate", typeof(TileEntityType), typeof(Chunk))]
public static class BrowserTvTileEntityInstantiatePatch
{
    private static void Postfix(ref TileEntity __result, TileEntityType type, Chunk _chunk)
    {
        if ((int)type != TileEntityBrowserTV.CustomTileEntityTypeId)
        {
            return;
        }

        if (_chunk == null)
        {
            Debug.LogError("[BrowserTV] Cannot instantiate TileEntityBrowserTV because chunk is null.");
            return;
        }

        __result = new TileEntityBrowserTV(_chunk);
    }
}
