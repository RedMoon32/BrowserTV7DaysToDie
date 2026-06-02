using System.IO;

public class NetPackageSetVolumeClient : NetPackage
{
	private Vector3i blockPos;

	private float volumeLevel;

	public override NetPackageDirection PackageDirection => (NetPackageDirection)2;

	public NetPackageSetVolumeClient Setup(Vector3i _blockPos, float _volumeLevel)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		blockPos = _blockPos;
		volumeLevel = _volumeLevel;
		return this;
	}

	public override void read(PooledBinaryReader _reader)
	{
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		blockPos = new Vector3i(((BinaryReader)(object)_reader).ReadInt32(), ((BinaryReader)(object)_reader).ReadInt32(), ((BinaryReader)(object)_reader).ReadInt32());
		volumeLevel = ((BinaryReader)(object)_reader).ReadSingle();
	}

	public override void write(PooledBinaryWriter _writer)
	{
		base.write(_writer);
		((BinaryWriter)(object)_writer).Write(blockPos.x);
		((BinaryWriter)(object)_writer).Write(blockPos.y);
		((BinaryWriter)(object)_writer).Write(blockPos.z);
		((BinaryWriter)(object)_writer).Write(volumeLevel);
	}

	public override void ProcessPackage(World _world, GameManager _callbacks)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		if (_world != null && !GameManager.IsDedicatedServer)
		{
			if (!(((WorldBase)_world).GetTileEntity(0, blockPos) is TileEntityYouTubeTV tileEntityYouTubeTV))
			{
				Log.Warning($"NetPackageSetVolumeClient: TileEntityYouTubeTV not found at {blockPos} on client.");
			}
			else
			{
				tileEntityYouTubeTV.ClientSetVolume(volumeLevel);
			}
		}
	}

	public override int GetLength()
	{
		return 20;
	}

	public int GetPackageId()
	{
		return NetPackageManager.GetPackageId(((object)this).GetType());
	}
}
