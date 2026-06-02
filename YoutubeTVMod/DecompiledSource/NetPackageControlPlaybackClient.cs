using System.IO;

public class NetPackageControlPlaybackClient : NetPackage
{
	private Vector3i blockPos;

	private PlaybackCommand command;

	private double serverTime;

	private string currentUrl;

	private bool isPlaying;

	private bool isLooping;

	public override NetPackageDirection PackageDirection => (NetPackageDirection)2;

	public NetPackageControlPlaybackClient Setup(Vector3i _blockPos, PlaybackCommand _command, string _url, bool _isPlaying, double _serverTime, bool _isLooping = false)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		blockPos = _blockPos;
		command = _command;
		currentUrl = _url;
		isPlaying = _isPlaying;
		serverTime = _serverTime;
		isLooping = _isLooping;
		return this;
	}

	public override void read(PooledBinaryReader _reader)
	{
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		blockPos = new Vector3i(((BinaryReader)(object)_reader).ReadInt32(), ((BinaryReader)(object)_reader).ReadInt32(), ((BinaryReader)(object)_reader).ReadInt32());
		command = (PlaybackCommand)((BinaryReader)(object)_reader).ReadByte();
		currentUrl = ((BinaryReader)(object)_reader).ReadString();
		isPlaying = ((BinaryReader)(object)_reader).ReadBoolean();
		serverTime = ((BinaryReader)(object)_reader).ReadDouble();
		isLooping = ((BinaryReader)(object)_reader).ReadBoolean();
	}

	public override void write(PooledBinaryWriter _writer)
	{
		base.write(_writer);
		((BinaryWriter)(object)_writer).Write(blockPos.x);
		((BinaryWriter)(object)_writer).Write(blockPos.y);
		((BinaryWriter)(object)_writer).Write(blockPos.z);
		((BinaryWriter)(object)_writer).Write((byte)command);
		((BinaryWriter)(object)_writer).Write(currentUrl ?? "");
		((BinaryWriter)(object)_writer).Write(isPlaying);
		((BinaryWriter)(object)_writer).Write(serverTime);
		((BinaryWriter)(object)_writer).Write(isLooping);
	}

	public override void ProcessPackage(World _world, GameManager _callbacks)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		if (_world != null && !GameManager.IsDedicatedServer)
		{
			if (!(((WorldBase)_world).GetTileEntity(0, blockPos) is TileEntityYouTubeTV tileEntityYouTubeTV))
			{
				Log.Warning($"NetPackageControlPlaybackClient: TileEntityYouTubeTV not found at {blockPos} on client.");
			}
			else
			{
				tileEntityYouTubeTV.ClientControlPlayback(command, currentUrl, isPlaying, serverTime, isLooping);
			}
		}
	}

	public override int GetLength()
	{
		return 13 + (currentUrl?.Length ?? 0) * 2 + 1 + 8 + 1 + 4;
	}

	public int GetPackageId()
	{
		return NetPackageManager.GetPackageId(((object)this).GetType());
	}
}
