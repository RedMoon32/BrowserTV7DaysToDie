using System;
using UnityEngine;

public class BrowserTvInitializer : MonoBehaviour
{
    private BrowserTvScreenController controller;

    public void Initialize(TileEntityBrowserTV parentTileEntity)
    {
        try
        {
            Transform screen = FindScreenObject();
            if (screen == null)
            {
                Debug.LogError("[BrowserTV] Could not find a screen mesh under " + gameObject.name);
                return;
            }

            Renderer renderer = screen.GetComponent<Renderer>();
            if (renderer == null)
            {
                Debug.LogError("[BrowserTV] Screen object has no Renderer: " + screen.name);
                return;
            }

            controller = screen.gameObject.GetComponent<BrowserTvScreenController>();
            if (controller == null)
            {
                controller = screen.gameObject.AddComponent<BrowserTvScreenController>();
            }

            controller.ParentTileEntity = parentTileEntity;
            controller.OwnerTransform = transform;
            controller.Initialize(renderer);

            if (parentTileEntity != null)
            {
                Vector3i worldPos = ((TileEntity)parentTileEntity).ToWorldPos();
                BrowserTvManager.Instance.Register(worldPos, controller);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("[BrowserTV] Failed to initialize screen: " + ex.Message + "\n" + ex.StackTrace);
        }
    }

    private Transform FindScreenObject()
    {
        string[] names = { "FilePlane", "Screen", "TVScreen", "DisplayPanel", "VideoScreen", "MonitorScreen" };
        foreach (string name in names)
        {
            Transform found = FindChildRecursive(transform, name);
            if (found != null)
            {
                return found;
            }
        }

        MeshRenderer renderer = GetComponentInChildren<MeshRenderer>();
        if (renderer != null && renderer.transform != transform)
        {
            return renderer.transform;
        }

        return null;
    }

    private static Transform FindChildRecursive(Transform parent, string targetName)
    {
        if (parent.name.Equals(targetName, StringComparison.OrdinalIgnoreCase) || parent.name.Contains(targetName))
        {
            return parent;
        }

        foreach (Transform child in parent)
        {
            Transform found = FindChildRecursive(child, targetName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}
