using System;
using Audio;
using GUI_2;
using InControl;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

public class ItemActionYouTubeTVVolume : ItemAction
{
	public class ItemActionYouTubeTVData : ItemActionRanged.ItemActionDataRanged
	{
		public ItemActionYouTubeTVData(ItemInventoryData _invData, int _indexInEntityOfAction)
			: base(_invData, _indexInEntityOfAction)
		{
		}

		public ItemActionData CreateItemActionData(ItemInventoryData _invData, int _indexInEntityOfAction)
		{
			return (ItemActionData)(object)new ItemActionYouTubeTVData(_invData, _indexInEntityOfAction);
		}
	}

	public class RadialContextRemote : XUiC_Radial.RadialContextAbs
	{
		public readonly ItemActionYouTubeTVVolume RemoteAction;

		public RadialContextRemote(ItemActionYouTubeTVVolume _remoteAction)
		{
			RemoteAction = _remoteAction;
		}
	}

	private float volumeStep = 0.1f;

	private float volumeDisplayDuration = 2f;

	public PlatformUserIdentifierAbs PrimaryId { get; set; }

	private YouTubeTVController FindTVController(EntityPlayerLocal player)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		Vector3 position = ((Entity)player).GetPosition();
		YouTubeTVController obj = YouTubeTVManager.Instance?.GetNearestTVController(position, 20f);
		if (!((Object)(object)obj != (Object)null) && (Object)(object)YouTubeTVManager.Instance != (Object)null)
		{
			YouTubeTVManager.Instance.GetRegisteredTVCount();
		}
		return obj;
	}

	public override void SetupRadial(XUiC_Radial _xuiRadialWindow, EntityPlayerLocal _epl)
	{
		//IL_008c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0098: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c0: Expected O, but got Unknown
		//IL_00c0: Expected O, but got Unknown
		//IL_00c0: Expected O, but got Unknown
		Debug.Log((object)"[YouTubeTVMod] Remote: SetupRadial called!");
		_xuiRadialWindow.ResetRadialEntries();
		_xuiRadialWindow.CreateRadialEntry(0, "ui_game_symbol_volume_up", "UIAtlas", "", "Volume Up", false);
		_xuiRadialWindow.CreateRadialEntry(1, "ui_game_symbol_volume_down", "UIAtlas", "", "Volume Down", false);
		_xuiRadialWindow.CreateRadialEntry(2, "ui_game_symbol_computer", "UIAtlas", "", "Set YouTube URL", false);
		_xuiRadialWindow.CreateRadialEntry(3, "ui_game_symbol_play", "UIAtlas", "", "Play/Pause", false);
		_xuiRadialWindow.SetCommonData(UIUtils.GetButtonIconForAction(_epl.playerInput.Activate), new XUiC_Radial.CommandHandlerDelegate(handleRadialCommand), (XUiC_Radial.RadialContextAbs)new XUiC_Radial.RadialContextHoldingSlotIndex(((EntityAlive)_epl).inventory.holdingItemIdx), -1, false, new XUiC_Radial.RadialStillValidDelegate(XUiC_Radial.RadialValidSameHoldingSlotIndex));
	}

	private TileEntityYouTubeTV RaycastForYouTubeTV(EntityPlayerLocal player)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		Ray lookRay = ((EntityAlive)player).GetLookRay();
		if (Voxel.Raycast(((Entity)player).world, lookRay, 200f, -555528213, 0f) && Voxel.voxelRayHitInfo.bHitValid && ((WorldBase)((Entity)player).world).GetTileEntity(0, Voxel.voxelRayHitInfo.hit.blockPos) is TileEntityYouTubeTV result)
		{
			return result;
		}
		return null;
	}

		public void handleRadialCommand(XUiC_Radial _sender, int _commandIndex, XUiC_Radial.RadialContextAbs _context)
	{
		//IL_011c: Unknown result type (might be due to invalid IL or missing references)
		if (!(_context is RadialContextRemote radialContextRemote))
		{
			return;
		}
		EntityPlayerLocal entityPlayer = ((XUiController)_sender).xui.playerUI.entityPlayer;
		ItemAction holdingPrimary = ((EntityAlive)entityPlayer).inventory.GetHoldingPrimary();
		Debug.Log((object)("[YouTubeTVMod] Remote: handleRadialCommand - holdingItemAction: " + ((object)holdingPrimary)?.GetType().Name + ", remoteContext.RemoteAction: " + ((object)radialContextRemote.RemoteAction)?.GetType().Name));
		if (holdingPrimary != radialContextRemote.RemoteAction)
		{
			Debug.Log((object)"[YouTubeTVMod] Remote: Item action mismatch, returning");
			return;
		}
		switch (_commandIndex)
		{
		case 0:
		case 1:
		{
			YouTubeTVController youTubeTVController = FindTVController(entityPlayer);
			if ((Object)(object)youTubeTVController == (Object)null)
			{
				GameManager.ShowTooltip(entityPlayer, "No YouTube TV nearby", false, false, 0f);
				break;
			}
			if (_commandIndex == 0)
			{
				youTubeTVController.AdjustVolume(volumeStep);
			}
			else
			{
				youTubeTVController.AdjustVolume(0f - volumeStep);
			}
			Manager.BroadcastPlay((Entity)(object)entityPlayer, "ui_denied", false);
			GameManager.ShowTooltip(entityPlayer, $"Volume: {Mathf.RoundToInt(youTubeTVController.GetCurrentVolume() * 100f)}%", false, false, 0f);
			break;
		}
		case 2:
		{
			TileEntityYouTubeTV tileEntityYouTubeTV = RaycastForYouTubeTV(entityPlayer);
			if (tileEntityYouTubeTV != null)
			{
				OpenURLInputDialog(entityPlayer, ((TileEntity)tileEntityYouTubeTV).ToWorldPos());
			}
			else
			{
				GameManager.ShowTooltip(entityPlayer, "No YouTube TV in sight.", false, false, 0f);
			}
			break;
		}
		case 3:
			GameManager.ShowTooltip(entityPlayer, "Play/Pause not implemented yet", false, false, 0f);
			break;
		}
	}

	public override void ExecuteAction(ItemActionData _actionData, bool _bReleased)
	{
		if (_bReleased)
		{
			return;
		}
		EntityAlive holdingEntity = _actionData.invData.holdingEntity;
		EntityPlayerLocal val = (EntityPlayerLocal)(object)((holdingEntity is EntityPlayerLocal) ? holdingEntity : null);
		if (val == null)
		{
			return;
		}
		YouTubeTVController youTubeTVController = FindTVController(val);
		if ((Object)(object)youTubeTVController == (Object)null)
		{
			GameManager.ShowTooltip(val, "No YouTube TV nearby", false, false, 0f);
			return;
		}
		if (((OneAxisInputControl)val.playerInput.Primary).WasPressed)
		{
			youTubeTVController.AdjustVolume(volumeStep);
			Manager.BroadcastPlay((Entity)(object)val, "ui_denied", false);
			GameManager.ShowTooltip(val, $"Volume: {Mathf.RoundToInt(youTubeTVController.GetCurrentVolume() * 100f)}%", false, false, 0f);
		}
		if (((OneAxisInputControl)val.playerInput.Secondary).WasPressed)
		{
			youTubeTVController.AdjustVolume(0f - volumeStep);
			Manager.BroadcastPlay((Entity)(object)val, "ui_denied", false);
			GameManager.ShowTooltip(val, $"Volume: {Mathf.RoundToInt(youTubeTVController.GetCurrentVolume() * 100f)}%", false, false, 0f);
		}
	}

	public override void OnHoldingUpdate(ItemActionData _actionData)
	{
		EntityAlive holdingEntity = _actionData.invData.holdingEntity;
		EntityPlayerLocal val = (EntityPlayerLocal)(object)((holdingEntity is EntityPlayerLocal) ? holdingEntity : null);
		if (val != null && ((EntityAlive)val).inventory.holdingItemData == _actionData.invData && ((OneAxisInputControl)val.playerInput.Secondary).WasPressed)
		{
			YouTubeTVController youTubeTVController = FindTVController(val);
			if ((Object)(object)youTubeTVController != (Object)null)
			{
				youTubeTVController.AdjustVolume(0f - volumeStep);
				Manager.BroadcastPlay((Entity)(object)val, "ui_denied", false);
				GameManager.ShowTooltip(val, $"Volume: {Mathf.RoundToInt(youTubeTVController.GetCurrentVolume() * 100f)}%", false, false, 0f);
			}
			else
			{
				GameManager.ShowTooltip(val, "No YouTube TV nearby", false, false, 0f);
			}
		}
	}

	public Vector3i GetBlockPos(YouTubeTVController tvController)
	{
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)tvController != (Object)null)
		{
			return tvController.GetBlockPosition();
		}
		return Vector3i.zero;
	}

	private void OpenURLInputDialog(EntityPlayerLocal player, Vector3i blockPos)
	{
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)player == (Object)null || blockPos == Vector3i.zero)
		{
			return;
		}
		try
		{
			World world = GameManager.Instance.World;
			TileEntityYouTubeTV tileEntity = ((WorldBase)world).GetTileEntity(0, blockPos) as TileEntityYouTubeTV;
			if (tileEntity == null)
			{
				GameManager.ShowTooltip(player, "TV not found", false, false, 0f);
				return;
			}
			XUi xui = player.PlayerUI.xui;
			xui.playerUI.windowManager.Open("windowYouTubeURLInput", true, false, true);
			XUiV_Window window = xui.GetWindow("windowYouTubeURLInput");
			if (((window != null) ? ((XUiView)window).Controller : null) is XUiC_InputWindow xUiC_InputWindow)
			{
				xUiC_InputWindow.TileEntity = tileEntity;
				xUiC_InputWindow.OnUrlEntered += delegate(string url)
				{
					HandleURLEntered(player, tileEntity, url);
				};
				Manager.BroadcastPlay((Entity)(object)player, "ui_open", false);
			}
			else
			{
				GameManager.ShowTooltip(player, "URL input window not available", false, false, 0f);
			}
		}
		catch (Exception ex)
		{
			Log.Error("[YouTubeTVMod] Remote: Exception opening URL input dialog: " + ex.Message);
			GameManager.ShowTooltip(player, "Error opening URL dialog", false, false, 0f);
		}
	}

	private void HandleURLEntered(EntityPlayerLocal player, TileEntityYouTubeTV tileEntity, string url)
	{
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)player == (Object)null || tileEntity == null)
		{
			Log.Warning("[YouTubeTVMod] Remote: Cannot handle URL entry - invalid player or tile entity.");
			return;
		}
		try
		{
			Debug.Log((object)$"[YouTubeTVMod] Remote: URL entered via remote: '{url}' for TV at {((TileEntity)tileEntity).ToWorldPos()}");
			PersistentPlayerData playerDataFromEntityID = GameManager.Instance.persistentPlayers.GetPlayerDataFromEntityID(((Entity)player).entityId);
			PlatformUserIdentifierAbs userID = ((playerDataFromEntityID != null) ? playerDataFromEntityID.PrimaryId : null);
			tileEntity.RequestSetYouTubeURL(url, userID, ((Entity)player).entityId);
			if (string.IsNullOrEmpty(url))
			{
				GameManager.ShowTooltip(player, "TV URL cleared", false, false, 0f);
			}
			else
			{
				GameManager.ShowTooltip(player, "TV URL set", false, false, 0f);
			}
			Manager.BroadcastPlay((Entity)(object)player, "ui_accepted", false);
		}
		catch (Exception ex)
		{
			Log.Error("[YouTubeTVMod] Remote: Exception handling URL entry: " + ex.Message + "\n" + ex.StackTrace);
			GameManager.ShowTooltip(player, "Error setting URL", false, false, 0f);
		}
	}

	public override bool HasRadial()
	{
		Debug.Log((object)"[YouTubeTVMod] Remote: HasRadial called - returning true");
		return true;
	}

	public override void ReadFrom(DynamicProperties _props)
	{
		base.ReadFrom(_props);
		_props.ParseFloat("VolumeStep", ref volumeStep);
		volumeStep = Mathf.Clamp(volumeStep, 0.01f, 0.5f);
		_props.ParseFloat("VolumeDisplayDuration", ref volumeDisplayDuration);
		volumeDisplayDuration = Mathf.Clamp(volumeDisplayDuration, 0.5f, 10f);
	}
}
