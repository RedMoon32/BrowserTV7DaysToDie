using System.Collections;
using System.Collections.Generic;
using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Unity.WebRTC;
using UnityEngine;

public class BrowserTvWebRtcViewer : MonoBehaviour
{
    private static bool webRtcInitialized;

    private BrowserTvState state;
    private BrowserTvScreenController controller;
    private RTCPeerConnection peer;
    private Coroutine connectLoop;
    private Coroutine updateLoop;
    private int remoteIceIndex;
    private readonly List<MediaStreamTrack> receivedTracks = new List<MediaStreamTrack>();

    public void StartViewing(BrowserTvState nextState, BrowserTvScreenController screenController)
    {
        if (state != null && state.SessionId == nextState.SessionId)
        {
            state = nextState.Clone();
            controller = screenController;
            if (controller != null)
            {
                controller.SetVolume(state.Volume);
            }
            return;
        }

        state = nextState.Clone();
        controller = screenController;
        if (controller == null)
        {
            Debug.LogWarning("[BrowserTV] Cannot start WebRTC viewer because screen controller is missing at " + state.BlockPos);
            return;
        }

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

        receivedTracks.Clear();
    }

    private IEnumerator Connect()
    {
        IEnumerator routine;
        try
        {
            routine = ConnectUnsafe();
        }
        catch (Exception ex)
        {
            Fail("WebRTC viewer setup failed", ex);
            yield break;
        }

        while (true)
        {
            object current;
            try
            {
                if (!routine.MoveNext())
                {
                    yield break;
                }

                current = routine.Current;
            }
            catch (Exception ex)
            {
                Fail("WebRTC viewer failed", ex);
                yield break;
            }

            yield return current;
        }
    }

    private IEnumerator ConnectUnsafe()
    {
        BrowserTvNativePluginLoader.Load();
        if (!webRtcInitialized)
        {
            WebRTC.ConfigureNativeLogging(true, NativeLoggingSeverity.Warning);
            WebRTC.InitializeInternal();
            webRtcInitialized = true;
            Debug.Log("[BrowserTV] WebRTC context initialized.");
        }

        if (updateLoop == null)
        {
            updateLoop = StartCoroutine(SafeWebRtcUpdate());
        }

        BackgroundTask<BrowserTvWebRtcOffer> offerTask = RunBackground(FetchOffer);
        yield return WaitForBackground(offerTask);
        BrowserTvWebRtcOffer offer = offerTask.Result;
        if (string.IsNullOrEmpty(offer.Sdp))
        {
            Debug.LogError("[BrowserTV] Bridge did not provide a WebRTC offer.");
            yield break;
        }

        peer = new RTCPeerConnection();
        Debug.Log("[BrowserTV] RTCPeerConnection created.");
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
            if (e.Track != null && !receivedTracks.Contains(e.Track))
            {
                receivedTracks.Add(e.Track);
            }

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
        Debug.Log("[BrowserTV] WebRTC remote description set.");

        var answerOp = peer.CreateAnswer();
        yield return answerOp;
        if (answerOp.IsError)
        {
            Debug.LogError("[BrowserTV] CreateAnswer failed: " + answerOp.Error.message);
            yield break;
        }
        Debug.Log("[BrowserTV] WebRTC answer created.");

        RTCSessionDescription answer = answerOp.Desc;
        answer.sdp = Regex.Replace(answer.sdp ?? "", "(stereo=1;)?useinbandfec=1", "useinbandfec=1;stereo=1");
        var setLocal = peer.SetLocalDescription(ref answer);
        yield return setLocal;
        if (setLocal.IsError)
        {
            Debug.LogError("[BrowserTV] SetLocalDescription failed: " + setLocal.Error.message);
            yield break;
        }
        Debug.Log("[BrowserTV] WebRTC local description set.");

        BackgroundTask<object> answerTask = RunBackground<object>(() =>
        {
            PostAnswer(answer.sdp);
            return null;
        });
        yield return WaitForBackground(answerTask);

        StartCoroutine(PollRemoteIce());
        Debug.Log("[BrowserTV] WebRTC answer sent.");
    }

    private void Fail(string message, Exception ex)
    {
        Debug.LogError("[BrowserTV] " + message + ": " + ex.GetType().Name + ": " + ex.Message + "\n" + ex.StackTrace);
        StopViewing();
        if (controller != null)
        {
            controller.SetState(BrowserTvScreenState.Error);
        }
    }

    private IEnumerator PollRemoteIce()
    {
        while (peer != null)
        {
            BackgroundTask<string> iceTask = RunBackground(() => HttpGet("/api/client/session/" + Escape(state.SessionId) + "/webrtc/ice?token=" + Escape(state.ViewerToken) + "&since=" + remoteIceIndex));
            while (!iceTask.IsDone)
            {
                yield return null;
            }

            if (iceTask.Error != null)
            {
                Debug.LogWarning("[BrowserTV] Remote ICE poll failed: " + iceTask.Error.Message);
                yield return new WaitForSeconds(1f);
                continue;
            }

            string json = iceTask.Result;
            try
            {
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
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[BrowserTV] Remote ICE parse/add failed: " + ex.Message);
            }

            yield return new WaitForSeconds(0.2f);
        }
    }

    private IEnumerator SafeWebRtcUpdate()
    {
        IEnumerator update = WebRTC.Update();
        while (true)
        {
            object current;
            try
            {
                if (!update.MoveNext())
                {
                    yield break;
                }

                current = update.Current;
            }
            catch (Exception ex)
            {
                Fail("WebRTC update failed", ex);
                yield break;
            }

            yield return current;
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
        RunBackground<object>(() =>
        {
            HttpPost("/api/client/session/" + Escape(state.SessionId) + "/webrtc/ice?token=" + Escape(state.ViewerToken), body);
            return null;
        });
    }

    private string HttpGet(string path)
    {
        HttpWebRequest request = (HttpWebRequest)WebRequest.Create(state.BridgeEndpoint + path);
        request.Method = "GET";
        request.Timeout = 10000;
        request.ReadWriteTimeout = 10000;
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
        request.Timeout = 10000;
        request.ReadWriteTimeout = 10000;
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

    private static BackgroundTask<T> RunBackground<T>(Func<T> work)
    {
        BackgroundTask<T> task = new BackgroundTask<T>();
        ThreadPool.QueueUserWorkItem(_ =>
        {
            try
            {
                task.Complete(work());
            }
            catch (Exception ex)
            {
                task.Fail(ex);
            }
        });

        return task;
    }

    private static IEnumerator WaitForBackground<T>(BackgroundTask<T> task)
    {
        while (!task.IsDone)
        {
            yield return null;
        }

        if (task.Error != null)
        {
            throw task.Error;
        }
    }

    private sealed class BackgroundTask<T>
    {
        private readonly object sync = new object();
        private bool done;
        private T result;
        private Exception error;

        public bool IsDone
        {
            get
            {
                lock (sync)
                {
                    return done;
                }
            }
        }

        public T Result
        {
            get
            {
                lock (sync)
                {
                    return result;
                }
            }
        }

        public Exception Error
        {
            get
            {
                lock (sync)
                {
                    return error;
                }
            }
        }

        public void Complete(T value)
        {
            lock (sync)
            {
                result = value;
                done = true;
            }
        }

        public void Fail(Exception ex)
        {
            lock (sync)
            {
                error = ex;
                done = true;
            }
        }
    }

    private sealed class BrowserTvWebRtcOffer
    {
        public string Sdp;
        public bool Lite;
    }
}
