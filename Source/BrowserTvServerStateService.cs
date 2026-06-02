using System;
using System.Threading;
using UnityEngine;

public static class BrowserTvServerStateService
{
    private static readonly object Sync = new object();
    private static BrowserTvState state = new BrowserTvState();
    private static BrowserTvBridgeClient bridgeClient;
    private static int operationRevision;

    public static BrowserTvState Current
    {
        get
        {
            lock (Sync)
            {
                return state.Clone();
            }
        }
    }

    public static void Initialize()
    {
        lock (Sync)
        {
            state.Reset();
            bridgeClient = new BrowserTvBridgeClient(BrowserTvConfig.Current);
        }
    }

    public static void HandleCommand(BrowserTvCommandType command, Vector3i blockPos, string text, float value, int entityId)
    {
        if (!BrowserTvConfig.Current.EnableBrowserTv)
        {
            return;
        }

        switch (command)
        {
            case BrowserTvCommandType.RequestState:
                BroadcastState();
                break;
            case BrowserTvCommandType.PowerOn:
                PowerOn(blockPos, string.IsNullOrEmpty(text) ? BrowserTvConfig.Current.DefaultUrl : text, entityId);
                break;
            case BrowserTvCommandType.PowerOff:
                PowerOff(blockPos);
                break;
            case BrowserTvCommandType.Navigate:
                Navigate(blockPos, text);
                break;
            case BrowserTvCommandType.SetVolume:
                SetVolume(blockPos, value);
                break;
        }
    }

    public static void PowerOn(Vector3i blockPos, string url, int entityId)
    {
        int requestRevision;
        lock (Sync)
        {
            if (state.Power != BrowserTvPowerState.Off && !state.IsSameTv(blockPos))
            {
                state.Power = BrowserTvPowerState.Error;
                state.StatusText = "Another Browser TV is already active";
                state.Revision++;
                BroadcastStateLocked();
                return;
            }

            state.Power = BrowserTvPowerState.Starting;
            state.BlockPos = blockPos;
            state.CurrentUrl = url;
            state.ControllerEntityId = entityId;
            state.StatusText = "Starting browser session";
            state.Revision++;
            requestRevision = ++operationRevision;
            BroadcastStateLocked();
        }

        ThreadPool.QueueUserWorkItem(_ =>
        {
            try
            {
                BrowserTvBridgeStartResult result = bridgeClient.StartSession(blockPos, url);
                BrowserTvManager.RunOnMainThread(() =>
                {
                    lock (Sync)
                    {
                        if (requestRevision != operationRevision || !state.IsSameTv(blockPos) || state.Power != BrowserTvPowerState.Starting)
                        {
                            return;
                        }

                        state.Power = BrowserTvPowerState.On;
                        state.BlockPos = blockPos;
                        state.SessionId = result.SessionId;
                        state.BridgeEndpoint = BrowserTvConfig.Current.BridgePublicUrl;
                        state.ViewerToken = result.ViewerToken;
                        state.ControllerToken = result.ControllerToken;
                        state.CurrentUrl = url;
                        state.StatusText = string.IsNullOrEmpty(result.StatusText) ? "On" : result.StatusText;
                        state.Revision++;
                        BroadcastStateLocked();
                    }
                });
            }
            catch (Exception ex)
            {
                BrowserTvManager.RunOnMainThread(() =>
                {
                    lock (Sync)
                    {
                        if (requestRevision != operationRevision || !state.IsSameTv(blockPos) || state.Power != BrowserTvPowerState.Starting)
                        {
                            return;
                        }

                        state.Power = BrowserTvPowerState.Error;
                        state.StatusText = "Bridge unavailable: " + ex.Message;
                        state.Revision++;
                        BroadcastStateLocked();
                    }
                });
            }
        });
    }

    public static void PowerOff(Vector3i blockPos)
    {
        string sessionId;
        lock (Sync)
        {
            if (state.Power == BrowserTvPowerState.Off || !state.IsSameTv(blockPos))
            {
                return;
            }

            sessionId = state.SessionId;
            operationRevision++;
            state.Reset();
            BroadcastStateLocked();
        }

        ThreadPool.QueueUserWorkItem(_ =>
        {
            try
            {
                bridgeClient.StopSession(sessionId);
            }
            catch (Exception ex)
            {
                BrowserTvManager.RunOnMainThread(() => Debug.LogWarning("[BrowserTV] Bridge stop failed: " + ex.Message));
            }
        });
    }

    private static void Navigate(Vector3i blockPos, string url)
    {
        lock (Sync)
        {
            if (state.Power != BrowserTvPowerState.On || !state.IsSameTv(blockPos))
            {
                return;
            }

            state.CurrentUrl = url;
            state.StatusText = "Navigating";
            state.Revision++;
            BroadcastStateLocked();
            string sessionId = state.SessionId;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    bridgeClient.Navigate(sessionId, url);
                }
                catch (Exception ex)
                {
                    BrowserTvManager.RunOnMainThread(() => Debug.LogWarning("[BrowserTV] Bridge navigate failed: " + ex.Message));
                }
            });
        }
    }

    private static void SetVolume(Vector3i blockPos, float volume)
    {
        lock (Sync)
        {
            if (state.Power == BrowserTvPowerState.Off || !state.IsSameTv(blockPos))
            {
                return;
            }

            state.Volume = Mathf.Clamp01(volume);
            state.Revision++;
            BroadcastStateLocked();
        }
    }

    public static void BroadcastState()
    {
        lock (Sync)
        {
            BroadcastStateLocked();
        }
    }

    private static void BroadcastStateLocked()
    {
        BrowserTvState snapshot = state.Clone();
        SingletonMonoBehaviour<ConnectionManager>.Instance.SendToClientsOrServer(new BrowserTvStatePackage().Setup(snapshot));
        if (!GameManager.IsDedicatedServer)
        {
            BrowserTvManager.RunOnMainThread(() => BrowserTvClientStateService.ApplyState(snapshot));
        }

        Debug.Log("[BrowserTV] Broadcast state rev=" + state.Revision + " power=" + state.Power + " status=" + state.StatusText);
    }
}
