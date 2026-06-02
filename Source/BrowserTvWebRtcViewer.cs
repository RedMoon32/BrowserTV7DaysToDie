using UnityEngine;
using UnityEngine.Video;

public class BrowserTvWebRtcViewer : MonoBehaviour
{
    private BrowserTvState state;
    private BrowserTvScreenController controller;
    private VideoPlayer videoPlayer;
    private AudioSource audioSource;
    private RenderTexture renderTexture;

    public void StartViewing(BrowserTvState nextState, BrowserTvScreenController screenController)
    {
        if (nextState == null || screenController == null)
        {
            StopViewing();
            return;
        }

        if (state != null &&
            state.SessionId == nextState.SessionId &&
            state.StreamUrl == nextState.StreamUrl)
        {
            state = nextState.Clone();
            controller = screenController;
            controller.SetVolume(state.Volume);
            return;
        }

        StopViewing();

        state = nextState.Clone();
        controller = screenController;
        controller.SetVolume(state.Volume);

        if (string.IsNullOrEmpty(state.StreamUrl))
        {
            Debug.LogWarning("[BrowserTV] Cannot start media viewer because streamUrl is empty.");
            controller.SetState(BrowserTvScreenState.Error);
            return;
        }

        EnsurePlayer();
        renderTexture = new RenderTexture(1280, 720, 0, RenderTextureFormat.ARGB32);
        renderTexture.name = "BrowserTV_Stream_" + state.SessionId;
        renderTexture.Create();

        controller.SetExternalTexture(renderTexture);
        videoPlayer.targetTexture = renderTexture;
        videoPlayer.url = state.StreamUrl;
        videoPlayer.Prepare();
        Debug.Log("[BrowserTV] VideoPlayer preparing " + state.StreamUrl);
    }

    public void StopViewing()
    {
        if (videoPlayer != null)
        {
            videoPlayer.Stop();
            videoPlayer.url = "";
            videoPlayer.targetTexture = null;
        }

        if (renderTexture != null)
        {
            renderTexture.Release();
            Destroy(renderTexture);
            renderTexture = null;
        }

        state = null;
    }

    private void EnsurePlayer()
    {
        if (videoPlayer != null)
        {
            return;
        }

        audioSource = gameObject.GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        videoPlayer = gameObject.AddComponent<VideoPlayer>();
        videoPlayer.playOnAwake = false;
        videoPlayer.isLooping = true;
        videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        videoPlayer.source = VideoSource.Url;
        videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
        videoPlayer.SetTargetAudioSource(0, audioSource);
        videoPlayer.errorReceived += (_, message) =>
        {
            Debug.LogError("[BrowserTV] VideoPlayer error: " + message);
            if (controller != null)
            {
                controller.SetState(BrowserTvScreenState.Error);
            }
        };
        videoPlayer.prepareCompleted += player =>
        {
            Debug.Log("[BrowserTV] VideoPlayer prepared; starting playback.");
            player.Play();
        };
    }

    private void OnDestroy()
    {
        StopViewing();
    }
}
