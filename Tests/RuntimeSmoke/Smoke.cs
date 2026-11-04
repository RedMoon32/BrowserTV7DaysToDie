using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

public class BrowserTvSmoke : IModApi
{
    public void InitMod(Mod mod) { ThreadManager.StartCoroutine(Run()); }

    private static void Check(bool condition, string description)
    {
        if (!condition) throw new Exception(description);
        Debug.Log("[BrowserTV Smoke] PASS " + description);
    }

    private static byte[] WritePacket(NetPackage packet)
    {
        using (var stream = new MemoryStream())
        {
            var writer = new PooledBinaryWriter(); writer.SetBaseStream(stream);
            packet.write(writer); writer.Flush();
            return stream.ToArray();
        }
    }

    private static void PacketRoundTrip(NetPackage original, NetPackage copy)
    {
        var bytes = WritePacket(original);
        using (var stream = new MemoryStream(bytes))
        {
            var reader = new PooledBinaryReader(); reader.SetBaseStream(stream);
            Check(reader.ReadUInt16() == original.PackageId, original.GetType().Name + " package mapping");
            copy.read(reader);
            Check(stream.Position == stream.Length, original.GetType().Name + " consumes entire packet");
        }
        Check(bytes.SequenceEqual(WritePacket(copy)), original.GetType().Name + " round trip");
    }

    private static void TileRoundTrip(StreamModeWrite writeMode, StreamModeRead readMode)
    {
        var original = new TileEntityBrowserTV(null) { localChunkPos = new Vector3i(4, 80, 9), CenteredPitch = 12f, CenteredYaw = 34f };
        using (var stream = new MemoryStream())
        {
            var writer = new PooledBinaryWriter(); writer.SetBaseStream(stream);
            original.write(writer, writeMode); writer.Flush();
            stream.Position = 0;
            var reader = new PooledBinaryReader(); reader.SetBaseStream(stream);
            var copy = TileEntity.InstantiateFromRead(reader, readMode, original.GetTileEntityType(), null, null, null) as TileEntityBrowserTV;
            Check(copy != null && copy.localChunkPos == original.localChunkPos && copy.CenteredPitch == 12f && copy.CenteredYaw == 34f,
                "TV entity " + readMode + " reconstructs position and data");
            Check(stream.Position == stream.Length, "TV entity " + readMode + " consumes entire record");
        }
    }

    private static IEnumerator Run()
    {
        for (int i = 0; i < 120; i++)
        {
            var ui = LocalPlayerUI.primaryUI;
            if (ui != null && ui.xui != null && ui.xui.IsReady) break;
            yield return new WaitForSecondsRealtime(0.5f);
        }
        try
        {
            NetPackageManager.StartServer();
            PacketRoundTrip(new BrowserTvClickPackage().Setup(new Vector3i(1, 70, -8), 0.25f, 0.75f, 42), new BrowserTvClickPackage());
            PacketRoundTrip(new BrowserTvCommandPackage().Setup(BrowserTvCommandType.Navigate, new Vector3i(-8, 75, 99), "https://example.com/тест", 0.6f, 42), new BrowserTvCommandPackage());
            PacketRoundTrip(new BrowserTvStatePackage().Setup(new BrowserTvState { Power = BrowserTvPowerState.On, SessionId = "smoke", CurrentUrl = "https://example.com/тест", Revision = 7 }), new BrowserTvStatePackage());
            TileRoundTrip(StreamModeWrite.Persistency, StreamModeRead.Persistency);
            TileRoundTrip(StreamModeWrite.ToServer, StreamModeRead.FromClient);
            // FromServer requires a live player UI and is not exercised by this headless test.
            NetPackageManager.StartClient();

            var xui = LocalPlayerUI.primaryUI.xui;
            XUi.SetXmlsForUi(false);
            xui.Load(new List<string> { "windowBrowserTvControl", "windowBrowserTvUrlInput" });
            var control = xui.GetWindow("windowBrowserTvControl").Controller as XUiC_BrowserTvControlWindow;
            Check(control != null, "control window loads with the 3.4 XML parser");
            XUiC_BrowserTvControlWindow.CurrentUrl = "https://example.com";
            control.OnOpen();
            var input = control.GetChildById("txtBrowserTvUrl") as XUiC_TextInput;
            var volume = control.GetChildById("browserTvVolumeSlider") as XUiC_ComboBoxFloat;
            Check(input != null && input.Text == "https://example.com" && input.CharacterLimit == 512, "URL field binds and opens");
            Check(volume != null && volume.Min == 0 && volume.Max == 1, "volume control binds with range 0..1");
            var method = typeof(XUiC_BrowserTvControlWindow).GetMethod("ShowVolumePanel", BindingFlags.NonPublic | BindingFlags.Instance);
            method.Invoke(control, null);
            float previous = BrowserTvLocalAudioSettings.Volume;
            volume.Value = 0.37;
            typeof(XUiC_BrowserTvControlWindow).GetMethod("VolumeSlider_OnValueChanged", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(control, new object[] { volume });
            Check(Math.Abs(BrowserTvLocalAudioSettings.Volume - 0.37f) < 0.001f, "volume changes reach local audio settings");
            BrowserTvLocalAudioSettings.SetVolume(previous);
            Check(xui.GetWindow("windowBrowserTvUrlInput").Controller is XUiC_BrowserTvUrlInputWindow, "secondary URL window loads");

            string bundlePath = Path.Combine(Path.GetDirectoryName(typeof(BrowserTvModInit).Assembly.Location), "Resources/BrowserTV.unity3d");
            var bundle = AssetBundle.LoadFromFile(bundlePath);
            Check(bundle != null, "TV asset bundle loads in Unity 2022.3");
            Check(bundle.LoadAllAssets<GameObject>().Length >= 5, "TV prefabs are available");
            bundle.Unload(true);
            Debug.Log("[BrowserTV Smoke] ALL CHECKS PASSED");
        }
        catch (Exception error) { Debug.LogError("[BrowserTV Smoke] FAILED " + error); }
        Application.Quit();
    }
}
