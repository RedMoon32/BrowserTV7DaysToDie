using System.IO;
using UnityEngine;

public class TileEntityBrowserTV : TileEntityPowered
{
    public const int CustomTileEntityTypeId = 251;

    private BrowserTvInitializer initializer;

    public TileEntityBrowserTV(Chunk chunk) : base(chunk)
    {
        PowerItemType = PowerItem.PowerItemTypes.Consumer;
        InitializePowerData();
    }

    public void SetBlockEntityData(BlockEntityData blockEntityData)
    {
        if (GameManager.IsDedicatedServer || blockEntityData == null || blockEntityData.transform == null)
        {
            return;
        }

        initializer = blockEntityData.transform.gameObject.GetComponent<BrowserTvInitializer>();
        if (initializer == null)
        {
            initializer = blockEntityData.transform.gameObject.AddComponent<BrowserTvInitializer>();
        }

        initializer.Initialize(this);
        BrowserTvManager.Instance.SetScreenState(((TileEntity)this).ToWorldPos(), IsPowered ? BrowserTvScreenState.Standby : BrowserTvScreenState.Off);
    }

    public override bool Activate(bool activated)
    {
        bool result = base.Activate(activated);
        if (!GameManager.IsDedicatedServer)
        {
            BrowserTvManager.Instance.SetScreenState(((TileEntity)this).ToWorldPos(), IsPowered ? BrowserTvScreenState.Standby : BrowserTvScreenState.Off);
        }

        return result;
    }

    public override void read(PooledBinaryReader br, StreamModeRead streamMode)
    {
        base.read(br, streamMode);
    }

    public override void write(PooledBinaryWriter bw, StreamModeWrite streamMode)
    {
        base.write(bw, streamMode);
    }

    public override PowerItem CreatePowerItem()
    {
        return PowerItem.CreateItem(PowerItem.PowerItemTypes.Consumer);
    }

    public override TileEntity Clone()
    {
        return base.Clone();
    }

    public override void CopyFrom(TileEntity other)
    {
        base.CopyFrom(other);
    }

    public override TileEntityType GetTileEntityType()
    {
        return (TileEntityType)CustomTileEntityTypeId;
    }

    public override void OnUnload(World world)
    {
        if (!GameManager.IsDedicatedServer)
        {
            BrowserTvManager.Instance.Unregister(((TileEntity)this).ToWorldPos());
        }

        base.OnUnload(world);
    }
}
