# BrowserTV

<img width="1280" height="719" alt="photo_2026-06-03_15-29-01" src="https://github.com/user-attachments/assets/09c79ee0-b15e-4db5-a1c0-c86212cea791" />

BrowserTV is a 7 Days to Die mod that adds powered TV blocks capable of showing a remote browser page in-game. The game mod sends only control and state messages through 7DTD NetPackages; browser rendering and media encoding are handled by a separate Docker bridge.

## What It Includes

- Browser TV blocks and recipes for 7 Days to Die.
- A Unity/LibVLC client viewer that plays the bridge stream on the TV mesh.
- A Docker bridge that runs Chromium in Xvfb, captures video/audio with ffmpeg, and serves an MPEG-TS stream.
- One ffmpeg encoder per active browser session, with multiple viewers fanned out from the same stream.

## Requirements

- 7 Days to Die with mod loading enabled.
- Windows client for the current LibVLC bundle.
- Docker Desktop or Docker Engine for the bridge.
- Network access from clients to the bridge public URL.

The current package is designed for a small trusted server. It is not hardened for public untrusted players.

## Installation

The mod has two parts that do not have to run on the same machine:

- The **game mod** (this folder) — the 7DTD blocks, UI and logic. It runs on the dedicated server and on every client that should watch TVs.
- The **media server** (the `bridge/` folder plus `docker-compose.yml`, referred to as the *bridge*) — a Docker container that renders and streams the browser. It can run on the same machine or on a separate server with Docker; clients only need HTTP access to it.

### 1. Install the game mod (server and every client)

1. **Get the files.** On GitHub press **Code ▾ → Download ZIP** (or `git clone https://github.com/RedMoon32/BrowserTV7DaysToDie.git`), then extract the ZIP anywhere.

2. **Name the folder.** GitHub names the extracted folder after the repo + branch, e.g. `BrowserTV7DaysToDie-main`. Rename it to `BrowserTV` so it matches the paths in this README.

3. **Find the game folder in Steam.** In the Steam library, right-click **7 Days to Die** → **Manage** → **Browse local files**. That opens the game folder, e.g.:

   ```text
   C:\Program Files (x86)\Steam\steamapps\common\7 Days To Die
   ```

4. **Open the Mods folder.** Inside the game folder open `Mods` (create the folder if it does not exist yet). The mod must end up so that `ModInfo.xml` is directly inside a folder under `Mods`:

   ```text
   ...\7 Days To Die\Mods\BrowserTV\ModInfo.xml
   ```

5. **Copy the mod folder** (`BrowserTV` from step 2) into `Mods`.

6. **Repeat on every machine that plays** — the dedicated server (its own `...\7 Days To Die\Mods\` folder) and every client. Use the same build everywhere so the server and client code stays in sync.

7. **Enable mods on clients.** 7 Days to Die must be allowed to load mods (EAC / advanced anti-cheat off or mods enabled), otherwise the blocks never appear.

### 2. Run the media bridge

The bridge is a small Node.js container. For each active TV it starts a headless Chromium on a virtual display (Xvfb), captures the screen and audio with ffmpeg, and serves the resulting MPEG-TS stream to every client watching that TV — all clients share one encoder, so rendering and encoding never happen on the game machines. The game only sends it small control commands (start/stop, URL, clicks).

1. Install Docker on the bridge host (a Linux server, or Windows with Docker Desktop) and place this folder there — any copy containing `docker-compose.yml` and `bridge/` works.

2. Copy `.env.example` to `.env`, set your bridge address and a strong secret (see Configuration below), then start the bridge from that folder:

   ```powershell
   docker compose up -d --build browser-tv-bridge
   ```

3. Check the bridge:

   ```powershell
   Invoke-RestMethod http://127.0.0.1:8787/health
   ```

4. Copy `Config/browser-tv.example.json` to `Config/browser-tv.json` on the game installation and point it at the bridge: `bridgeInternalUrl` is used by the game server, `bridgePublicUrl` must be reachable by every client (hostname or LAN IP of the bridge host). `serverSecret` in the config must equal `BROWSER_TV_SERVER_SECRET` in the bridge host's `.env`.

5. Start the game/server and place a BrowserTV block.

The bridge serves plain HTTP. If clients connect from outside the LAN, put it behind a reverse proxy with TLS and keep the secret strong. The game side is Windows-only: the bundled native LibVLC client lives in `libvlc/win-x64`; the bridge itself is a Linux container and runs fine on Windows via Docker Desktop.

## Configuration

Game-side settings live in `Config/browser-tv.json`. This file contains your `serverSecret`, so it is **per-install and intentionally not committed** to the repository.

On a fresh install, copy the committed template and fill it in:

```powershell
Copy-Item Config/browser-tv.example.json Config/browser-tv.json
```

On the bridge host, copy `.env.example` to `.env` and edit it before starting Docker:

```powershell
Copy-Item .env.example .env
```

For an existing installation, back up your configuration before updating and keep its URLs and secret. Do not overwrite it with the template. Update the mod on the server and every client together, and rebuild the bridge: the remote uses a new network packet format.

Example (`Config/browser-tv.example.json`):

```json
{
  "enableBrowserTv": true,
  "bridgeInternalUrl": "http://127.0.0.1:8787",
  "bridgePublicUrl": "http://127.0.0.1:8787",
  "serverSecret": "change-me-browser-tv-secret",
  "defaultUrl": "https://www.google.com",
  "spatialAudioEnabled": true,
  "audioMinDistance": 2.0,
  "audioMaxDistance": 20.0,
  "audioRolloffPower": 1.5,
  "browserWidth": 1280,
  "browserHeight": 720
}
```

`bridgeInternalUrl` is used by the game server to control the bridge. `bridgePublicUrl` is sent to clients and must be reachable by every player who should see the TV stream. On a dedicated server, this usually needs to be the server LAN IP or public hostname, not `127.0.0.1`.

The Docker bridge reads settings from `.env` through `docker-compose.yml`, especially:

- `BROWSER_TV_SERVER_SECRET`
- `BROWSER_TV_PUBLIC_URL`
- `BROWSER_TV_DEFAULT_URL`
- `BROWSER_TV_WIDTH`
- `BROWSER_TV_HEIGHT`
- `BROWSER_TV_FPS`
- `BROWSER_TV_CONSENT_COOKIES` (optional JSON array of `{ "domain": "...", "value": "..." }` entries overriding the default Google/YouTube consent cookies)
- `BROWSER_TV_MEDIA_ROOT` (default `/tmp/browser-tv-media`, scratch/session files for the bridge)
- `BROWSER_TV_DISPLAY_BASE` (default `90`, first Xvfb display number used for sessions)

`Config/browser-tv.json` and `.env` are ignored by Git. Only the example files belong in the repository; do not force-add local configuration or put real secrets in the examples.

For a real server, replace the placeholder with the same strong secret in `Config/browser-tv.json` and `.env`. Keep `browserWidth`/`browserHeight` equal to `BROWSER_TV_WIDTH`/`BROWSER_TV_HEIGHT` so remote clicks land at the correct position.

For sessions starting on Google or YouTube, the bridge seeds consent cookies in the Chromium profile before opening the page. This keeps the consent-window handling alongside remote clicks; cookie values can be overridden using `BROWSER_TV_CONSENT_COOKIES` if the sites change them.

## Usage

Power the TV block and interact with it to open the Browser TV URL input. The URL field is focused immediately, while the Volume button opens the local volume control.

Volume is stored locally for each client and is not synchronized through the server. The selected percentage is the volume heard while standing near the TV; distance attenuation is still applied on top of it according to the TV block type. The default local volume is 100%.

The bridge opens the selected URL in Chromium and streams the captured browser window back to clients through LibVLC.

## Browser TV Remote

Craft `Browser TV Remote` at a workbench. With the remote held, aim at the visible browser screen and use the primary action to send a left-click to that exact point in the browser page. The game client resolves the screen mesh UV, while the game server validates the active TV, held remote, range, and click rate before it forwards the click to the bridge.

The bridge uses Chromium's local DevTools endpoint only inside its container; do not publish port `9222`.

Only one BrowserTV session is currently intended to be active at a time. Multiple players can watch the same active TV without spawning multiple ffmpeg encoders.

## Current Limitations

- Current native LibVLC bundle is Windows x64 only.
- TV playback state is runtime state; it is not fully restored after server restart.
- The bridge is intended for trusted use. Do not expose it publicly without changing the secret and adding network restrictions.
- URL validation is minimal. Trusted players are assumed.
- The current architecture is best suited for a small private group.

## Development

Build the C# mod:

```powershell
dotnet build Source/BrowserTV.csproj -v:minimal
```

The project writes `BrowserTV.dll` to the mod root. Generated `bin`/`obj` folders should not be committed.

Useful bridge commands:

```powershell
docker compose up -d --build browser-tv-bridge
docker logs -f browser-tv-bridge
docker compose down
```

## Repository Notes

The old `YoutubeTVMod` reference copy and diagnostic captures are intentionally not part of this repository. Keep the repo focused on BrowserTV source, game assets, runtime dependencies, and bridge code.
