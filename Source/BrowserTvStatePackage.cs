using System.IO;
using UnityEngine;

public class BrowserTvStatePackage : NetPackage
{
    private BrowserTvState state = new BrowserTvState();

    public override NetPackageDirection PackageDirection => NetPackageDirection.ToClient;

    public BrowserTvStatePackage Setup(BrowserTvState source)
    {
        state = source.Clone();
        return this;
    }

    public override void read(PooledBinaryReader reader)
    {
        state.Power = (BrowserTvPowerState)((BinaryReader)(object)reader).ReadInt32();
        state.BlockPos = new Vector3i(((BinaryReader)(object)reader).ReadInt32(), ((BinaryReader)(object)reader).ReadInt32(), ((BinaryReader)(object)reader).ReadInt32());
        state.SessionId = ((BinaryReader)(object)reader).ReadString();
        state.BridgeEndpoint = ((BinaryReader)(object)reader).ReadString();
        state.StreamUrl = ((BinaryReader)(object)reader).ReadString();
        state.ViewerToken = ((BinaryReader)(object)reader).ReadString();
        state.ControllerToken = ((BinaryReader)(object)reader).ReadString();
        state.ControllerEntityId = ((BinaryReader)(object)reader).ReadInt32();
        state.CurrentUrl = ((BinaryReader)(object)reader).ReadString();
        state.Volume = ((BinaryReader)(object)reader).ReadSingle();
        state.StatusText = ((BinaryReader)(object)reader).ReadString();
        state.Revision = ((BinaryReader)(object)reader).ReadInt32();
    }

    public override void write(PooledBinaryWriter writer)
    {
        base.write(writer);
        ((BinaryWriter)(object)writer).Write((int)state.Power);
        ((BinaryWriter)(object)writer).Write(state.BlockPos.x);
        ((BinaryWriter)(object)writer).Write(state.BlockPos.y);
        ((BinaryWriter)(object)writer).Write(state.BlockPos.z);
        ((BinaryWriter)(object)writer).Write(state.SessionId ?? "");
        ((BinaryWriter)(object)writer).Write(state.BridgeEndpoint ?? "");
        ((BinaryWriter)(object)writer).Write(state.StreamUrl ?? "");
        ((BinaryWriter)(object)writer).Write(state.ViewerToken ?? "");
        ((BinaryWriter)(object)writer).Write(state.ControllerToken ?? "");
        ((BinaryWriter)(object)writer).Write(state.ControllerEntityId);
        ((BinaryWriter)(object)writer).Write(state.CurrentUrl ?? "");
        ((BinaryWriter)(object)writer).Write(state.Volume);
        ((BinaryWriter)(object)writer).Write(state.StatusText ?? "");
        ((BinaryWriter)(object)writer).Write(state.Revision);
    }

    public override void ProcessPackage(World world, GameManager callbacks)
    {
        if (GameManager.IsDedicatedServer)
        {
            return;
        }

        BrowserTvClientStateService.ApplyState(state);
    }

    public override int GetLength()
    {
        return 128 + StringLen(state.SessionId) + StringLen(state.BridgeEndpoint) + StringLen(state.StreamUrl) + StringLen(state.ViewerToken) + StringLen(state.ControllerToken) + StringLen(state.CurrentUrl) + StringLen(state.StatusText);
    }

    private static int StringLen(string value)
    {
        return (value?.Length ?? 0) * 2 + 4;
    }
}
