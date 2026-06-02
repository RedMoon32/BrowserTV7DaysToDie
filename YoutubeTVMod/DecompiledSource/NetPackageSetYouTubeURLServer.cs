using System;
using System.IO;
using Platform.EOS;
using Platform.Local;
using Platform.PSN;
using Platform.Steam;
using Platform.XBL;

public class NetPackageSetYouTubeURLServer : NetPackage
{
	private Vector3i blockPos;

	private string newURL;

	private PlatformUserIdentifierAbs userID;

	private int entityId;

	public override NetPackageDirection PackageDirection => (NetPackageDirection)1;

	public NetPackageSetYouTubeURLServer Setup(Vector3i _blockPos, string _newURL, PlatformUserIdentifierAbs _userID, int _entityId)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		blockPos = _blockPos;
		newURL = _newURL;
		userID = _userID;
		entityId = _entityId;
		return this;
	}

	public override void read(PooledBinaryReader _reader)
	{
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		blockPos = new Vector3i(((BinaryReader)(object)_reader).ReadInt32(), ((BinaryReader)(object)_reader).ReadInt32(), ((BinaryReader)(object)_reader).ReadInt32());
		newURL = ((BinaryReader)(object)_reader).ReadString();
		try
		{
			userID = PlatformUserIdentifierAbs.FromStream((BinaryReader)(object)_reader, false, false);
		}
		catch (Exception ex)
		{
			Log.Warning("NetPackageSetYouTubeURLServer: Error reading PlatformUserIdentifierAbs: " + ex.Message);
			userID = null;
		}
		entityId = ((BinaryReader)(object)_reader).ReadInt32();
	}

	public override void write(PooledBinaryWriter _writer)
	{
		base.write(_writer);
		((BinaryWriter)(object)_writer).Write(blockPos.x);
		((BinaryWriter)(object)_writer).Write(blockPos.y);
		((BinaryWriter)(object)_writer).Write(blockPos.z);
		((BinaryWriter)(object)_writer).Write(newURL ?? "");
		PlatformUserIdentifierExtensions.ToStream(userID, (BinaryWriter)(object)_writer, false);
		((BinaryWriter)(object)_writer).Write(entityId);
	}

	public override void ProcessPackage(World _world, GameManager _callbacks)
	{
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		if (_world == null || !SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer)
		{
			return;
		}
		if (!(((WorldBase)_world).GetTileEntity(0, blockPos) is TileEntityYouTubeTV tileEntityYouTubeTV))
		{
			Log.Warning($"NetPackageSetYouTubeURLServer: TileEntityYouTubeTV not found at {blockPos} on server.");
		}
		else if (((NetPackage)this).ValidEntityIdForSender(entityId, false))
		{
			if (userID == null)
			{
				userID = ((NetPackage)this).Sender?.PlatformId ?? ((NetPackage)this).Sender?.CrossplatformId;
			}
			tileEntityYouTubeTV.ServerSetYouTubeURL(newURL, userID, entityId);
		}
	}

	public override int GetLength()
	{
		int num = 20;
		if (userID is UserIdentifierLocal)
		{
			num = 1;
		}
		else if (userID is UserIdentifierSteam)
		{
			num = 12;
		}
		else if (userID is UserIdentifierEos)
		{
			num = 36;
		}
		else if (userID is UserIdentifierPSN)
		{
			num = 24;
		}
		else if (userID is UserIdentifierXbl)
		{
			num = 24;
		}
		return 12 + (newURL?.Length ?? 0) * 2 + num + 4 + 4;
	}

	public int GetPackageId()
	{
		return NetPackageManager.GetPackageId(((object)this).GetType());
	}
}
