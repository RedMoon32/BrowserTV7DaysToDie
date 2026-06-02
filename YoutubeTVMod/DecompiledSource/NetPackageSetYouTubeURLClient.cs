using System.IO;

public class NetPackageSetYouTubeURLClient : NetPackage
{
	private Vector3i blockPos;

	private string newURL;

	private bool isPowered;

	public override NetPackageDirection PackageDirection => (NetPackageDirection)2;

	public NetPackageSetYouTubeURLClient Setup(Vector3i _blockPos, string _newURL, bool _isPowered)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		blockPos = _blockPos;
		newURL = _newURL;
		isPowered = _isPowered;
		return this;
	}

	public override void read(PooledBinaryReader _reader)
	{
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		blockPos = new Vector3i(((BinaryReader)(object)_reader).ReadInt32(), ((BinaryReader)(object)_reader).ReadInt32(), ((BinaryReader)(object)_reader).ReadInt32());
		newURL = ((BinaryReader)(object)_reader).ReadString();
		isPowered = ((BinaryReader)(object)_reader).ReadBoolean();
	}

	public override void write(PooledBinaryWriter _writer)
	{
		base.write(_writer);
		((BinaryWriter)(object)_writer).Write(blockPos.x);
		((BinaryWriter)(object)_writer).Write(blockPos.y);
		((BinaryWriter)(object)_writer).Write(blockPos.z);
		((BinaryWriter)(object)_writer).Write(newURL ?? "");
		((BinaryWriter)(object)_writer).Write(isPowered);
	}

	public override void ProcessPackage(World _world, GameManager _callbacks)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		if (_world != null && !GameManager.IsDedicatedServer)
		{
			if (!(((WorldBase)_world).GetTileEntity(0, blockPos) is TileEntityYouTubeTV tileEntityYouTubeTV))
			{
				Log.Warning($"NetPackageSetYouTubeURLClient: TileEntityYouTubeTV not found at {blockPos} on client.");
			}
			else
			{
				tileEntityYouTubeTV.ClientUpdateURLAndPower(newURL, isPowered);
			}
		}
	}

	public override int GetLength()
	{
		return 12 + (newURL?.Length ?? 0) * 2 + 1 + 4;
	}

	public int GetPackageId()
	{
		return NetPackageManager.GetPackageId(((object)this).GetType());
	}
}
