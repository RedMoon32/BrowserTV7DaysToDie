import http from "node:http";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { spawn, spawnSync } from "node:child_process";

const PORT = Number(process.env.PORT || 8787);
const SERVER_SECRET = process.env.BROWSER_TV_SERVER_SECRET || "change-me-browser-tv-secret";
const PUBLIC_URL = trimSlash(process.env.BROWSER_TV_PUBLIC_URL || `http://127.0.0.1:${PORT}`);
const DEFAULT_URL = process.env.BROWSER_TV_DEFAULT_URL || "https://www.google.com";
const MEDIA_ROOT = process.env.BROWSER_TV_MEDIA_ROOT || "/tmp/browser-tv-media";
const DISPLAY_BASE = Number(process.env.BROWSER_TV_DISPLAY_BASE || 90);
const WIDTH = Number(process.env.BROWSER_TV_WIDTH || 1280);
const HEIGHT = Number(process.env.BROWSER_TV_HEIGHT || 720);
const FPS = Number(process.env.BROWSER_TV_FPS || 25);

/** @type {Map<string, any>} */
const sessions = new Map();
let activeSession = null;

fs.mkdirSync(MEDIA_ROOT, { recursive: true });

const server = http.createServer(async (req, res) => {
  try {
    const url = new URL(req.url || "/", `http://${req.headers.host || "localhost"}`);

    if (req.method === "GET" && url.pathname === "/health") {
      return json(res, 200, {
        ok: true,
        active: Boolean(activeSession),
        mediaRoot: MEDIA_ROOT,
        media: "mpegts-av",
      });
    }

    if (req.method === "POST" && url.pathname === "/api/server/session/start") {
      if (!authorizeServer(req)) return json(res, 401, { error: "unauthorized" });
      const body = await readJson(req);
      if (activeSession) closeSession(activeSession);

      const session = createSession(String(body.tvId || "default"), String(body.url || DEFAULT_URL));
      activeSession = session;
      sessions.set(session.sessionId, session);
      await startMedia(session);

      console.log(JSON.stringify({
        event: "session_start",
        sessionId: session.sessionId,
        tvId: session.tvId,
        streamUrl: session.streamUrl,
      }));

      return json(res, 200, {
        sessionId: session.sessionId,
        viewerToken: session.viewerToken,
        controllerToken: session.controllerToken,
        endpoint: PUBLIC_URL,
        streamUrl: session.streamUrl,
        status: "On",
      });
    }

    if (req.method === "POST" && url.pathname === "/api/server/session/stop") {
      if (!authorizeServer(req)) return json(res, 401, { error: "unauthorized" });
      const body = await readJson(req);
      const session = sessions.get(String(body.sessionId || ""));
      closeSession(session);
      if (activeSession?.sessionId === body.sessionId) activeSession = null;
      return json(res, 200, { ok: true, status: "Off" });
    }

    if (req.method === "POST" && url.pathname === "/api/server/session/navigate") {
      if (!authorizeServer(req)) return json(res, 401, { error: "unauthorized" });
      const body = await readJson(req);
      const session = sessions.get(String(body.sessionId || ""));
      if (!session) return json(res, 404, { error: "session not found" });
      session.currentUrl = String(body.url || DEFAULT_URL);
      restartChromium(session);
      console.log(JSON.stringify({ event: "browser_navigate", sessionId: session.sessionId, url: session.currentUrl }));
      return json(res, 200, { ok: true, currentUrl: session.currentUrl });
    }

    if (req.method === "GET" && url.pathname.startsWith("/media/")) {
      return serveMedia(req, res, url);
    }

    return json(res, 404, { error: "not found" });
  } catch (error) {
    console.error(JSON.stringify({ event: "request_error", error: String(error?.stack || error) }));
    return json(res, 500, { error: "internal error", detail: String(error?.message || error) });
  }
});

server.listen(PORT, "0.0.0.0", () => {
  console.log(JSON.stringify({ event: "bridge_start", port: PORT, publicUrl: PUBLIC_URL }));
});

function createSession(tvId, currentUrl) {
  const sessionId = token("sess");
  const viewerToken = token("view");
  return {
    sessionId,
    tvId,
    currentUrl,
    viewerToken,
    controllerToken: token("ctrl"),
    display: `:${DISPLAY_BASE + sessions.size + 1}`,
    dir: path.join(MEDIA_ROOT, sessionId),
    pulseRuntime: path.join(MEDIA_ROOT, sessionId, "pulse-runtime"),
    pulseSink: `browsertv_${sessionId.replace(/[^A-Za-z0-9_]/g, "_")}`,
    streamUrl: `${PUBLIC_URL}/media/${sessionId}/stream.ts?token=${encodeURIComponent(viewerToken)}`,
    processes: [],
    chromium: null,
    pulse: null,
    stream: null,
  };
}

async function startMedia(session) {
  fs.rmSync(session.dir, { recursive: true, force: true });
  fs.mkdirSync(session.dir, { recursive: true });

  const xvfb = spawnLogged(session, "Xvfb", [
    session.display,
    "-screen", "0", `${WIDTH}x${HEIGHT}x24`,
    "-nolisten", "tcp",
  ]);
  session.processes.push(xvfb);
  await delay(700);

  await startPulseAudio(session);
  restartChromium(session);

  await delay(700);
}

async function startPulseAudio(session) {
  fs.mkdirSync(session.pulseRuntime, { recursive: true, mode: 0o700 });
  const pulseEnv = makePulseEnv(session);
  const pulse = spawnLogged(session, "pulseaudio", [
    "--daemonize=no",
    "--exit-idle-time=-1",
    "--log-target=stderr",
    "--disallow-exit",
  ], pulseEnv);
  session.pulse = pulse;
  session.processes.push(pulse);

  await delay(900);
  runPactl(session, ["load-module", "module-null-sink", `sink_name=${session.pulseSink}`, `sink_properties=device.description=${session.pulseSink}`]);
  runPactl(session, ["set-default-sink", session.pulseSink]);
  console.log(JSON.stringify({ event: "pulse_ready", sessionId: session.sessionId, sink: session.pulseSink }));
}

function restartChromium(session) {
  if (session.chromium) {
    killProcess(session.chromium);
    session.processes = session.processes.filter(process => process !== session.chromium);
    session.chromium = null;
  }

  const chromium = spawnLogged(session, "chromium", [
    "--no-sandbox",
    "--no-first-run",
    "--no-default-browser-check",
    "--disable-sync",
    "--disable-dev-shm-usage",
    "--disable-gpu",
    "--disable-software-rasterizer",
    "--disable-background-networking",
    "--autoplay-policy=no-user-gesture-required",
    "--lang=ru-RU",
    "--accept-lang=ru-RU,ru",
    "--window-position=0,0",
    `--window-size=${WIDTH},${HEIGHT}`,
    "--start-fullscreen",
    "--kiosk",
    session.currentUrl,
  ], makeChromiumEnv(session));

  session.chromium = chromium;
  session.processes.push(chromium);
}

function spawnLogged(session, command, args, extraEnv = {}) {
  const child = spawn(command, args, {
    env: { ...process.env, ...extraEnv },
    stdio: ["ignore", "pipe", "pipe"],
  });
  child.stdout.on("data", data => logProcess(session, command, data));
  child.stderr.on("data", data => logProcess(session, command, data));
  child.on("exit", (code, signal) => {
    console.log(JSON.stringify({ event: "process_exit", sessionId: session.sessionId, command, code, signal }));
  });
  child.on("error", error => {
    console.error(JSON.stringify({ event: "process_error", sessionId: session.sessionId, command, error: String(error?.message || error) }));
  });
  return child;
}

function logProcess(session, command, data) {
  for (const line of String(data).split(/\r?\n/)) {
    if (line.trim()) {
      console.log(JSON.stringify({ event: "process_log", sessionId: session.sessionId, command, line }));
    }
  }
}

function serveMedia(_req, res, url) {
  const match = url.pathname.match(/^\/media\/([^/]+)\/([^/]+)$/);
  if (!match) return json(res, 404, { error: "not found" });

  const session = sessions.get(match[1]);
  if (!session || url.searchParams.get("token") !== session.viewerToken) {
    return json(res, 403, { error: "forbidden" });
  }

  const name = path.basename(match[2]);
  if (name === "stream.ts") {
    return serveTransportStream(_req, res, session);
  }

  const file = path.join(session.dir, name);
  if (!file.startsWith(session.dir) || !fs.existsSync(file)) {
    return json(res, 404, { error: "not found" });
  }

  const contentType = name.endsWith(".m3u8")
    ? "application/vnd.apple.mpegurl"
    : name.endsWith(".ts")
      ? "video/mp2t"
      : "application/octet-stream";

  if (name.endsWith(".m3u8")) {
    const playlist = fs.readFileSync(file, "utf8")
      .split(/\r?\n/)
      .map(line => line.endsWith(".ts") ? `${line}?token=${encodeURIComponent(session.viewerToken)}` : line)
      .join("\n");
    res.writeHead(200, { "content-type": contentType });
    res.end(playlist);
    return;
  }

  res.writeHead(200, { "content-type": contentType });
  fs.createReadStream(file).pipe(res);
}

function serveTransportStream(req, res, session) {
  const stream = ensureTransportStream(session);

  res.writeHead(200, {
    "content-type": "video/mp2t",
    "cache-control": "no-cache",
  });

  const client = {
    res,
    closed: false,
  };
  stream.clients.add(client);
  console.log(JSON.stringify({
    event: "stream_client_join",
    sessionId: session.sessionId,
    clients: stream.clients.size,
  }));

  const stop = () => removeStreamClient(session, stream, client);
  req.on("close", stop);
  res.on("close", stop);
}

function ensureTransportStream(session) {
  if (session.stream?.ffmpeg && !session.stream.ffmpeg.killed) {
    if (session.stream.idleTimer) {
      clearTimeout(session.stream.idleTimer);
      session.stream.idleTimer = null;
    }

    return session.stream;
  }

  const stream = {
    ffmpeg: null,
    clients: new Set(),
    statsTimer: null,
    idleTimer: null,
    totalBytes: 0,
    intervalBytes: 0,
    stopping: false,
  };
  session.stream = stream;

  const ffmpeg = spawn("ffmpeg", [
    "-hide_banner",
    "-loglevel", "warning",
    "-fflags", "+genpts",
    "-thread_queue_size", "1024",
    "-use_wallclock_as_timestamps", "1",
    "-f", "x11grab",
    "-draw_mouse", "1",
    "-video_size", `${WIDTH}x${HEIGHT}`,
    "-framerate", String(FPS),
    "-i", `${session.display}.0`,
    "-thread_queue_size", "1024",
    "-use_wallclock_as_timestamps", "1",
    "-f", "pulse",
    "-i", `${session.pulseSink}.monitor`,
    "-map", "0:v:0",
    "-map", "1:a:0",
    "-c:v", "libx264",
    "-preset", "ultrafast",
    "-tune", "zerolatency",
    "-profile:v", "baseline",
    "-x264-params", "bframes=0:rc-lookahead=0:sync-lookahead=0:sliced-threads=1",
    "-pix_fmt", "yuv420p",
    "-r", String(FPS),
    "-g", String(FPS),
    "-keyint_min", String(FPS),
    "-sc_threshold", "0",
    "-c:a", "aac",
    "-b:a", "128k",
    "-ar", "48000",
    "-ac", "2",
    "-muxdelay", "0",
    "-muxpreload", "0",
    "-mpegts_flags", "+resend_headers",
    "-f", "mpegts",
    "pipe:1",
  ], {
    env: makePulseEnv(session),
    stdio: ["ignore", "pipe", "pipe"],
  });
  stream.ffmpeg = ffmpeg;

  console.log(JSON.stringify({
    event: "stream_start",
    sessionId: session.sessionId,
    display: session.display,
    sink: session.pulseSink,
    fps: FPS,
    width: WIDTH,
    height: HEIGHT,
  }));
  ffmpeg.stderr.on("data", data => logProcess(session, "ffmpeg-stream", data));
  ffmpeg.on("exit", (code, signal) => {
    console.log(JSON.stringify({ event: "stream_exit", sessionId: session.sessionId, code, signal }));
    closeStreamClients(stream);
    clearTimeout(stream.idleTimer);
    if (session.stream === stream) {
      clearInterval(stream.statsTimer);
      session.stream = null;
    }
  });
  ffmpeg.on("error", error => {
    console.error(JSON.stringify({ event: "stream_error", sessionId: session.sessionId, error: String(error?.message || error) }));
    closeStreamClients(stream);
  });

  stream.statsTimer = setInterval(() => {
    console.log(JSON.stringify({
      event: "stream_bytes",
      sessionId: session.sessionId,
      clients: stream.clients.size,
      intervalBytes: stream.intervalBytes,
      totalBytes: stream.totalBytes,
    }));
    stream.intervalBytes = 0;
  }, 2000);

  ffmpeg.stdout.on("data", chunk => {
    stream.totalBytes += chunk.length;
    stream.intervalBytes += chunk.length;
    for (const client of stream.clients) {
      if (client.closed || client.res.destroyed || client.res.writableEnded) {
        removeStreamClient(session, stream, client);
        continue;
      }

      if (client.res.writableLength > 4 * 1024 * 1024) {
        console.warn(JSON.stringify({
          event: "stream_client_drop_slow",
          sessionId: session.sessionId,
          bufferedBytes: client.res.writableLength,
        }));
        client.res.destroy();
        removeStreamClient(session, stream, client);
        continue;
      }

      client.res.write(chunk);
    }
  });

  return stream;
}

function removeStreamClient(session, stream, client) {
  if (client.closed) {
    return;
  }

  client.closed = true;
  stream.clients.delete(client);
  console.log(JSON.stringify({
    event: "stream_client_leave",
    sessionId: session.sessionId,
    clients: stream.clients.size,
  }));

  if (stream.clients.size === 0 && !stream.idleTimer && !stream.stopping) {
    stream.idleTimer = setTimeout(() => {
      if (stream.clients.size === 0 && session.stream === stream) {
        stopTransportStream(session, stream, "idle");
      }
    }, 5000);
  }
}

function stopTransportStream(session, stream = session.stream, reason = "stop") {
  if (!stream || stream.stopping) {
    return;
  }

  stream.stopping = true;
  clearTimeout(stream.idleTimer);
  clearInterval(stream.statsTimer);
  closeStreamClients(stream);
  if (stream.ffmpeg) {
    killProcess(stream.ffmpeg);
  }

  if (session.stream === stream) {
    session.stream = null;
  }

  console.log(JSON.stringify({ event: "stream_stop", sessionId: session.sessionId, reason }));
}

function closeStreamClients(stream) {
  for (const client of stream.clients) {
    client.closed = true;
    try {
      if (!client.res.destroyed && !client.res.writableEnded) {
        client.res.end();
      }
    } catch {}
  }

  stream.clients.clear();
}

function closeSession(session) {
  if (!session) return;
  stopTransportStream(session, session.stream, "session_close");
  for (const child of session.processes.slice().reverse()) {
    killProcess(child);
  }
  sessions.delete(session.sessionId);
  fs.rmSync(session.dir, { recursive: true, force: true });
  console.log(JSON.stringify({ event: "session_stop", sessionId: session.sessionId }));
}

function killProcess(child) {
  try {
    if (!child.killed) child.kill("SIGTERM");
  } catch {}
}

function makePulseEnv(session) {
  return {
    ...process.env,
    XDG_RUNTIME_DIR: session.pulseRuntime,
    PULSE_RUNTIME_PATH: path.join(session.pulseRuntime, "pulse"),
    PULSE_SERVER: `unix:${path.join(session.pulseRuntime, "pulse", "native")}`,
  };
}

function makeChromiumEnv(session) {
  return {
    ...makePulseEnv(session),
    DISPLAY: session.display,
    PULSE_SINK: session.pulseSink,
    LANG: "ru_RU.UTF-8",
    LC_ALL: "ru_RU.UTF-8",
    LANGUAGE: "ru_RU:ru",
  };
}

function runPactl(session, args) {
  const result = spawnSync("pactl", args, {
    env: makePulseEnv(session),
    encoding: "utf8",
  });
  if (result.status !== 0) {
    throw new Error(`pactl ${args.join(" ")} failed: ${result.stderr || result.stdout}`);
  }
  if (result.stdout?.trim()) {
    logProcess(session, "pactl", result.stdout);
  }
}

function authorizeServer(req) {
  return req.headers["x-browsertv-secret"] === SERVER_SECRET;
}

async function readJson(req) {
  const chunks = [];
  for await (const chunk of req) chunks.push(chunk);
  if (!chunks.length) return {};
  return JSON.parse(Buffer.concat(chunks).toString("utf8"));
}

function json(res, status, body) {
  const bytes = Buffer.from(JSON.stringify(body));
  res.writeHead(status, { "content-type": "application/json", "content-length": bytes.length });
  res.end(bytes);
}

async function waitFor(predicate, timeoutMs) {
  const start = Date.now();
  while (!predicate()) {
    if (Date.now() - start > timeoutMs) throw new Error("timeout");
    await delay(100);
  }
}

function delay(ms) {
  return new Promise(resolve => setTimeout(resolve, ms));
}

function token(prefix) {
  return `${prefix}_${crypto.randomBytes(18).toString("base64url")}`;
}

function trimSlash(value) {
  return String(value || "").replace(/\/+$/, "");
}
