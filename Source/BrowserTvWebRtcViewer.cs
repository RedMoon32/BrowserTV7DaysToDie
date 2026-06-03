using System;
using System.IO;
using System.Runtime.InteropServices;
using LibVLCSharp.Shared;
using UnityEngine;

public class BrowserTvWebRtcViewer : MonoBehaviour
{
    private const int Width = 1280;
    private const int Height = 720;
    private const int BytesPerPixel = 4;
    private const uint Pitch = Width * BytesPerPixel;
    private const int TargetFps = 30;
    private const float FrameIntervalSeconds = 1f / TargetFps;

    private static bool vlcInitialized;

    private BrowserTvState state;
    private BrowserTvScreenController controller;
    private LibVLC libVlc;
    private MediaPlayer mediaPlayer;
    private MediaPlayer audioPlayer;
    private Media media;
    private Media audioMedia;
    private Texture2D texture;
    private byte[] decodeBuffer;
    private byte[] uploadBuffer;
    private GCHandle decodeHandle;
    private readonly object frameLock = new object();
    private volatile bool frameReady;
    private bool firstFrameDisplayed;
    private float nextFrameUploadTime;

    private MediaPlayer.LibVLCVideoLockCb lockCallback;
    private MediaPlayer.LibVLCVideoUnlockCb unlockCallback;
    private MediaPlayer.LibVLCVideoDisplayCb displayCallback;

    public void StartViewing(BrowserTvState nextState, BrowserTvScreenController screenController)
    {
        if (nextState == null || screenController == null)
        {
            StopViewing();
            return;
        }

        if (state != null &&
            state.SessionId == nextState.SessionId &&
            state.StreamUrl == nextState.StreamUrl &&
            state.CurrentUrl == nextState.CurrentUrl)
        {
            state = nextState.Clone();
            controller = screenController;
            controller.SetVolume(state.Volume);
            if (mediaPlayer != null)
            {
                mediaPlayer.Volume = VolumeToVlc(state.Volume);
            }
            if (audioPlayer != null)
            {
                audioPlayer.Volume = VolumeToVlc(state.Volume);
            }
            return;
        }

        StopViewing();

        state = nextState.Clone();
        controller = screenController;
        controller.SetVolume(state.Volume);
        controller.SetState(BrowserTvScreenState.Standby);

        if (string.IsNullOrEmpty(state.StreamUrl))
        {
            Debug.LogWarning("[BrowserTV] Cannot start media viewer because streamUrl is empty.");
            controller.SetState(BrowserTvScreenState.Error);
            return;
        }

        try
        {
            EnsureVlcInitialized();
            EnsureTexture();
            EnsureFrameBuffers();
            EnsureCallbacks();

            libVlc = new LibVLC(
                "--no-video-title-show",
                "--quiet",
                "--network-caching=250",
                "--live-caching=250",
                "--clock-jitter=0",
                "--clock-synchro=0");

            mediaPlayer = new MediaPlayer(libVlc);
            mediaPlayer.SetVideoFormat("RV32", Width, Height, Pitch);
            mediaPlayer.SetVideoCallbacks(lockCallback, unlockCallback, displayCallback);
            mediaPlayer.Volume = VolumeToVlc(state.Volume);
            mediaPlayer.EncounteredError += (_, __) =>
            {
                Debug.LogError("[BrowserTV] LibVLC media player encountered an error.");
                if (controller != null)
                {
                    controller.SetState(BrowserTvScreenState.Error);
                }
            };
            mediaPlayer.Playing += (_, __) => Debug.Log("[BrowserTV] LibVLC playback started.");

            media = new Media(libVlc, new Uri(state.StreamUrl));
            media.AddOption(":demux=ts");
            media.AddOption(":no-audio");
            media.AddOption(":network-caching=250");
            mediaPlayer.Play(media);

            string audioUrl = GetAudioUrl(state.StreamUrl);
            audioPlayer = new MediaPlayer(libVlc);
            audioPlayer.Volume = VolumeToVlc(state.Volume);
            audioPlayer.EncounteredError += (_, __) => Debug.LogWarning("[BrowserTV] LibVLC audio player encountered an error.");
            audioMedia = new Media(libVlc, new Uri(audioUrl));
            audioMedia.AddOption(":demux=ts");
            audioMedia.AddOption(":no-video");
            audioMedia.AddOption(":network-caching=250");
            audioPlayer.Play(audioMedia);

            controller.SetExternalTexture(texture);
            Debug.Log("[BrowserTV] LibVLC preparing " + state.StreamUrl + " for " + state.CurrentUrl);
        }
        catch (Exception ex)
        {
            Debug.LogError("[BrowserTV] Failed to start LibVLC viewer: " + ex);
            controller.SetState(BrowserTvScreenState.Error);
            StopViewing();
        }
    }

    public void StopViewing()
    {
        if (mediaPlayer != null)
        {
            try
            {
                mediaPlayer.Stop();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[BrowserTV] LibVLC stop failed: " + ex.Message);
            }

            mediaPlayer.Dispose();
            mediaPlayer = null;
        }

        if (audioPlayer != null)
        {
            try
            {
                audioPlayer.Stop();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[BrowserTV] LibVLC audio stop failed: " + ex.Message);
            }

            audioPlayer.Dispose();
            audioPlayer = null;
        }

        if (media != null)
        {
            media.Dispose();
            media = null;
        }

        if (audioMedia != null)
        {
            audioMedia.Dispose();
            audioMedia = null;
        }

        if (libVlc != null)
        {
            libVlc.Dispose();
            libVlc = null;
        }

        if (texture != null)
        {
            Destroy(texture);
            texture = null;
        }

        if (decodeHandle.IsAllocated)
        {
            decodeHandle.Free();
        }

        decodeBuffer = null;
        uploadBuffer = null;
        frameReady = false;
        firstFrameDisplayed = false;
        nextFrameUploadTime = 0f;
        state = null;
    }

    private void Update()
    {
        if (!frameReady || texture == null)
        {
            return;
        }

        if (Time.unscaledTime < nextFrameUploadTime)
        {
            return;
        }

        lock (frameLock)
        {
            if (!frameReady || uploadBuffer == null)
            {
                return;
            }

            CopyFrameForUnityUpload();
            frameReady = false;
        }

        texture.Apply(false, false);
        nextFrameUploadTime = Time.unscaledTime + FrameIntervalSeconds;
        if (!firstFrameDisplayed)
        {
            firstFrameDisplayed = true;
            if (controller != null)
            {
                controller.SetExternalTexture(texture);
            }
            Debug.Log("[BrowserTV] First LibVLC frame displayed.");
        }
    }

    private static void EnsureVlcInitialized()
    {
        if (vlcInitialized)
        {
            return;
        }

        string assemblyDir = Path.GetDirectoryName(typeof(BrowserTvWebRtcViewer).Assembly.Location);
        string vlcDir = Path.Combine(assemblyDir ?? ".", "libvlc", "win-x64");
        if (!File.Exists(Path.Combine(vlcDir, "libvlc.dll")))
        {
            throw new FileNotFoundException("libvlc.dll was not found in " + vlcDir);
        }

        Core.Initialize(vlcDir);
        vlcInitialized = true;
        Debug.Log("[BrowserTV] LibVLC initialized from " + vlcDir);
    }

    private void EnsureTexture()
    {
        if (texture != null)
        {
            return;
        }

        texture = new Texture2D(Width, Height, TextureFormat.BGRA32, false);
        texture.name = "BrowserTV_LibVLC_" + state.SessionId;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
    }

    private void EnsureFrameBuffers()
    {
        int frameBytes = Width * Height * BytesPerPixel;
        decodeBuffer = new byte[frameBytes];
        uploadBuffer = new byte[frameBytes];
        decodeHandle = GCHandle.Alloc(decodeBuffer, GCHandleType.Pinned);
    }

    private void EnsureCallbacks()
    {
        lockCallback = LockFrame;
        unlockCallback = UnlockFrame;
        displayCallback = DisplayFrame;
    }

    private IntPtr LockFrame(IntPtr opaque, IntPtr planes)
    {
        Marshal.WriteIntPtr(planes, decodeHandle.AddrOfPinnedObject());
        return IntPtr.Zero;
    }

    private void UnlockFrame(IntPtr opaque, IntPtr picture, IntPtr planes)
    {
        lock (frameLock)
        {
            if (decodeBuffer != null && uploadBuffer != null)
            {
                frameReady = true;
            }
        }
    }

    private void DisplayFrame(IntPtr opaque, IntPtr picture)
    {
    }

    private void CopyFrameForUnityUpload()
    {
        if (decodeBuffer == null || uploadBuffer == null)
        {
            return;
        }

        Buffer.BlockCopy(decodeBuffer, 0, uploadBuffer, 0, uploadBuffer.Length);
        texture.LoadRawTextureData(uploadBuffer);
    }

    private static int VolumeToVlc(float volume)
    {
        return Mathf.Clamp(Mathf.RoundToInt(volume * 100f), 0, 100);
    }

    private static string GetAudioUrl(string streamUrl)
    {
        return streamUrl.Replace("/stream.ts?", "/audio.ts?");
    }

    private void OnDestroy()
    {
        StopViewing();
    }
}
