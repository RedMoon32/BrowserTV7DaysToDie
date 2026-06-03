using UnityEngine;

public static class BrowserTvClientStateService
{
    private static BrowserTvState current = new BrowserTvState();

    public static BrowserTvState Current => current.Clone();

    public static void ReapplyIfCurrentTv(Vector3i blockPos)
    {
        BrowserTvState snapshot = current.Clone();
        if (snapshot.Power == BrowserTvPowerState.Off || !snapshot.IsSameTv(blockPos))
        {
            return;
        }

        Debug.Log("[BrowserTV] Reapplying current state for registered TV screen at " + blockPos);
        ApplyState(snapshot);
    }

    public static void ApplyState(BrowserTvState state)
    {
        if (state.Revision < current.Revision)
        {
            return;
        }

        current = state.Clone();
        Debug.Log("[BrowserTV] Client state rev=" + current.Revision + " power=" + current.Power + " status=" + current.StatusText);

        bool shouldSetScreenState = true;
        BrowserTvScreenState screenState = BrowserTvScreenState.Off;
        if (current.Power == BrowserTvPowerState.Starting)
        {
            screenState = BrowserTvScreenState.Standby;
        }
        else if (current.Power == BrowserTvPowerState.On)
        {
            if (string.IsNullOrEmpty(current.StreamUrl))
            {
                screenState = BrowserTvScreenState.Standby;
            }
            else
            {
                shouldSetScreenState = false;
            }
        }
        else if (current.Power == BrowserTvPowerState.Error)
        {
            screenState = BrowserTvScreenState.Error;
        }

        if (shouldSetScreenState)
        {
            BrowserTvManager.Instance.SetScreenState(current.BlockPos, screenState);
        }

        BrowserTvWebRtcViewerHost.Ensure().ApplyState(current);
    }
}
