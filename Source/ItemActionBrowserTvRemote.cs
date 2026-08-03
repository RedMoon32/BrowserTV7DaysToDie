using UnityEngine;

public class ItemActionBrowserTvRemote : ItemAction
{
    private const float MaxRange = 30f;
    private const float ObstacleEpsilon = 0.05f;

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
        if (!controller.TryGetBrowserCoordinates(ray, out Vector2 browserCoordinates, out float screenDistance, out BrowserTvScreenController.ScreenRaycastFailure failure))
        {
            ShowTooltip(player, GetRaycastFailureMessage(failure));
            return;
        }

        if (screenDistance > MaxRange)
        {
            ShowTooltip(player, "Browser TV screen is too far away");
            return;
        }

        if (HasForeignObstacle(ray, screenDistance, controller))
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

    private static bool HasForeignObstacle(Ray ray, float screenDistance, BrowserTvScreenController controller)
    {
        RaycastHit[] hits = Physics.RaycastAll(ray, screenDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (first, second) => first.distance.CompareTo(second.distance));
        float obstacleDistance = screenDistance - ObstacleEpsilon;
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null || hit.distance >= obstacleDistance || controller.OwnsCollider(hit.collider))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private static string GetRaycastFailureMessage(BrowserTvScreenController.ScreenRaycastFailure failure)
    {
        switch (failure)
        {
            case BrowserTvScreenController.ScreenRaycastFailure.RendererMissing:
                return "Browser TV screen is not loaded";
            case BrowserTvScreenController.ScreenRaycastFailure.MeshMissing:
                return "Browser TV screen mesh is unavailable";
            case BrowserTvScreenController.ScreenRaycastFailure.MeshUvMissing:
                return "Browser TV screen has no click coordinates";
            default:
                return "Aim at the Browser TV screen";
        }
    }
}
