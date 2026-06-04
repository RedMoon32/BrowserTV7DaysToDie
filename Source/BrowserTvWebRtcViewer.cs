using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
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
    private const int FrameBufferCount = 3;
    private const int NoFrame = -1;
    private const float SpatialAudioUpdateIntervalSeconds = 0.15f;

    private static bool vlcInitialized;

    private BrowserTvState state;
    private BrowserTvScreenController controller;
    private LibVLC libVlc;
    private MediaPlayer mediaPlayer;
    private Media media;
    private Texture2D texture;
    private byte[][] frameBuffers;
    private IntPtr[] framePointers;
    private byte[] uploadBuffer;
    private byte[] droppedFrameBuffer;
    private IntPtr droppedFramePointer;
    private GCHandle[] frameHandles;
    private GCHandle droppedFrameHandle;
    private bool[] frameInUse;
    private bool[] frameDecoded;
    private int readyFrame = NoFrame;
    private int uploadFrame = NoFrame;
    private readonly object frameLock = new object();
    private volatile bool frameReady;
    private bool firstFrameDisplayed;
    private float nextFrameUploadTime;
    private float nextCallbackDiagnosticsTime;
    private int lastLoggedLockFrameCount;
    private int lastLoggedUnlockFrameCount;
    private int lastLoggedDisplayFrameCount;
    private int lastLoggedUploadedFrameCount;
    private int lockFrameCount;
    private int unlockFrameCount;
    private int displayFrameCount;
    private int uploadedFrameCount;
    private int lastUploadedFrameIndex = NoFrame;
    private uint lastUploadedFrameHash;
    private byte lastSampleA;
    private byte lastSampleB;
    private byte lastSampleC;
    private float nextSpatialAudioUpdateTime;
    private int lastAppliedVlcVolume = -1;

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
            if (texture != null)
            {
                controller.SetExternalTexture(texture);
            }
            if (mediaPlayer != null)
            {
                ApplySpatialAudioVolume(true);
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
                "--network-caching=500",
                "--live-caching=500",
                "--no-drop-late-frames",
                "--no-skip-frames",
                "--avcodec-hw=none");

            mediaPlayer = new MediaPlayer(libVlc);
            mediaPlayer.SetVideoFormat("RV32", Width, Height, Pitch);
            mediaPlayer.SetVideoCallbacks(lockCallback, unlockCallback, displayCallback);
            ApplySpatialAudioVolume(true);
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
            media.AddOption(":network-caching=500");
            media.AddOption(":live-caching=500");
            media.AddOption(":no-drop-late-frames");
            media.AddOption(":no-skip-frames");
            mediaPlayer.Play(media);

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

        ReleaseFrameBuffers();
        uploadBuffer = null;
        frameReady = false;
        readyFrame = NoFrame;
        uploadFrame = NoFrame;
        firstFrameDisplayed = false;
        nextFrameUploadTime = 0f;
        nextCallbackDiagnosticsTime = 0f;
        nextSpatialAudioUpdateTime = 0f;
        lastAppliedVlcVolume = -1;
        lastLoggedLockFrameCount = 0;
        lastLoggedUnlockFrameCount = 0;
        lastLoggedDisplayFrameCount = 0;
        lastLoggedUploadedFrameCount = 0;
        lockFrameCount = 0;
        unlockFrameCount = 0;
        displayFrameCount = 0;
        uploadedFrameCount = 0;
        lastUploadedFrameIndex = NoFrame;
        lastUploadedFrameHash = 0;
        lastSampleA = 0;
        lastSampleB = 0;
        lastSampleC = 0;
        state = null;
    }

    private void Update()
    {
        if (state == null || mediaPlayer == null)
        {
            return;
        }

        ApplySpatialAudioVolume(false);

        if (!frameReady || texture == null)
        {
            LogCallbackDiagnosticsIfNeeded(true);
            return;
        }

        if (Time.unscaledTime < nextFrameUploadTime)
        {
            LogCallbackDiagnosticsIfNeeded(true);
            return;
        }

        int frameToUpload;
        lock (frameLock)
        {
            if (!frameReady || readyFrame == NoFrame || uploadBuffer == null)
            {
                LogCallbackDiagnosticsIfNeeded(true);
                return;
            }

            frameToUpload = readyFrame;
            uploadFrame = frameToUpload;
            readyFrame = NoFrame;
            frameReady = false;
        }

        CopyFrameForUnityUpload(frameToUpload);

        lock (frameLock)
        {
            if (frameInUse != null && frameToUpload >= 0 && frameToUpload < frameInUse.Length)
            {
                frameInUse[frameToUpload] = false;
                if (frameDecoded != null)
                {
                    frameDecoded[frameToUpload] = false;
                }
            }

            uploadFrame = NoFrame;
        }

        texture.Apply(false, false);
        Interlocked.Increment(ref uploadedFrameCount);
        if (nextFrameUploadTime <= 0f || Time.unscaledTime - nextFrameUploadTime > FrameIntervalSeconds)
        {
            nextFrameUploadTime = Time.unscaledTime + FrameIntervalSeconds;
        }
        else
        {
            nextFrameUploadTime += FrameIntervalSeconds;
        }
        if (!firstFrameDisplayed)
        {
            firstFrameDisplayed = true;
            if (controller != null)
            {
                controller.SetExternalTexture(texture);
            }
            Debug.Log("[BrowserTV] First LibVLC frame displayed.");
        }

        LogCallbackDiagnosticsIfNeeded(true);
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
        frameBuffers = new byte[FrameBufferCount][];
        framePointers = new IntPtr[FrameBufferCount];
        frameHandles = new GCHandle[FrameBufferCount];
        frameInUse = new bool[FrameBufferCount];
        frameDecoded = new bool[FrameBufferCount];

        for (int i = 0; i < FrameBufferCount; i++)
        {
            frameBuffers[i] = new byte[frameBytes + 31];
            frameHandles[i] = GCHandle.Alloc(frameBuffers[i], GCHandleType.Pinned);
            framePointers[i] = AlignPointer(frameHandles[i].AddrOfPinnedObject(), 32);
        }

        uploadBuffer = new byte[frameBytes];
        droppedFrameBuffer = new byte[frameBytes + 31];
        droppedFrameHandle = GCHandle.Alloc(droppedFrameBuffer, GCHandleType.Pinned);
        droppedFramePointer = AlignPointer(droppedFrameHandle.AddrOfPinnedObject(), 32);
        readyFrame = NoFrame;
        uploadFrame = NoFrame;
    }

    private void ReleaseFrameBuffers()
    {
        if (frameHandles != null)
        {
            for (int i = 0; i < frameHandles.Length; i++)
            {
                if (frameHandles[i].IsAllocated)
                {
                    frameHandles[i].Free();
                }
            }
        }

        if (droppedFrameHandle.IsAllocated)
        {
            droppedFrameHandle.Free();
        }

        frameBuffers = null;
        framePointers = null;
        frameHandles = null;
        frameInUse = null;
        frameDecoded = null;
        droppedFrameBuffer = null;
        droppedFramePointer = IntPtr.Zero;
    }

    private int ReserveDecodeFrame()
    {
        if (frameInUse == null)
        {
            return NoFrame;
        }

        for (int i = 0; i < frameInUse.Length; i++)
        {
            if (!frameInUse[i])
            {
                frameInUse[i] = true;
                frameDecoded[i] = false;
                return i;
            }
        }

        if (readyFrame != NoFrame && readyFrame != uploadFrame)
        {
            int reusableFrame = readyFrame;
            readyFrame = NoFrame;
            frameReady = false;
            frameDecoded[reusableFrame] = false;
            return reusableFrame;
        }

        return NoFrame;
    }

    private void EnsureCallbacks()
    {
        lockCallback = LockFrame;
        unlockCallback = UnlockFrame;
        displayCallback = DisplayFrame;
    }

    private IntPtr LockFrame(IntPtr opaque, IntPtr planes)
    {
        Interlocked.Increment(ref lockFrameCount);
        lock (frameLock)
        {
            int frame = ReserveDecodeFrame();
            if (frame == NoFrame || framePointers == null)
            {
                Marshal.WriteIntPtr(planes, droppedFramePointer);
                return IntPtr.Zero;
            }

            Marshal.WriteIntPtr(planes, framePointers[frame]);
            return FrameIndexToPicture(frame);
        }
    }

    private void UnlockFrame(IntPtr opaque, IntPtr picture, IntPtr planes)
    {
        Interlocked.Increment(ref unlockFrameCount);
        lock (frameLock)
        {
            int frame = PictureToFrameIndex(picture);
            if (frameInUse != null && frame >= 0 && frame < frameInUse.Length && frameInUse[frame])
            {
                frameDecoded[frame] = true;
            }
        }
    }

    private void DisplayFrame(IntPtr opaque, IntPtr picture)
    {
        Interlocked.Increment(ref displayFrameCount);
        lock (frameLock)
        {
            int frame = PictureToFrameIndex(picture);
            if (frameInUse == null ||
                frameDecoded == null ||
                frame < 0 ||
                frame >= frameInUse.Length ||
                !frameInUse[frame] ||
                !frameDecoded[frame])
            {
                return;
            }

            if (readyFrame != NoFrame && readyFrame != frame && readyFrame != uploadFrame)
            {
                frameInUse[readyFrame] = false;
                frameDecoded[readyFrame] = false;
            }

            readyFrame = frame;
            frameReady = true;
        }
    }

    private void CopyFrameForUnityUpload(int frameIndex)
    {
        if (framePointers == null || uploadBuffer == null || frameIndex < 0 || frameIndex >= framePointers.Length)
        {
            return;
        }

        Marshal.Copy(framePointers[frameIndex], uploadBuffer, 0, uploadBuffer.Length);
        lastUploadedFrameIndex = frameIndex;
        lastUploadedFrameHash = ComputeFrameHash(uploadBuffer);
        lastSampleA = uploadBuffer[BytesPerPixel * ((Height / 4) * Width + (Width / 4))];
        lastSampleB = uploadBuffer[BytesPerPixel * ((Height / 2) * Width + (Width / 2))];
        lastSampleC = uploadBuffer[BytesPerPixel * (((Height * 3) / 4) * Width + ((Width * 3) / 4))];
        texture.LoadRawTextureData(uploadBuffer);
    }

    private static IntPtr AlignPointer(IntPtr pointer, int alignment)
    {
        long address = pointer.ToInt64();
        long aligned = (address + alignment - 1) & ~(alignment - 1);
        return new IntPtr(aligned);
    }

    private static IntPtr FrameIndexToPicture(int frameIndex)
    {
        return new IntPtr(frameIndex + 1);
    }

    private static int PictureToFrameIndex(IntPtr picture)
    {
        long value = picture.ToInt64();
        return value <= 0 ? NoFrame : (int)value - 1;
    }

    private static int VolumeToVlc(float volume)
    {
        return Mathf.Clamp(Mathf.RoundToInt(volume * 100f), 0, 100);
    }

    private void ApplySpatialAudioVolume(bool force)
    {
        if (mediaPlayer == null || state == null)
        {
            return;
        }

        if (!force && Time.unscaledTime < nextSpatialAudioUpdateTime)
        {
            return;
        }

        nextSpatialAudioUpdateTime = Time.unscaledTime + SpatialAudioUpdateIntervalSeconds;
        float effectiveVolume = Mathf.Clamp01(state.Volume);
        if (BrowserTvConfig.Current.SpatialAudioEnabled)
        {
            effectiveVolume *= GetSpatialAudioAttenuation(state.BlockPos);
        }

        int vlcVolume = VolumeToVlc(effectiveVolume);
        if (force || vlcVolume != lastAppliedVlcVolume)
        {
            mediaPlayer.Volume = vlcVolume;
            lastAppliedVlcVolume = vlcVolume;
        }
    }

    private static float GetSpatialAudioAttenuation(Vector3i blockPos)
    {
        EntityPlayerLocal player = GetPrimaryLocalPlayer();
        if (player == null)
        {
            return 0f;
        }

        Vector3 tvPosition = new Vector3(blockPos.x + 0.5f, blockPos.y + 0.5f, blockPos.z + 0.5f);
        float distance = Vector3.Distance(((EntityAlive)player).position, tvPosition);
        float minDistance = BrowserTvConfig.Current.AudioMinDistance;
        float maxDistance = BrowserTvConfig.Current.AudioMaxDistance;
        if (distance <= minDistance)
        {
            return 1f;
        }

        if (distance >= maxDistance)
        {
            return 0f;
        }

        float t = Mathf.InverseLerp(minDistance, maxDistance, distance);
        return Mathf.Pow(1f - t, BrowserTvConfig.Current.AudioRolloffPower);
    }

    private static EntityPlayerLocal GetPrimaryLocalPlayer()
    {
        try
        {
            World world = GameManager.Instance != null ? GameManager.Instance.World : null;
            return world != null ? ((WorldBase)world).GetPrimaryPlayer() : null;
        }
        catch
        {
            return null;
        }
    }

    private static uint ComputeFrameHash(byte[] bytes)
    {
        unchecked
        {
            uint hash = 2166136261u;
            int step = Math.Max(1, bytes.Length / 4096);
            for (int i = 0; i < bytes.Length; i += step)
            {
                hash ^= bytes[i];
                hash *= 16777619u;
            }

            return hash;
        }
    }

    private void LogCallbackDiagnosticsIfNeeded(bool allowAfterFirstFrame)
    {
        if (mediaPlayer == null || Time.unscaledTime < nextCallbackDiagnosticsTime)
        {
            return;
        }

        if (firstFrameDisplayed && !allowAfterFirstFrame)
        {
            return;
        }

        nextCallbackDiagnosticsTime = Time.unscaledTime + 2f;
        int currentLock = lockFrameCount;
        int currentUnlock = unlockFrameCount;
        int currentDisplay = displayFrameCount;
        int currentUploaded = uploadedFrameCount;
        string prefix = firstFrameDisplayed
            ? "[BrowserTV] LibVLC video callback counters: "
            : "[BrowserTV] LibVLC video callback counters before first frame: ";

        Debug.Log(prefix + "lock=" +
            lockFrameCount +
            " unlock=" +
            unlockFrameCount +
            " display=" +
            displayFrameCount +
            " uploaded=" +
            uploadedFrameCount +
            " deltaLock=" +
            (currentLock - lastLoggedLockFrameCount) +
            " deltaUnlock=" +
            (currentUnlock - lastLoggedUnlockFrameCount) +
            " deltaDisplay=" +
            (currentDisplay - lastLoggedDisplayFrameCount) +
            " deltaUploaded=" +
            (currentUploaded - lastLoggedUploadedFrameCount) +
            " frameIndex=" +
            lastUploadedFrameIndex +
            " frameHash=0x" +
            lastUploadedFrameHash.ToString("X8") +
            " samples=" +
            lastSampleA +
            "," +
            lastSampleB +
            "," +
            lastSampleC);

        lastLoggedLockFrameCount = currentLock;
        lastLoggedUnlockFrameCount = currentUnlock;
        lastLoggedDisplayFrameCount = currentDisplay;
        lastLoggedUploadedFrameCount = currentUploaded;
    }

    private void OnDestroy()
    {
        StopViewing();
    }
}
