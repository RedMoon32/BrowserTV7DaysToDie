using UnityEngine;

public class BrowserTvWebRtcViewerHost : MonoBehaviour
{
    private static BrowserTvWebRtcViewerHost instance;
    private BrowserTvWebRtcViewer viewer;

    public static BrowserTvWebRtcViewerHost Ensure()
    {
        if (instance != null)
        {
            return instance;
        }

        GameObject go = new GameObject("BrowserTvWebRtcViewerHost");
        instance = go.AddComponent<BrowserTvWebRtcViewerHost>();
        DontDestroyOnLoad(go);
        return instance;
    }

    public void ApplyState(BrowserTvState state)
    {
        if (state.Power != BrowserTvPowerState.On || string.IsNullOrEmpty(state.SessionId))
        {
            StopViewer();
            return;
        }

        BrowserTvScreenController controller = BrowserTvManager.Instance.GetController(state.BlockPos);
        if (controller == null)
        {
            return;
        }

        if (viewer == null)
        {
            viewer = gameObject.AddComponent<BrowserTvWebRtcViewer>();
        }

        viewer.StartViewing(state, controller);
    }

    private void StopViewer()
    {
        if (viewer != null)
        {
            viewer.StopViewing();
        }
    }
}
