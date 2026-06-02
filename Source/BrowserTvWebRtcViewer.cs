using System.Collections;
using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Unity.WebRTC;
using UnityEngine;

public class BrowserTvWebRtcViewer : MonoBehaviour
{
    private BrowserTvState state;
    private BrowserTvScreenController controller;
    private RTCPeerConnection peer;
    private Coroutine connectLoop;
    private Coroutine updateLoop;
    private int remoteIceIndex;

    public void StartViewing(BrowserTvState nextState, BrowserTvScreenController screenController)
    {
        if (state != null && state.SessionId == nextState.SessionId && state.Revision == nextState.Revision)
        {
            return;
        }

        state = nextState.Clone();
        controller = screenController;
        controller.SetVolume(state.Volume);

        StopViewing();
        connectLoop = StartCoroutine(Connect());
    }

    public void StopViewing()
    {
        if (connectLoop != null)
        {
            StopCoroutine(connectLoop);
            connectLoop = null;
        }

        if (updateLoop != null)
        {
            StopCoroutine(updateLoop);
            updateLoop = null;
        }

        if (peer != null)
        {
            peer.Close();
            peer.Dispose();
            peer = null;
        }
    }

    private IEnumerator Connect()
    {
        BrowserTvNativePluginLoader.Load();
        if (updateLoop == null)
        {
            updateLoop = StartCoroutine(WebRTC.Update());
        }

        BrowserTvWebRtcOffer offer = FetchOffer();
        if (string.IsNullOrEmpty(offer.Sdp))
        {
            Debug.LogError("[BrowserTV] Bridge did not provide a WebRTC offer.");
            yield break;
        }

        RTCConfiguration config = default;
        peer = offer.Lite ? new RTCPeerConnection() : new RTCPeerConnection(ref config);
        peer.OnIceCandidate = candidate =>
        {
            if (candidate == null)
            {
                return;
            }

            PostIce(candidate);
        };
        peer.OnTrack = e =>
        {
            if (e.Track is VideoStreamTrack videoTrack)
            {
                videoTrack.OnVideoReceived += texture => controller.SetExternalTexture(texture);
                Debug.Log("[BrowserTV] WebRTC video track received.");
            }
            else if (e.Track is AudioStreamTrack audioTrack)
            {
                AudioSource audio = controller.GetComponent<AudioSource>();
                if (audio != null)
                {
                    audio.SetTrack(audioTrack);
                    audio.loop = true;
                    audio.Play();
                    Debug.Log("[BrowserTV] WebRTC audio track received.");
                }
            }
        };

        var remoteDesc = new RTCSessionDescription { type = RTCSdpType.Offer, sdp = offer.Sdp };
        var setRemote = peer.SetRemoteDescription(ref remoteDesc);
        yield return setRemote;
        if (setRemote.IsError)
        {
            Debug.LogError("[BrowserTV] SetRemoteDescription failed: " + setRemote.Error.message);
            yield break;
        }

        var answerOp = peer.CreateAnswer();
        yield return answerOp;
        if (answerOp.IsError)
        {
            Debug.LogError("[BrowserTV] CreateAnswer failed: " + answerOp.Error.message);
            yield break;
        }

        RTCSessionDescription answer = answerOp.Desc;
        answer.sdp = Regex.Replace(answer.sdp ?? "", "(stereo=1;)?useinbandfec=1", "useinbandfec=1;stereo=1");
        var setLocal = peer.SetLocalDescription(ref answer);
        yield return setLocal;
        if (setLocal.IsError)
        {
            Debug.LogError("[BrowserTV] SetLocalDescription failed: " + setLocal.Error.message);
            yield break;
        }

        PostAnswer(answer.sdp);
        StartCoroutine(PollRemoteIce());
        Debug.Log("[BrowserTV] WebRTC answer sent.");
    }

    private IEnumerator PollRemoteIce()
    {
        while (peer != null)
        {
            string json = HttpGet("/api/client/session/" + Escape(state.SessionId) + "/webrtc/ice?token=" + Escape(state.ViewerToken) + "&since=" + remoteIceIndex);
            foreach (Match match in Regex.Matches(json ?? "", "\\{\\s*\"candidate\"\\s*:\\s*\"([^\"]*)\"\\s*,\\s*\"sdpMid\"\\s*:\\s*\"([^\"]*)\"\\s*,\\s*\"sdpMLineIndex\"\\s*:\\s*(\\d+)\\s*\\}"))
            {
                RTCIceCandidateInit init = new RTCIceCandidateInit
                {
                    candidate = Regex.Unescape(match.Groups[1].Value),
                    sdpMid = Regex.Unescape(match.Groups[2].Value),
                    sdpMLineIndex = int.Parse(match.Groups[3].Value)
                };
                peer.AddIceCandidate(new RTCIceCandidate(init));
                remoteIceIndex++;
            }

            yield return new WaitForSeconds(0.2f);
        }
    }

    private BrowserTvWebRtcOffer FetchOffer()
    {
        string json = HttpGet("/api/client/session/" + Escape(state.SessionId) + "/webrtc/offer?token=" + Escape(state.ViewerToken));
        return new BrowserTvWebRtcOffer
        {
            Sdp = GetString(json, "sdp"),
            Lite = GetBool(json, "lite")
        };
    }

    private void PostAnswer(string sdp)
    {
        HttpPost("/api/client/session/" + Escape(state.SessionId) + "/webrtc/answer?token=" + Escape(state.ViewerToken), "{\"sdp\":\"" + JsonEscape(sdp) + "\"}");
    }

    private void PostIce(RTCIceCandidate candidate)
    {
        string body = "{\"candidate\":\"" + JsonEscape(candidate.Candidate) + "\",\"sdpMid\":\"" + JsonEscape(candidate.SdpMid) + "\",\"sdpMLineIndex\":" + candidate.SdpMLineIndex.GetValueOrDefault() + "}";
        HttpPost("/api/client/session/" + Escape(state.SessionId) + "/webrtc/ice?token=" + Escape(state.ViewerToken), body);
    }

    private string HttpGet(string path)
    {
        HttpWebRequest request = (HttpWebRequest)WebRequest.Create(state.BridgeEndpoint + path);
        request.Method = "GET";
        using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
        using (StreamReader reader = new StreamReader(response.GetResponseStream()))
        {
            return reader.ReadToEnd();
        }
    }

    private void HttpPost(string path, string body)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(body);
        HttpWebRequest request = (HttpWebRequest)WebRequest.Create(state.BridgeEndpoint + path);
        request.Method = "POST";
        request.ContentType = "application/json";
        request.ContentLength = bytes.Length;
        using (Stream stream = request.GetRequestStream())
        {
            stream.Write(bytes, 0, bytes.Length);
        }

        using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
        {
            response.Close();
        }
    }

    private static string GetString(string json, string name)
    {
        Match match = Regex.Match(json ?? "", "\"" + Regex.Escape(name) + "\"\\s*:\\s*\"([\\s\\S]*?)\"");
        return match.Success ? Regex.Unescape(match.Groups[1].Value) : "";
    }

    private static bool GetBool(string json, string name)
    {
        Match match = Regex.Match(json ?? "", "\"" + Regex.Escape(name) + "\"\\s*:\\s*(true|false)", RegexOptions.IgnoreCase);
        return match.Success && bool.Parse(match.Groups[1].Value);
    }

    private static string Escape(string value)
    {
        return Uri.EscapeDataString(value ?? "");
    }

    private static string JsonEscape(string value)
    {
        return (value ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n");
    }

    private sealed class BrowserTvWebRtcOffer
    {
        public string Sdp;
        public bool Lite;
    }
}
