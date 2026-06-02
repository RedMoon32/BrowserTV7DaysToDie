using System.IO;

public class BrowserTvCommandPackage : NetPackage
{
    private BrowserTvCommandType command;
    private Vector3i blockPos;
    private string text;
    private float value;
    private int entityId;

    public override NetPackageDirection PackageDirection => NetPackageDirection.ToServer;

    public BrowserTvCommandPackage Setup(BrowserTvCommandType commandType, Vector3i pos, string commandText, float commandValue, int playerEntityId)
    {
        command = commandType;
        blockPos = pos;
        text = commandText ?? "";
        value = commandValue;
        entityId = playerEntityId;
        return this;
    }

    public override void read(PooledBinaryReader reader)
    {
        command = (BrowserTvCommandType)((BinaryReader)(object)reader).ReadInt32();
        blockPos = new Vector3i(((BinaryReader)(object)reader).ReadInt32(), ((BinaryReader)(object)reader).ReadInt32(), ((BinaryReader)(object)reader).ReadInt32());
        text = ((BinaryReader)(object)reader).ReadString();
        value = ((BinaryReader)(object)reader).ReadSingle();
        entityId = ((BinaryReader)(object)reader).ReadInt32();
    }

    public override void write(PooledBinaryWriter writer)
    {
        base.write(writer);
        ((BinaryWriter)(object)writer).Write((int)command);
        ((BinaryWriter)(object)writer).Write(blockPos.x);
        ((BinaryWriter)(object)writer).Write(blockPos.y);
        ((BinaryWriter)(object)writer).Write(blockPos.z);
        ((BinaryWriter)(object)writer).Write(text ?? "");
        ((BinaryWriter)(object)writer).Write(value);
        ((BinaryWriter)(object)writer).Write(entityId);
    }

    public override void ProcessPackage(World world, GameManager callbacks)
    {
        if (world == null || !SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer)
        {
            return;
        }

        if (!ValidEntityIdForSender(entityId, false))
        {
            return;
        }

        BrowserTvServerStateService.HandleCommand(command, blockPos, text, value, entityId);
    }

    public override int GetLength()
    {
        return 28 + (text?.Length ?? 0) * 2;
    }
}
