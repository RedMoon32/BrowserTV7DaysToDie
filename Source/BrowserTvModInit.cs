using System;
using System.IO;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

public class BrowserTvModInit : IModApi
{
    public void InitMod(Mod modInstance)
    {
        string modPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        Debug.Log("[BrowserTV] Initializing BrowserTV mod from " + modPath);
        BrowserTvConfig.Load();
        RegisterNetworkPackages();
        BrowserTvServerStateService.Initialize();
        new Harmony("BrowserTV.Mod").PatchAll(Assembly.GetExecutingAssembly());
        BrowserTvManager.EnsureCreated();
        Debug.Log("[BrowserTV] BrowserTV initialized. Phase 1 shell active; no media is sent through 7DTD NetPackages.");
    }

    private static void RegisterNetworkPackages()
    {
        RegisterNetPackage<BrowserTvStatePackage>();
        RegisterNetPackage<BrowserTvCommandPackage>();
        RegisterNetPackage<BrowserTvClickPackage>();
    }

    private static void RegisterNetPackage<TPackage>() where TPackage : NetPackage
    {
        Type type = typeof(TPackage);
        NetPackageManager.knownPackageTypes[type.Name] = type;
        Debug.Log("[BrowserTV] Registered net package " + type.Name);
    }
}
