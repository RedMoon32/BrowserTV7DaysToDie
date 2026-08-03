using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

public sealed class BrowserTvBridgeClient
{
    private readonly BrowserTvConfig config;

    public BrowserTvBridgeClient(BrowserTvConfig config)
    {
        this.config = config;
    }

    public BrowserTvBridgeStartResult StartSession(Vector3i blockPos, string url)
    {
        string body = "{\"tvId\":\"" + JsonEscape(MakeTvId(blockPos)) + "\",\"url\":\"" + JsonEscape(url) + "\"}";
        string response = Post("/api/server/session/start", body);
        return new BrowserTvBridgeStartResult
        {
            SessionId = GetString(response, "sessionId"),
            StreamUrl = GetString(response, "streamUrl"),
            ViewerToken = GetString(response, "viewerToken"),
            ControllerToken = GetString(response, "controllerToken"),
            StatusText = GetString(response, "status")
        };
    }

    public void StopSession(string sessionId)
    {
        if (string.IsNullOrEmpty(sessionId))
        {
            return;
        }

        Post("/api/server/session/stop", "{\"sessionId\":\"" + JsonEscape(sessionId) + "\"}");
    }

    public void Navigate(string sessionId, string url)
    {
        if (string.IsNullOrEmpty(sessionId) || string.IsNullOrEmpty(url))
        {
            return;
        }

        Post("/api/server/session/navigate", "{\"sessionId\":\"" + JsonEscape(sessionId) + "\",\"url\":\"" + JsonEscape(url) + "\"}");
    }

    public void Click(string sessionId, int x, int y)
    {
        if (string.IsNullOrEmpty(sessionId))
        {
            return;
        }

        Post("/api/server/session/click", "{\"sessionId\":\"" + JsonEscape(sessionId) + "\",\"x\":" + x + ",\"y\":" + y + "}");
    }

    private string Post(string path, string body)
    {
        string url = config.BridgeInternalUrl + path;
        HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
        request.Method = "POST";
        request.ContentType = "application/json";
        request.Headers["X-BrowserTV-Secret"] = config.ServerSecret;
        byte[] bytes = Encoding.UTF8.GetBytes(body);
        request.ContentLength = bytes.Length;
        using (Stream requestStream = request.GetRequestStream())
        {
            requestStream.Write(bytes, 0, bytes.Length);
        }

        using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
        using (StreamReader reader = new StreamReader(response.GetResponseStream()))
        {
            return reader.ReadToEnd();
        }
    }

    private static string MakeTvId(Vector3i blockPos)
    {
        return blockPos.x + ":" + blockPos.y + ":" + blockPos.z;
    }

    private static string GetString(string json, string name)
    {
        Match match = Regex.Match(json ?? "", "\"" + Regex.Escape(name) + "\"\\s*:\\s*\"([^\"]*)\"");
        return match.Success ? Regex.Unescape(match.Groups[1].Value) : "";
    }

    private static string JsonEscape(string value)
    {
        return (value ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}

public sealed class BrowserTvBridgeStartResult
{
    public string SessionId;
    public string StreamUrl;
    public string ViewerToken;
    public string ControllerToken;
    public string StatusText;
}
