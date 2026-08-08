using System;
using System.Collections.Generic;
using UnityEngine;

public class ItemActionBrowserTvRemote : ItemAction
{
    private const float MaxRange = 30f;
    private const float ObstacleEpsilon = 0.5f;

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

        Ray ray = player.GetLookRay();
        ray.origin -= Origin.position;

        BrowserTvScreenController target = null;
        Vector2 browserCoordinates = Vector2.zero;
        float nearestDistance = float.MaxValue;
        BrowserTvScreenController.ScreenRaycastFailure lastFailure = BrowserTvScreenController.ScreenRaycastFailure.None;

        List<BrowserTvScreenController> controllers = BrowserTvManager.Instance.GetAllControllers();
        for (int i = 0; i < controllers.Count; i++)
        {
            BrowserTvScreenController candidate = controllers[i];
            if (candidate == null || candidate.ParentTileEntity == null)
            {
                continue;
            }

            if (!candidate.TryGetBrowserCoordinates(ray, MaxRange, out Vector2 coords, out float distance, out BrowserTvScreenController.ScreenRaycastFailure failure))
            {
                lastFailure = failure;
                continue;
            }

            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                browserCoordinates = coords;
                target = candidate;
            }
        }

        if (target == null)
        {
            ShowTooltip(player, GetRaycastFailureMessage(lastFailure));
            return;
        }

        if (nearestDistance > MaxRange)
        {
            ShowTooltip(player, "Browser TV screen is too far away");
            return;
        }

        if (!state.IsSameTv(target.ParentTileEntity.ToWorldPos()))
        {
            ShowTooltip(player, "Aim at the active Browser TV");
            return;
        }

        if (HasForeignObstacle(ray, nearestDistance, target))
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
        Bounds screenBounds = controller.ScreenBounds;
        RaycastHit[] hits = Physics.RaycastAll(ray, screenDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (first, second) => first.distance.CompareTo(second.distance));
        float obstacleLimit = screenDistance - ObstacleEpsilon;
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null || hit.distance >= obstacleLimit || controller.OwnsCollider(hit.collider) || screenBounds.Contains(hit.point))
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
            case BrowserTvScreenController.ScreenRaycastFailure.MissedSurface:
                return "Aim at the Browser TV screen";
            default:
                return "Aim at the Browser TV screen";
        }
    }
}
