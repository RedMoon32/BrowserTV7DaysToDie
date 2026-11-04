# 3.4 runtime smoke test

This is a temporary test mod, not part of a normal BrowserTV installation. It loads the real 3.4 game API and BrowserTV DLL inside Unity, checks network serialization, TV entity persistence, the two XML windows, URL input, volume binding, and the bundled TV prefabs. It quits the game after reporting the result.

Build BrowserTV first, then build this project with the same `GameDirectory`:

```powershell
dotnet build Tests/RuntimeSmoke/RuntimeSmoke.csproj "-p:GameDirectory=C:\Program Files (x86)\Steam\steamapps\common\7 Days To Die"
```

Use an isolated test game installation and a separate `-UserDataFolder`. Place only `BrowserTV.Smoke.dll` from the build output in `Mods/zz_BrowserTvSmoke`, alongside a `ModInfo.xml` containing:

```xml
<xml>
  <Name value="BrowserTVSmoke" />
  <DisplayName value="BrowserTV smoke tests" />
  <Version value="1.0.0" />
  <Description value="Temporary local verification" />
  <Author value="Test" />
</xml>
```

The test installation must also contain the built BrowserTV mod and the game's `0_TFP_Harmony`. Run the game with `-batchmode -nographics -noeac -nogs -UserDataFolder=<test-data> -logfile <test-log>`. Look for `[BrowserTV Smoke] ALL CHECKS PASSED`, and check for XML/Harmony/BrowserTV errors elsewhere in the log too. Graphics and intro-video errors are expected with `-nographics`.

The test does not join a world or contact the media bridge. It does not verify electrical wiring, browser playback, raycast accuracy, or multiplayer behavior. The game API's `FromServer` tile-entity read requires a live player's UI; that path needs an in-world multiplayer test. Never install this test mod on a regular server or use it with an existing save.
