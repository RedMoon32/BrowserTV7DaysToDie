using System;
using HarmonyLib;
using UnityEngine;

[HarmonyPatch(typeof(RenderDisplacedCube))]
public static class BrowserTvRenderDisplacedCubePatch
{
    [HarmonyPatch("disableAllComponents")]
    [HarmonyPrefix]
    public static bool DisableAllComponentsPrefix(Transform _transform)
    {
        if (_transform == null)
        {
            return false;
        }

        try
        {
            DisableBehaviours(_transform);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[BrowserTV] RenderDisplacedCube disableAllComponents patch failed: " + ex.Message);
        }

        return false;
    }

    private static void DisableBehaviours(Transform transform)
    {
        Component[] components = transform.GetComponents<Component>();
        foreach (Component component in components)
        {
            if (component is Behaviour behaviour)
            {
                behaviour.enabled = false;
            }
        }

        foreach (Transform child in transform)
        {
            DisableBehaviours(child);
        }
    }
}
