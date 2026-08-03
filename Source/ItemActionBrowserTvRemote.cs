using UnityEngine;

public class ItemActionBrowserTvRemote : ItemAction
{
    private const float MaxRange = 30f;

    public override void ExecuteAction(ItemActionData actionData, bool released)
    {
        if (released || actionData == null || actionData.invData == null)
        {
            return;
        }

        EntityPlayerLocal player = actionData.invData.holdingEntity as EntityPlayerLocal;
        BrowserTvState state = BrowserTvClientStateService.Current;
        if (player == null || state.Power != BrowserTvPowerState.On || string.IsNullOrEmpty(state.SessionId))
        {
            return;
        }

        BrowserTvScreenController controller = BrowserTvManager.Instance.GetController(state.BlockPos);
        if (controller == null)
        {
            ShowTooltip(player, "Browser TV screen is not loaded");
            return;
        }

        Ray ray = player.GetLookRay();
        if (!controller.TryGetBrowserCoordinates(ray, out Vector2 browserCoordinates, out float screenDistance) || screenDistance > MaxRange)
        {
            ShowTooltip(player, "Aim at the Browser TV screen");
            return;
        }

        RaycastHit obstacle;
        if (Physics.Raycast(ray, out obstacle, MaxRange, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) && obstacle.distance < screenDistance - 0.75f)
        {
            ShowTooltip(player, "Browser TV screen is blocked");
            return;
        }

        int entityId = ((Entity)player).entityId;
        if (SingletonMonoBehaviour<ConnectionManager>.Instance.IsClient)
        {
            SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(new BrowserTvClickPackage().Setup(state.BlockPos, browserCoordinates.x, browserCoordinates.y, entityId), false);
        }
        else
        {
            BrowserTvServerStateService.HandleClick(player.world, state.BlockPos, browserCoordinates.x, browserCoordinates.y, entityId);
        }
    }

    private static void ShowTooltip(EntityPlayerLocal player, string text)
    {
        GameManager.ShowTooltip(player, text, string.Empty, "ui_denied", null, false, false, 0f);
    }
}
