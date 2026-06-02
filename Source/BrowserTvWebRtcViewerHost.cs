using UnityEngine;
using System.Collections;

public class BrowserTvWebRtcViewerHost : MonoBehaviour
{
    private static BrowserTvWebRtcViewerHost instance;
    private BrowserTvWebRtcViewer viewer;
    private Coroutine applyLoop;

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
        if (applyLoop != null)
        {
            StopCoroutine(applyLoop);
            applyLoop = null;
        }

        if (state.Power != BrowserTvPowerState.On || string.IsNullOrEmpty(state.SessionId))
        {
            StopViewer();
            return;
        }

        applyLoop = StartCoroutine(ApplyStateDelayed(state.Clone()));
    }

    private IEnumerator ApplyStateDelayed(BrowserTvState state)
    {
        yield return null;
        yield return null;
        yield return null;

        BrowserTvScreenController controller = BrowserTvManager.Instance.GetController(state.BlockPos);
        if (controller == null)
        {
            applyLoop = null;
            yield break;
        }

        if (viewer == null)
        {
            viewer = gameObject.AddComponent<BrowserTvWebRtcViewer>();
        }

        viewer.StartViewing(state, controller);
        applyLoop = null;
    }

    private void StopViewer()
    {
        if (viewer != null)
        {
            viewer.StopViewing();
        }
    }
}
