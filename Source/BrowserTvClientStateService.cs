using UnityEngine;

public static class BrowserTvClientStateService
{
    private static BrowserTvState current = new BrowserTvState();

    public static BrowserTvState Current => current.Clone();

    public static void ApplyState(BrowserTvState state)
    {
        if (state.Revision < current.Revision)
        {
            return;
        }

        current = state.Clone();
        Debug.Log("[BrowserTV] Client state rev=" + current.Revision + " power=" + current.Power + " status=" + current.StatusText);

        BrowserTvScreenState screenState = BrowserTvScreenState.Off;
        if (current.Power == BrowserTvPowerState.On || current.Power == BrowserTvPowerState.Starting)
        {
            screenState = BrowserTvScreenState.Standby;
        }
        else if (current.Power == BrowserTvPowerState.Error)
        {
            screenState = BrowserTvScreenState.Error;
        }

        BrowserTvManager.Instance.SetScreenState(current.BlockPos, screenState);
        BrowserTvWebRtcViewerHost.Ensure().ApplyState(current);
    }
}
