using System;
using System.Collections.Generic;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;
using UnityEngine.Scripting;

[Preserve]
public class BlockYouTubeTV : BlockPowered
{
	private YouTubeTVManager youtubeManager = YouTubeTVManager.Instance;

	public BlockYouTubeTV()
	{
		((Block)this).HasTileEntity = true;
	}

	public override void Init()
	{
		base.Init();
	}

	public override bool UpdateTick(WorldBase _world, int _clrIdx, Vector3i _blockPos, BlockValue _blockValue, bool _bRandomTick, ulong _ticksIfLoaded, GameRandom _rnd)
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
			if (!_blockValue.ischild)
		{
			return false;
		}
		TileEntityYouTubeTV tileEntityYouTubeTV = _world.GetTileEntity(_clrIdx, _blockPos) as TileEntityYouTubeTV;
			if (_blockValue.ischild && GameManager.Instance.gamePaused && youtubeManager != null)
		{
			youtubeManager.GetController(_blockPos).videoPlayer.Pause();
		}
		if (_ticksIfLoaded % 100 == 0L)
		{
			if (tileEntityYouTubeTV == null)
			{
				return false;
			}
				if (_blockValue.ischild && youtubeManager != null && !((TileEntityPowered)tileEntityYouTubeTV).isPowered)
			{
				youtubeManager.GetController(_blockPos).videoPlayer.Stop();
			}
		}
		return false;
	}

	private TileEntityYouTubeTV CreateTileEntity(Chunk chunk)
	{
		return new TileEntityYouTubeTV(chunk);
	}

	public override void OnBlockAdded(WorldBase world, Chunk _chunk, Vector3i _blockPos, BlockValue _blockValue, PlatformUserIdentifierAbs _addedByPlayer)
	{
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_00af: Unknown result type (might be due to invalid IL or missing references)
		//IL_009b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		if (_blockValue.ischild)
		{
			base.OnBlockAdded(world, _chunk, _blockPos, _blockValue, _addedByPlayer);
			return;
		}
		if (world.GetTileEntity(_chunk.ClrIdx, _blockPos) == null)
		{
			try
			{
				TileEntityYouTubeTV tileEntityYouTubeTV = CreateTileEntity(_chunk);
				if (tileEntityYouTubeTV != null)
				{
					((TileEntity)tileEntityYouTubeTV).localChunkPos = World.toBlock(_blockPos);
					_chunk.AddTileEntity((TileEntity)(object)tileEntityYouTubeTV);
					Debug.Log((object)$"BlockYouTubeTV.OnBlockAdded: Successfully created TileEntityYouTubeTV at {_blockPos}");
				}
				else
				{
					Log.Error($"BlockYouTubeTV.OnBlockAdded: CreateTileEntity returned null for {_blockPos}.");
				}
			}
			catch (Exception ex)
			{
				Log.Error($"BlockYouTubeTV.OnBlockAdded: Exception creating TileEntityYouTubeTV at {_blockPos}: {ex.Message}\n{ex.StackTrace}");
			}
		}
		else
		{
			Debug.Log((object)$"BlockYouTubeTV.OnBlockAdded: TileEntity already exists at {_blockPos}.");
		}
		base.OnBlockAdded(world, _chunk, _blockPos, _blockValue, _addedByPlayer);
	}

	public override void OnBlockEntityTransformAfterActivated(WorldBase _world, Vector3i _blockPos, int _cIdx, BlockValue _blockValue, BlockEntityData _ebcd)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0004: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ca: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		//IL_006e: Expected O, but got Unknown
		//IL_00ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_007d: Unknown result type (might be due to invalid IL or missing references)
		//IL_007e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0094: Unknown result type (might be due to invalid IL or missing references)
		base.OnBlockEntityTransformAfterActivated(_world, _blockPos, _cIdx, _blockValue, _ebcd);
		if (_ebcd == null || (Object)(object)_ebcd.transform == (Object)null)
		{
			Log.Warning($"BlockYouTubeTV.OnBlockEntityTransformAfterActivated: BlockEntityData or transform is null for {_blockPos}.");
			return;
		}
		TileEntityYouTubeTV tileEntityYouTubeTV = _world.GetTileEntity(_cIdx, _blockPos) as TileEntityYouTubeTV;
		if (tileEntityYouTubeTV == null)
		{
			Log.Error($"BlockYouTubeTV.OnBlockEntityTransformAfterActivated: TileEntityYouTubeTV not found at {_blockPos}! Attempting safeguard.");
			Chunk val = (Chunk)((WorldBase)(World)_world).GetChunkFromWorldPos(_blockPos);
			if (val != null)
			{
				tileEntityYouTubeTV = CreateTileEntity(val);
				if (tileEntityYouTubeTV != null)
				{
					((TileEntity)tileEntityYouTubeTV).localChunkPos = World.toBlock(_blockPos);
					val.AddTileEntity((TileEntity)(object)tileEntityYouTubeTV);
					Debug.Log((object)$"BlockYouTubeTV.OnBlockEntityTransformAfterActivated: Safeguard created TileEntityYouTubeTV at {_blockPos}");
				}
			}
			if (tileEntityYouTubeTV == null)
			{
				Log.Error($"BlockYouTubeTV.OnBlockEntityTransformAfterActivated: Safeguard FAILED for TE at {_blockPos}.");
				return;
			}
		}
		tileEntityYouTubeTV.SetBlockEntityData(_ebcd);
		Debug.Log((object)$"BlockYouTubeTV.OnBlockEntityTransformAfterActivated: Called tileEntity.SetBlockEntityData for {_blockPos}.");
	}

	public override string GetActivationText(WorldBase _world, BlockValue _blockValue, int _clrIdx, Vector3i _blockPos, EntityAlive _entityFocusing)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		if (_world.GetTileEntity(_clrIdx, _blockPos) is TileEntityYouTubeTV tileEntityYouTubeTV && ((TileEntityPowered)tileEntityYouTubeTV).IsPowered)
		{
			return "Press Interact to enter a YouTube URL";
		}
		return "TV requires power";
	}

	public override bool OnBlockActivated(string _commandName, WorldBase _world, int _cIdx, Vector3i _blockPos, BlockValue _blockValue, EntityPlayerLocal _player)
	{
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_0355: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b9: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f8: Expected O, but got Unknown
		//IL_0200: Unknown result type (might be due to invalid IL or missing references)
		//IL_020a: Expected O, but got Unknown
		//IL_02c0: Unknown result type (might be due to invalid IL or missing references)
		TileEntityYouTubeTV te = _world.GetTileEntity(_cIdx, _blockPos) as TileEntityYouTubeTV;
		if (te == null)
		{
			Log.Warning($"BlockYouTubeTV.OnBlockActivated: TileEntityYouTubeTV not found at {_blockPos} for command '{_commandName}'.");
			return false;
		}
		if (_commandName == "take")
		{
			((BlockPowered)this).TakeItemWithTimer(_cIdx, _blockPos, _blockValue, (EntityAlive)(object)_player);
			return true;
		}
		if (_commandName == "edit" || string.IsNullOrEmpty(_commandName))
		{
			if (!((TileEntityPowered)te).IsPowered)
			{
				GameManager.ShowTooltip(_player, Localization.Get("ttTVRequiresPowerToEdit", false), string.Empty, "ui_denied", (ToolTipEvent)null, false, false, 0f);
				return true;
			}
			try
			{
				GameManager.Instance.TELockServer(_cIdx, _blockPos, ((TileEntity)te).EntityId, ((Entity)_player).entityId, (string)null);
				((TileEntity)te).SetUserAccessing(true);
				string inputWindowName = "windowYouTubeURLInput";
				string text = "textInputYouTubeURL";
				bool flag = false;
				XUiV_Window window = _player.PlayerUI.xui.GetWindow(inputWindowName);
				if (window == null)
				{
					Log.Warning("BlockYouTubeTV: Window '" + inputWindowName + "' not found. Ensure it's defined in XUi.xml.");
				}
				else if (!(((XUiView)window).Controller is XUiC_InputWindow xUiC_InputWindow))
				{
					Log.Warning("BlockYouTubeTV: Controller for window '" + inputWindowName + "' is null or not of type XUiC_InputWindow.");
				}
				else
				{
					xUiC_InputWindow.TileEntity = te;
					XUiC_TextInput textInputController = default(XUiC_TextInput);
					ref XUiC_TextInput reference = ref textInputController;
					XUiController childById = ((XUiController)xUiC_InputWindow).GetChildById(text);
					reference = (XUiC_TextInput)(object)((childById is XUiC_TextInput) ? childById : null);
					if (textInputController == null)
					{
						Log.Warning("BlockYouTubeTV: XUiC_TextInput '" + text + "' not found in window '" + inputWindowName + "'.");
					}
					else
					{
						XUiEvent_InputOnSubmitEventHandler onSubmit = null;
						XUiEvent_InputOnAbortedEventHandler onAbort = null;
						Action cleanupAndUnlock = delegate
						{
							//IL_002f: Unknown result type (might be due to invalid IL or missing references)
							if (((TileEntity)te).IsUserAccessing())
							{
								((TileEntity)te).SetUserAccessing(false);
								GameManager.Instance.TEUnlockServer(((TileEntity)te).GetClrIdx(), ((TileEntity)te).ToWorldPos(), ((TileEntity)te).EntityId, true);
							}
							if (onSubmit != null)
							{
								textInputController.OnSubmitHandler -= onSubmit;
							}
							if (onAbort != null)
							{
								textInputController.OnInputAbortedHandler -= onAbort;
							}
						};
						onSubmit = (XUiEvent_InputOnSubmitEventHandler)delegate(XUiController sender, string newText)
						{
							PersistentPlayerData playerDataFromEntityID = GameManager.Instance.persistentPlayers.GetPlayerDataFromEntityID(((Entity)_player).entityId);
							PlatformUserIdentifierAbs userID = ((playerDataFromEntityID != null) ? playerDataFromEntityID.PrimaryId : null);
							te.RequestSetYouTubeURL(newText, userID, ((Entity)_player).entityId);
							_player.PlayerUI.windowManager.Close(inputWindowName);
							cleanupAndUnlock();
						};
						onAbort = (XUiEvent_InputOnAbortedEventHandler)delegate
						{
							_player.PlayerUI.windowManager.Close(inputWindowName);
							cleanupAndUnlock();
						};
						textInputController.OnSubmitHandler += onSubmit;
						textInputController.OnInputAbortedHandler += onAbort;
						_player.PlayerUI.windowManager.Open(inputWindowName, true, false, true);
						textInputController.SetSelected(true, false);
						flag = true;
						Debug.Log((object)("BlockYouTubeTV: Opened window '" + inputWindowName + "' with text input '" + text + "'."));
					}
				}
				if (!flag)
				{
					if (((TileEntity)te).IsUserAccessing())
					{
						((TileEntity)te).SetUserAccessing(false);
						GameManager.Instance.TEUnlockServer(((TileEntity)te).GetClrIdx(), ((TileEntity)te).ToWorldPos(), ((TileEntity)te).EntityId, true);
					}
					Log.Warning("BlockYouTubeTV: Failed to configure or open '" + inputWindowName + "'. The 'edit' action will still be consumed.");
					return true;
				}
				return true;
			}
			catch (Exception ex)
			{
				Log.Error("BlockYouTubeTV.OnBlockActivated: Exception handling edit command: " + ex.Message + "\n" + ex.StackTrace);
				if (te != null && ((TileEntity)te).IsUserAccessing())
				{
					((TileEntity)te).SetUserAccessing(false);
					GameManager.Instance.TEUnlockServer(((TileEntity)te).GetClrIdx(), ((TileEntity)te).ToWorldPos(), ((TileEntity)te).EntityId, true);
				}
				return false;
			}
		}
		return false;
	}

	public override bool OnBlockActivated(WorldBase _world, int _cIdx, Vector3i _blockPos, BlockValue _blockValue, EntityPlayerLocal _player)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		return ((Block)this).OnBlockActivated("", _world, _cIdx, _blockPos, _blockValue, _player);
	}

	public override BlockActivationCommand[] GetBlockActivationCommands(WorldBase _world, BlockValue _blockValue, int _clrIdx, Vector3i _blockPos, EntityAlive _entityFocusing)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		if (!(_world.GetTileEntity(_clrIdx, _blockPos) is TileEntityYouTubeTV tileEntityYouTubeTV))
		{
			return BlockActivationCommand.Empty;
		}
		List<BlockActivationCommand> list = new List<BlockActivationCommand>
		{
			new BlockActivationCommand("take", "hand", true, false, (string)null)
		};
		if (((TileEntityPowered)tileEntityYouTubeTV).IsPowered)
		{
			list.Insert(0, new BlockActivationCommand("edit", "pen", true, false, (string)null));
		}
		return list.ToArray();
	}

	public override void OnBlockRemoved(WorldBase world, Chunk _chunk, Vector3i _blockPos, BlockValue _blockValue)
	{
		//IL_0073: Unknown result type (might be due to invalid IL or missing references)
		//IL_0074: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
			if (!_blockValue.ischild && (Object)(object)youtubeManager != (Object)null)
		{
			Debug.Log((object)$"BlockYouTubeTV.OnBlockRemoved: Cleaning up YouTube TV at {_blockPos}");
			YouTubeTVController controller = youtubeManager.GetController(_blockPos);
			if ((Object)(object)controller != (Object)null)
			{
				controller.StopAllPlayback();
				controller.SetScreenBlack(isBlack: true);
				Debug.Log((object)$"BlockYouTubeTV.OnBlockRemoved: Stopped all playback and set screen black for TV at {_blockPos}");
			}
			youtubeManager.UnregisterTV(_blockPos);
		}
		base.OnBlockRemoved(world, _chunk, _blockPos, _blockValue);
	}
}
