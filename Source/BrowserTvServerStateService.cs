using System;
using System.Net;
using System.Threading;
using UnityEngine;

public static class BrowserTvServerStateService
{
    private static readonly object Sync = new object();
    private static BrowserTvState state = new BrowserTvState();
    private static BrowserTvBridgeClient bridgeClient;
    private static int operationRevision;
    private static readonly System.Collections.Generic.Dictionary<int, DateTime> LastClickAt = new System.Collections.Generic.Dictionary<int, DateTime>();
    private const double ClickCooldownMilliseconds = 120d;

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

    public static void HandleClick(World world, Vector3i blockPos, float u, float v, int entityId)
    {
        if (!BrowserTvConfig.Current.EnableBrowserTv || world == null || float.IsNaN(u) || float.IsNaN(v) || u < 0f || u > 1f || v < 0f || v > 1f)
        {
            return;
        }

        string sessionId;
        lock (Sync)
        {
            if (state.Power != BrowserTvPowerState.On || !state.IsSameTv(blockPos) || string.IsNullOrEmpty(state.SessionId))
            {
                return;
            }

            EntityAlive player = world.GetEntity(entityId) as EntityAlive;
            if (player == null || player.inventory == null || player.inventory.holdingItem == null || player.inventory.holdingItem.Name != "BrowserTvRemote")
            {
                Debug.LogWarning("[BrowserTV] Rejected click from " + entityId + ": Browser TV remote is not held.");
                return;
            }

            Vector3 targetCenter = new Vector3(blockPos.x + 0.5f, blockPos.y + 0.5f, blockPos.z + 0.5f);
            if (Vector3.Distance(player.position, targetCenter) > 31f)
            {
                Debug.LogWarning("[BrowserTV] Rejected click from " + entityId + ": target is out of range.");
                return;
            }

            DateTime now = DateTime.UtcNow;
            if (LastClickAt.TryGetValue(entityId, out DateTime lastClick) && (now - lastClick).TotalMilliseconds < ClickCooldownMilliseconds)
            {
                return;
            }

            LastClickAt[entityId] = now;
            sessionId = state.SessionId;
        }

        int screenWidth = BrowserTvConfig.Current.BrowserWidth;
        int screenHeight = BrowserTvConfig.Current.BrowserHeight;
        int x = Mathf.Clamp(Mathf.RoundToInt(u * (screenWidth - 1)), 0, screenWidth - 1);
        int y = Mathf.Clamp(Mathf.RoundToInt(v * (screenHeight - 1)), 0, screenHeight - 1);
        ThreadPool.QueueUserWorkItem(_ =>
        {
            try
            {
                bridgeClient.Click(sessionId, x, y);
                Debug.Log("[BrowserTV] Click at " + x + "," + y + " by player " + entityId + " on " + blockPos);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[BrowserTV] Bridge click failed: " + ex.Message);
            }
        });
    }

    public static void PowerOn(Vector3i blockPos, string url, int entityId)
    {
        int requestRevision;
        lock (Sync)
        {
            if ((state.Power == BrowserTvPowerState.Starting || state.Power == BrowserTvPowerState.On) && !state.IsSameTv(blockPos))
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
                        state.StreamUrl = result.StreamUrl;
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

    public static void HandleBlockRemoved(Vector3i blockPos)
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
                BrowserTvManager.RunOnMainThread(() => Debug.LogWarning("[BrowserTV] Bridge stop after removed block failed: " + ex.Message));
            }
        });
    }

    private static void Navigate(Vector3i blockPos, string url)
    {
        int requestRevision;
        string sessionId;
        lock (Sync)
        {
            if (state.Power != BrowserTvPowerState.On || !state.IsSameTv(blockPos))
            {
                return;
            }

            state.CurrentUrl = url;
            state.StatusText = "Navigating";
            state.Revision++;
            requestRevision = ++operationRevision;
            sessionId = state.SessionId;
            BroadcastStateLocked();
        }

        ThreadPool.QueueUserWorkItem(_ =>
        {
            try
            {
                if (string.IsNullOrEmpty(sessionId))
                {
                    throw new WebException("Bridge session is missing");
                }

                bridgeClient.Navigate(sessionId, url);
                BrowserTvManager.RunOnMainThread(() =>
                {
                    lock (Sync)
                    {
                        if (requestRevision != operationRevision || !state.IsSameTv(blockPos) || state.Power != BrowserTvPowerState.On || state.CurrentUrl != url)
                        {
                            return;
                        }

                        state.StatusText = "On";
                        state.Revision++;
                        BroadcastStateLocked();
                    }
                });
            }
            catch (Exception ex)
            {
                if (IsBridgeSessionMissing(ex))
                {
                    RestartSessionAfterLostBridgeSession(requestRevision, blockPos, url);
                    return;
                }

                BrowserTvManager.RunOnMainThread(() =>
                {
                    lock (Sync)
                    {
                        if (requestRevision != operationRevision || !state.IsSameTv(blockPos) || state.Power != BrowserTvPowerState.On || state.CurrentUrl != url)
                        {
                            return;
                        }

                        state.StatusText = "Navigate failed: " + ex.Message;
                        state.Revision++;
                        BroadcastStateLocked();
                    }

                    Debug.LogWarning("[BrowserTV] Bridge navigate failed: " + ex.Message);
                });
            }
        });
    }

    private static void RestartSessionAfterLostBridgeSession(int requestRevision, Vector3i blockPos, string url)
    {
        try
        {
            Debug.LogWarning("[BrowserTV] Bridge session is missing; starting a replacement session for " + blockPos);
            BrowserTvBridgeStartResult result = bridgeClient.StartSession(blockPos, url);
            BrowserTvManager.RunOnMainThread(() =>
            {
                lock (Sync)
                {
                    if (requestRevision != operationRevision || !state.IsSameTv(blockPos) || state.Power != BrowserTvPowerState.On || state.CurrentUrl != url)
                    {
                        return;
                    }

                    state.SessionId = result.SessionId;
                    state.StreamUrl = result.StreamUrl;
                    state.ViewerToken = result.ViewerToken;
                    state.ControllerToken = result.ControllerToken;
                    state.StatusText = string.IsNullOrEmpty(result.StatusText) ? "On" : result.StatusText;
                    state.Revision++;
                    BroadcastStateLocked();
                }
            });
        }
        catch (Exception restartEx)
        {
            BrowserTvManager.RunOnMainThread(() =>
            {
                lock (Sync)
                {
                    if (requestRevision != operationRevision || !state.IsSameTv(blockPos) || state.Power != BrowserTvPowerState.On || state.CurrentUrl != url)
                    {
                        return;
                    }

                    state.Power = BrowserTvPowerState.Error;
                    state.StatusText = "Bridge unavailable: " + restartEx.Message;
                    state.Revision++;
                    BroadcastStateLocked();
                }
            });
        }
    }

    private static bool IsBridgeSessionMissing(Exception ex)
    {
        if (ex.Message.IndexOf("Bridge session is missing", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return true;
        }

        if (ex is WebException webException && webException.Response is HttpWebResponse response)
        {
            return response.StatusCode == HttpStatusCode.NotFound;
        }

        return false;
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
