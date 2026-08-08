using System;
using System.Collections.Generic;
using UnityEngine;

public class BrowserTvManager : MonoBehaviour
{
    private static BrowserTvManager instance;
    private static readonly Queue<Action> MainThreadActions = new Queue<Action>();
    private readonly Dictionary<Vector3i, BrowserTvScreenController> controllers = new Dictionary<Vector3i, BrowserTvScreenController>();

    public static BrowserTvManager Instance
    {
        get
        {
            EnsureCreated();
            return instance;
        }
    }

    public static void EnsureCreated()
    {
        if (instance != null)
        {
            return;
        }

        GameObject gameObject = new GameObject("BrowserTvManager");
        instance = gameObject.AddComponent<BrowserTvManager>();
        DontDestroyOnLoad(gameObject);
    }

    public static void RunOnMainThread(Action action)
    {
        if (action == null)
        {
            return;
        }

        EnsureCreated();
        lock (MainThreadActions)
        {
            MainThreadActions.Enqueue(action);
        }
    }

    private void Update()
    {
        while (true)
        {
            Action action;
            lock (MainThreadActions)
            {
                if (MainThreadActions.Count == 0)
                {
                    return;
                }

                action = MainThreadActions.Dequeue();
            }

            try
            {
                action();
            }
            catch (Exception ex)
            {
                Debug.LogError("[BrowserTV] Main-thread action failed: " + ex.GetType().Name + ": " + ex.Message + "\n" + ex.StackTrace);
            }
        }
    }

    public void Register(Vector3i worldPos, BrowserTvScreenController controller)
    {
        if (controller == null)
        {
            return;
        }

        controllers[worldPos] = controller;
        Debug.Log("[BrowserTV] Registered TV screen controller at " + worldPos);
        BrowserTvClientStateService.ReapplyIfCurrentTv(worldPos);
        RequestServerState();
    }

    public void Unregister(Vector3i worldPos)
    {
        if (controllers.TryGetValue(worldPos, out BrowserTvScreenController controller) && controller != null)
        {
            controller.SetState(BrowserTvScreenState.Off);
        }

        controllers.Remove(worldPos);
        Debug.Log("[BrowserTV] Unregistered TV screen controller at " + worldPos);
    }

    public BrowserTvScreenController GetController(Vector3i worldPos)
    {
        controllers.TryGetValue(worldPos, out BrowserTvScreenController controller);
        return controller;
    }

    public System.Collections.Generic.List<BrowserTvScreenController> GetAllControllers()
    {
        return new System.Collections.Generic.List<BrowserTvScreenController>(controllers.Values);
    }

    public void SetScreenState(Vector3i worldPos, BrowserTvScreenState state)
    {
        BrowserTvScreenController controller = GetController(worldPos);
        if (controller == null)
        {
            Debug.LogWarning("[BrowserTV] No screen controller registered at " + worldPos + " for state " + state);
            return;
        }

        controller.SetState(state);
    }

    private static void RequestServerState()
    {
        if (SingletonMonoBehaviour<ConnectionManager>.Instance == null)
        {
            return;
        }

        if (SingletonMonoBehaviour<ConnectionManager>.Instance.IsClient)
        {
            SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(new BrowserTvCommandPackage().Setup(BrowserTvCommandType.RequestState, Vector3i.zero, string.Empty, 0f, -1), false);
            return;
        }

        BrowserTvServerStateService.BroadcastState();
    }
}
