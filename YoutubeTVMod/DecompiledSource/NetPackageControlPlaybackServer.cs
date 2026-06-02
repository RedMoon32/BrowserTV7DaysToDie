using System.IO;

public class NetPackageControlPlaybackServer : NetPackage
{
	private Vector3i blockPos;

	private PlaybackCommand command;

	private double targetTime;

	private int entityId;

	private float volumeLevel;

	public override NetPackageDirection PackageDirection => (NetPackageDirection)1;

	public NetPackageControlPlaybackServer Setup(Vector3i _blockPos, PlaybackCommand _command, int _entityId, double _targetTime = 0.0, float _volumeLevel = 0f)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		blockPos = _blockPos;
		command = _command;
		entityId = _entityId;
		targetTime = _targetTime;
		volumeLevel = _volumeLevel;
		return this;
	}

	public override void read(PooledBinaryReader _reader)
	{
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		blockPos = new Vector3i(((BinaryReader)(object)_reader).ReadInt32(), ((BinaryReader)(object)_reader).ReadInt32(), ((BinaryReader)(object)_reader).ReadInt32());
		command = (PlaybackCommand)((BinaryReader)(object)_reader).ReadByte();
		entityId = ((BinaryReader)(object)_reader).ReadInt32();
		if (command == PlaybackCommand.Seek || command == PlaybackCommand.TogglePlayPause)
		{
			targetTime = ((BinaryReader)(object)_reader).ReadDouble();
		}
		if (command == PlaybackCommand.SetVolume)
		{
			volumeLevel = ((BinaryReader)(object)_reader).ReadSingle();
		}
	}

	public override void write(PooledBinaryWriter _writer)
	{
		base.write(_writer);
		((BinaryWriter)(object)_writer).Write(blockPos.x);
		((BinaryWriter)(object)_writer).Write(blockPos.y);
		((BinaryWriter)(object)_writer).Write(blockPos.z);
		((BinaryWriter)(object)_writer).Write((byte)command);
		((BinaryWriter)(object)_writer).Write(entityId);
		if (command == PlaybackCommand.Seek || command == PlaybackCommand.TogglePlayPause)
		{
			((BinaryWriter)(object)_writer).Write(targetTime);
		}
		if (command == PlaybackCommand.SetVolume)
		{
			((BinaryWriter)(object)_writer).Write(volumeLevel);
		}
	}

	public override void ProcessPackage(World _world, GameManager _callbacks)
	{
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		if (_world != null && SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer)
		{
			if (!(((WorldBase)_world).GetTileEntity(0, blockPos) is TileEntityYouTubeTV tileEntityYouTubeTV))
			{
				Log.Warning($"NetPackageControlPlaybackServer: TileEntityYouTubeTV not found at {blockPos} on server.");
			}
			else if (command == PlaybackCommand.SetVolume)
			{
				tileEntityYouTubeTV.ServerSetVolume(volumeLevel, entityId);
			}
			else
			{
				tileEntityYouTubeTV.ServerControlPlayback(command, entityId, targetTime);
			}
		}
	}

	public override int GetLength()
	{
		int num = 21;
		if (command == PlaybackCommand.Seek || command == PlaybackCommand.TogglePlayPause)
		{
			num += 8;
		}
		if (command == PlaybackCommand.SetVolume)
		{
			num += 4;
		}
		return num;
	}

	public int GetPackageId()
	{
		return NetPackageManager.GetPackageId(((object)this).GetType());
	}
}
