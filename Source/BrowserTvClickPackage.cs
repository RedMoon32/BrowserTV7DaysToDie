using System.IO;

public class BrowserTvClickPackage : NetPackage
{
    private Vector3i blockPos;
    private float u;
    private float v;
    private int entityId;

    public override NetPackageDirection PackageDirection => NetPackageDirection.ToServer;

    public BrowserTvClickPackage Setup(Vector3i pos, float normalizedU, float normalizedV, int playerEntityId)
    {
        blockPos = pos;
        u = normalizedU;
        v = normalizedV;
        entityId = playerEntityId;
        return this;
    }

    public override void read(PooledBinaryReader reader)
    {
        BinaryReader binaryReader = (BinaryReader)(object)reader;
        blockPos = new Vector3i(binaryReader.ReadInt32(), binaryReader.ReadInt32(), binaryReader.ReadInt32());
        u = binaryReader.ReadSingle();
        v = binaryReader.ReadSingle();
        entityId = binaryReader.ReadInt32();
    }

    public override void write(PooledBinaryWriter writer)
    {
        base.write(writer);
        BinaryWriter binaryWriter = (BinaryWriter)(object)writer;
        binaryWriter.Write(blockPos.x);
        binaryWriter.Write(blockPos.y);
        binaryWriter.Write(blockPos.z);
        binaryWriter.Write(u);
        binaryWriter.Write(v);
        binaryWriter.Write(entityId);
    }

    public override void ProcessPackage(World world, GameManager callbacks)
    {
        if (world == null || !SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer || !ValidEntityIdForSender(entityId, false))
        {
            return;
        }

        BrowserTvServerStateService.HandleClick(world, blockPos, u, v, entityId);
    }

    public override int GetLength()
    {
        return 28;
    }
}
