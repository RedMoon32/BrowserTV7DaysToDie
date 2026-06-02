import http from "node:http";
import crypto from "node:crypto";

const PORT = Number(process.env.PORT || 8787);
const SERVER_SECRET = process.env.BROWSER_TV_SERVER_SECRET || "change-me-browser-tv-secret";
const PUBLIC_URL = trimSlash(process.env.BROWSER_TV_PUBLIC_URL || `http://127.0.0.1:${PORT}`);
const NEKO_URL = trimSlash(process.env.NEKO_URL || "http://neko:8080");
const DEFAULT_URL = process.env.BROWSER_TV_DEFAULT_URL || "https://www.google.com";
const NEKO_PASSWORD = process.env.NEKO_PASSWORD || "";

/** @type {Map<string, any>} */
const sessions = new Map();
let activeSession = null;

const server = http.createServer(async (req, res) => {
  try {
    const url = new URL(req.url || "/", `http://${req.headers.host || "localhost"}`);
    if (req.method === "GET" && url.pathname === "/health") {
      return json(res, 200, { ok: true, active: Boolean(activeSession), nekoUrl: NEKO_URL });
    }

    if (req.method === "POST" && url.pathname === "/api/server/session/start") {
      if (!authorizeServer(req)) return json(res, 401, { error: "unauthorized" });
      const body = await readJson(req);
      const session = {
        sessionId: activeSession?.sessionId || token("sess"),
        tvId: String(body.tvId || "default"),
        currentUrl: String(body.url || DEFAULT_URL),
        viewerToken: token("view"),
        controllerToken: token("ctrl"),
        startedAt: Date.now(),
        status: "On",
        neko: null,
      };
      activeSession = session;
      sessions.set(session.sessionId, session);
      console.log(JSON.stringify({ event: "session_start", sessionId: session.sessionId, tvId: session.tvId }));
      return json(res, 200, {
        sessionId: session.sessionId,
        viewerToken: session.viewerToken,
        controllerToken: session.controllerToken,
        endpoint: PUBLIC_URL,
        status: session.status,
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
      if (session) session.currentUrl = String(body.url || DEFAULT_URL);
      console.log(JSON.stringify({ event: "browser_navigate_requested", sessionId: session?.sessionId, url: session?.currentUrl }));
      return json(res, 200, { ok: true, currentUrl: session?.currentUrl || "" });
    }

    const offerMatch = url.pathname.match(/^\/api\/client\/session\/([^/]+)\/webrtc\/offer$/);
    if (req.method === "GET" && offerMatch) {
      const session = authorizeViewer(offerMatch[1], url);
      if (!session) return json(res, 404, { error: "session not found" });
      const neko = await ensureNeko(session);
      await waitFor(() => neko.offer?.sdp, 15000);
      return json(res, 200, { sdp: neko.offer.sdp, lite: neko.offer.lite === true, ice: neko.offer.ice || [] });
    }

    const answerMatch = url.pathname.match(/^\/api\/client\/session\/([^/]+)\/webrtc\/answer$/);
    if (req.method === "POST" && answerMatch) {
      const session = authorizeViewer(answerMatch[1], url);
      if (!session) return json(res, 404, { error: "session not found" });
      const body = await readJson(req);
      const neko = await ensureNeko(session);
      neko.ws.send(JSON.stringify({ event: "signal/answer", sdp: String(body.sdp || ""), displayname: "7DTD" }));
      return json(res, 200, { ok: true });
    }

    const iceMatch = url.pathname.match(/^\/api\/client\/session\/([^/]+)\/webrtc\/ice$/);
    if (req.method === "POST" && iceMatch) {
      const session = authorizeViewer(iceMatch[1], url);
      if (!session) return json(res, 404, { error: "session not found" });
      const body = await readJson(req);
      const neko = await ensureNeko(session);
      neko.ws.send(JSON.stringify({ event: "signal/candidate", data: JSON.stringify(body) }));
      return json(res, 200, { ok: true });
    }

    if (req.method === "GET" && iceMatch) {
      const session = authorizeViewer(iceMatch[1], url);
      if (!session) return json(res, 404, { error: "session not found" });
      const neko = await ensureNeko(session);
      const since = Number(url.searchParams.get("since") || 0);
      return json(res, 200, { candidates: neko.candidates.slice(since), next: neko.candidates.length });
    }

    return json(res, 404, { error: "not found" });
  } catch (error) {
    console.error(JSON.stringify({ event: "request_error", error: String(error?.stack || error) }));
    return json(res, 500, { error: "internal error", detail: String(error?.message || error) });
  }
});

server.listen(PORT, "0.0.0.0", () => {
  console.log(JSON.stringify({ event: "bridge_start", port: PORT, publicUrl: PUBLIC_URL, nekoUrl: NEKO_URL }));
});

async function ensureNeko(session) {
  if (session.neko?.ws?.readyState === WebSocket.OPEN && session.neko.offer) return session.neko;

  const neko = { ws: null, offer: null, candidates: [] };
  session.neko = neko;
  const wsUrl = toWsUrl(`${NEKO_URL}/ws?password=${encodeURIComponent(NEKO_PASSWORD)}&username=${encodeURIComponent("7DTD")}`);
  const ws = new WebSocket(wsUrl);
  neko.ws = ws;

  ws.addEventListener("open", () => console.log(JSON.stringify({ event: "neko_ws_open", sessionId: session.sessionId })));
  ws.addEventListener("close", () => console.log(JSON.stringify({ event: "neko_ws_close", sessionId: session.sessionId })));
  ws.addEventListener("error", event => console.error(JSON.stringify({ event: "neko_ws_error", sessionId: session.sessionId, error: String(event.message || event.type) })));
  ws.addEventListener("message", event => {
    try {
      const message = JSON.parse(String(event.data));
      if (message.event === "signal/provide" || message.event === "signal/offer") {
        neko.offer = { sdp: message.sdp, lite: message.lite, ice: message.ice, id: message.id };
        console.log(JSON.stringify({ event: "neko_offer", sessionId: session.sessionId, lite: message.lite === true }));
      } else if (message.event === "signal/candidate") {
        neko.candidates.push(JSON.parse(message.data));
      }
    } catch (error) {
      console.warn(JSON.stringify({ event: "neko_message_parse_failed", error: String(error) }));
    }
  });

  await waitFor(() => ws.readyState === WebSocket.OPEN, 15000);
  return neko;
}

function authorizeServer(req) {
  return req.headers["x-browsertv-secret"] === SERVER_SECRET;
}

function authorizeViewer(sessionId, url) {
  const session = sessions.get(String(sessionId || ""));
  if (!session || url.searchParams.get("token") !== session.viewerToken) return null;
  return session;
}

function closeSession(session) {
  if (!session) return;
  try { session.neko?.ws?.close(); } catch {}
  sessions.delete(session.sessionId);
  console.log(JSON.stringify({ event: "session_stop", sessionId: session.sessionId }));
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
    await new Promise(resolve => setTimeout(resolve, 50));
  }
}

function token(prefix) {
  return `${prefix}_${crypto.randomBytes(18).toString("base64url")}`;
}

function trimSlash(value) {
  return String(value || "").replace(/\/+$/, "");
}

function toWsUrl(value) {
  return value.replace(/^http:/, "ws:").replace(/^https:/, "wss:");
}
