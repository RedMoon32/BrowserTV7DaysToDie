import http from "node:http";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { spawn } from "node:child_process";

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
        media: "mpegts",
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
    streamUrl: `${PUBLIC_URL}/media/${sessionId}/stream.ts?token=${encodeURIComponent(viewerToken)}`,
    processes: [],
    chromium: null,
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

  restartChromium(session);

  await delay(700);
}

function restartChromium(session) {
  if (session.chromium) {
    killProcess(session.chromium);
    session.processes = session.processes.filter(process => process !== session.chromium);
    session.chromium = null;
  }

  const chromium = spawnLogged(session, "chromium", [
    "--no-sandbox",
    "--disable-dev-shm-usage",
    "--disable-gpu",
    "--disable-software-rasterizer",
    "--disable-background-networking",
    "--window-position=0,0",
    `--window-size=${WIDTH},${HEIGHT}`,
    "--start-fullscreen",
    "--kiosk",
    session.currentUrl,
  ], { DISPLAY: session.display });

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
  const ffmpeg = spawn("ffmpeg", [
    "-hide_banner",
    "-loglevel", "warning",
    "-f", "x11grab",
    "-draw_mouse", "1",
    "-video_size", `${WIDTH}x${HEIGHT}`,
    "-framerate", String(FPS),
    "-i", `${session.display}.0`,
    "-an",
    "-c:v", "libx264",
    "-preset", "veryfast",
    "-tune", "zerolatency",
    "-pix_fmt", "yuv420p",
    "-g", String(FPS),
    "-keyint_min", String(FPS),
    "-sc_threshold", "0",
    "-f", "mpegts",
    "pipe:1",
  ], {
    env: process.env,
    stdio: ["ignore", "pipe", "pipe"],
  });

  console.log(JSON.stringify({ event: "stream_start", sessionId: session.sessionId }));
  ffmpeg.stderr.on("data", data => logProcess(session, "ffmpeg-stream", data));
  ffmpeg.on("exit", (code, signal) => {
    console.log(JSON.stringify({ event: "stream_exit", sessionId: session.sessionId, code, signal }));
  });
  ffmpeg.on("error", error => {
    console.error(JSON.stringify({ event: "stream_error", sessionId: session.sessionId, error: String(error?.message || error) }));
  });

  res.writeHead(200, {
    "content-type": "video/mp2t",
    "cache-control": "no-cache",
  });
  ffmpeg.stdout.pipe(res);

  const stop = () => killProcess(ffmpeg);
  req.on("close", stop);
  res.on("close", stop);
}

function closeSession(session) {
  if (!session) return;
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
