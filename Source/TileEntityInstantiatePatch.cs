using HarmonyLib;

[HarmonyPatch(typeof(TileEntity), nameof(TileEntity.InstantiateFromRead))]
public static class BrowserTvTileEntityInstantiatePatch
{
    private static bool Prefix(ref TileEntity __result, PooledBinaryReader _br, StreamModeRead _eStreamMode, TileEntityType _type, Chunk _chunk)
    {
        if ((int)_type != TileEntityBrowserTV.CustomTileEntityTypeId)
        {
            return true;
        }

        // 3.4 constructs and reads the entity in this method. Consume its data
        // here too, including network reads whose chunk is assigned later.
        TileEntityBrowserTV tileEntity = new TileEntityBrowserTV(_chunk);
        tileEntity.read(_br, _eStreamMode);
        __result = tileEntity;
        return false;
    }
}
