using System;
using System.Collections.Generic;
using UnityEngine;

public class BlockBrowserTV : BlockPowered
{
    public BlockBrowserTV()
    {
        HasTileEntity = true;
    }

    public override void OnBlockAdded(WorldBase world, Chunk chunk, Vector3i blockPos, BlockValue blockValue, PlatformUserIdentifierAbs addedByPlayer)
    {
        if (!blockValue.ischild && world.GetTileEntity(chunk.ClrIdx, blockPos) == null)
        {
            CreateTileEntity(chunk, blockPos);
        }

        base.OnBlockAdded(world, chunk, blockPos, blockValue, addedByPlayer);
    }

    public override void OnBlockEntityTransformAfterActivated(WorldBase world, Vector3i blockPos, int cIdx, BlockValue blockValue, BlockEntityData ebcd)
    {
        base.OnBlockEntityTransformAfterActivated(world, blockPos, cIdx, blockValue, ebcd);
        if (ebcd == null || ebcd.transform == null)
        {
            return;
        }

        TileEntityBrowserTV tileEntity = world.GetTileEntity(cIdx, blockPos) as TileEntityBrowserTV;
        if (tileEntity == null)
        {
            Chunk chunk = (Chunk)((World)world).GetChunkFromWorldPos(blockPos);
            tileEntity = CreateTileEntity(chunk, blockPos);
        }

        tileEntity?.SetBlockEntityData(ebcd);
    }

    public override string GetActivationText(WorldBase world, BlockValue blockValue, int clrIdx, Vector3i blockPos, EntityAlive entityFocusing)
    {
        TileEntityBrowserTV tileEntity = world.GetTileEntity(clrIdx, blockPos) as TileEntityBrowserTV;
        if (tileEntity != null && ((TileEntityPowered)tileEntity).IsPowered)
        {
            return "Use Browser TV";
        }

        return Localization.Get("ttBrowserTvRequiresPower", false);
    }

    public override bool OnBlockActivated(string commandName, WorldBase world, int cIdx, Vector3i blockPos, BlockValue blockValue, EntityPlayerLocal player)
    {
        TileEntityBrowserTV tileEntity = world.GetTileEntity(cIdx, blockPos) as TileEntityBrowserTV;
        if (tileEntity == null)
        {
            Debug.LogWarning("[BrowserTV] Missing TileEntityBrowserTV at " + blockPos);
            return false;
        }

        if (commandName == "take")
        {
            TakeItemWithTimer(cIdx, blockPos, blockValue, player);
            return true;
        }

        if (!((TileEntityPowered)tileEntity).IsPowered)
        {
            GameManager.ShowTooltip(player, Localization.Get("ttBrowserTvRequiresPower", false), string.Empty, "ui_denied", null, false, false, 0f);
            return true;
        }

        if (commandName == "edit" || string.IsNullOrEmpty(commandName))
        {
            return OpenUrlInput(world, cIdx, blockPos, tileEntity, player);
        }

        BrowserTvState current = BrowserTvClientStateService.Current;
        BrowserTvCommandType command = current.Power == BrowserTvPowerState.Off || !current.IsSameTv(blockPos)
            ? BrowserTvCommandType.PowerOn
            : BrowserTvCommandType.PowerOff;
        int entityId = ((Entity)player).entityId;

        if (SingletonMonoBehaviour<ConnectionManager>.Instance.IsClient)
        {
            SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(new BrowserTvCommandPackage().Setup(command, blockPos, BrowserTvConfig.Current.DefaultUrl, 0f, entityId), false);
        }
        else
        {
            BrowserTvServerStateService.HandleCommand(command, blockPos, BrowserTvConfig.Current.DefaultUrl, 0f, entityId);
        }

        GameManager.ShowTooltip(player, command == BrowserTvCommandType.PowerOn ? "Browser TV powering on" : "Browser TV powering off", string.Empty, "ui_game_symbol_computer", null, false, false, 0f);
        Debug.Log("[BrowserTV] Interaction command " + command + " at " + blockPos + " by player " + entityId);
        return true;
    }

    private static bool OpenUrlInput(WorldBase world, int cIdx, Vector3i blockPos, TileEntityBrowserTV tileEntity, EntityPlayerLocal player)
    {
        try
        {
            BrowserTvState current = BrowserTvClientStateService.Current;
            XUiC_BrowserTvUrlInputWindow.BlockPos = blockPos;
            XUiC_BrowserTvUrlInputWindow.CurrentUrl = current.IsSameTv(blockPos) ? current.CurrentUrl : BrowserTvConfig.Current.DefaultUrl;
            XUiC_BrowserTvUrlInputWindow.OnUrlEntered = url =>
            {
                BrowserTvState latest = BrowserTvClientStateService.Current;
                BrowserTvCommandType command = latest.Power == BrowserTvPowerState.Off || !latest.IsSameTv(blockPos)
                    ? BrowserTvCommandType.PowerOn
                    : BrowserTvCommandType.Navigate;
                int entityId = ((Entity)player).entityId;

                if (string.IsNullOrWhiteSpace(url))
                {
                    url = BrowserTvConfig.Current.DefaultUrl;
                }

                if (SingletonMonoBehaviour<ConnectionManager>.Instance.IsClient)
                {
                    SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(new BrowserTvCommandPackage().Setup(command, blockPos, url, 0f, entityId), false);
                }
                else
                {
                    BrowserTvServerStateService.HandleCommand(command, blockPos, url, 0f, entityId);
                }

                GameManager.ShowTooltip(player, command == BrowserTvCommandType.PowerOn ? "Browser TV powering on" : "Browser TV navigating", string.Empty, "ui_game_symbol_computer", null, false, false, 0f);
                Debug.Log("[BrowserTV] URL command " + command + " at " + blockPos + " by player " + entityId + ": " + url);
            };

            XUi xui = player.PlayerUI.xui;
            XUiV_Window window = xui.GetWindow("windowBrowserTvUrlInput");
            if (window == null || ((XUiView)window).Controller == null)
            {
                Debug.LogWarning("[BrowserTV] URL input window is not available; falling back to default URL.");
                int entityId = ((Entity)player).entityId;
                SendCommand(BrowserTvCommandType.PowerOn, blockPos, BrowserTvConfig.Current.DefaultUrl, entityId);
                return true;
            }

            player.PlayerUI.windowManager.Open("windowBrowserTvUrlInput", true, false, true);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError("[BrowserTV] Failed to open URL input: " + ex);
            GameManager.ShowTooltip(player, "Error opening Browser TV URL input", string.Empty, "ui_denied", null, false, false, 0f);
            return true;
        }
    }

    private static void SendCommand(BrowserTvCommandType command, Vector3i blockPos, string text, int entityId)
    {
        if (SingletonMonoBehaviour<ConnectionManager>.Instance.IsClient)
        {
            SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(new BrowserTvCommandPackage().Setup(command, blockPos, text, 0f, entityId), false);
        }
        else
        {
            BrowserTvServerStateService.HandleCommand(command, blockPos, text, 0f, entityId);
        }
    }

    public override bool OnBlockActivated(WorldBase world, int cIdx, Vector3i blockPos, BlockValue blockValue, EntityPlayerLocal player)
    {
        return OnBlockActivated(string.Empty, world, cIdx, blockPos, blockValue, player);
    }

    public override BlockActivationCommand[] GetBlockActivationCommands(WorldBase world, BlockValue blockValue, int clrIdx, Vector3i blockPos, EntityAlive entityFocusing)
    {
        List<BlockActivationCommand> commands = new List<BlockActivationCommand>
        {
            new BlockActivationCommand("take", "hand", true, false, null)
        };

        TileEntityBrowserTV tileEntity = world.GetTileEntity(clrIdx, blockPos) as TileEntityBrowserTV;
        if (tileEntity != null && ((TileEntityPowered)tileEntity).IsPowered)
        {
            commands.Insert(0, new BlockActivationCommand("edit", "pen", true, false, null));
        }

        return commands.ToArray();
    }

    public override void OnBlockRemoved(WorldBase world, Chunk chunk, Vector3i blockPos, BlockValue blockValue)
    {
        if (!blockValue.ischild)
        {
            BrowserTvManager.Instance.Unregister(blockPos);
        }

        base.OnBlockRemoved(world, chunk, blockPos, blockValue);
    }

    private static TileEntityBrowserTV CreateTileEntity(Chunk chunk, Vector3i blockPos)
    {
        if (chunk == null)
        {
            return null;
        }

        try
        {
            TileEntityBrowserTV tileEntity = new TileEntityBrowserTV(chunk);
            ((TileEntity)tileEntity).localChunkPos = World.toBlock(blockPos);
            chunk.AddTileEntity(tileEntity);
            Debug.Log("[BrowserTV] Created TileEntityBrowserTV at " + blockPos);
            return tileEntity;
        }
        catch (Exception ex)
        {
            Debug.LogError("[BrowserTV] Failed to create TileEntityBrowserTV at " + blockPos + ": " + ex.Message);
            return null;
        }
    }
}
