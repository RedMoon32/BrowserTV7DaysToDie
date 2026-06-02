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
    private const int BrightnessPercent = 60;
    private static readonly bool FlipVertical = true;
    private static readonly bool FlipHorizontal = false;

    private static bool vlcInitialized;

    private BrowserTvState state;
    private BrowserTvScreenController controller;
    private LibVLC libVlc;
    private MediaPlayer mediaPlayer;
    private Media media;
    private Texture2D texture;
    private byte[] decodeBuffer;
    private byte[] uploadBuffer;
    private GCHandle decodeHandle;
    private readonly object frameLock = new object();
    private volatile bool frameReady;
    private bool firstFrameDisplayed;

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
            state.StreamUrl == nextState.StreamUrl)
        {
            state = nextState.Clone();
            controller = screenController;
            controller.SetVolume(state.Volume);
            if (mediaPlayer != null)
            {
                mediaPlayer.Volume = VolumeToVlc(state.Volume);
            }
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
            media.AddOption(":network-caching=250");
            mediaPlayer.Play(media);
            controller.SetExternalTexture(texture);
            Debug.Log("[BrowserTV] LibVLC preparing " + state.StreamUrl);
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

        if (media != null)
        {
            media.Dispose();
            media = null;
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
        state = null;
    }

    private void Update()
    {
        if (!frameReady || texture == null)
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

        int rowBytes = Width * BytesPerPixel;
        if (!FlipVertical && !FlipHorizontal)
        {
            for (int y = 0; y < Height; y++)
            {
                CopyDimmedRow(y * rowBytes, y * rowBytes);
            }

            texture.LoadRawTextureData(uploadBuffer);
            return;
        }

        for (int y = 0; y < Height; y++)
        {
            int sourceY = FlipVertical ? Height - 1 - y : y;
            int sourceRow = sourceY * rowBytes;
            int targetRow = y * rowBytes;

            if (!FlipHorizontal)
            {
                CopyDimmedRow(sourceRow, targetRow);
                continue;
            }

            for (int x = 0; x < Width; x++)
            {
                int source = sourceRow + ((Width - 1 - x) * BytesPerPixel);
                int target = targetRow + (x * BytesPerPixel);
                uploadBuffer[target] = DimByte(decodeBuffer[source]);
                uploadBuffer[target + 1] = DimByte(decodeBuffer[source + 1]);
                uploadBuffer[target + 2] = DimByte(decodeBuffer[source + 2]);
                uploadBuffer[target + 3] = decodeBuffer[source + 3];
            }
        }

        texture.LoadRawTextureData(uploadBuffer);
    }

    private void CopyDimmedRow(int sourceRow, int targetRow)
    {
        int rowBytes = Width * BytesPerPixel;
        for (int offset = 0; offset < rowBytes; offset += BytesPerPixel)
        {
            int source = sourceRow + offset;
            int target = targetRow + offset;
            uploadBuffer[target] = DimByte(decodeBuffer[source]);
            uploadBuffer[target + 1] = DimByte(decodeBuffer[source + 1]);
            uploadBuffer[target + 2] = DimByte(decodeBuffer[source + 2]);
            uploadBuffer[target + 3] = decodeBuffer[source + 3];
        }
    }

    private static byte DimByte(byte value)
    {
        return (byte)((value * BrightnessPercent) / 100);
    }

    private static int VolumeToVlc(float volume)
    {
        return Mathf.Clamp(Mathf.RoundToInt(volume * 100f), 0, 100);
    }

    private void OnDestroy()
    {
        StopViewing();
    }
}
