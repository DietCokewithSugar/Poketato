using System;
using System.Collections.Generic;
using System.Text;
#if !UNITY_WEBGL || UNITY_EDITOR
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
#endif
using UnityEngine;

namespace Poketato.Backend
{
    /// <summary>
    /// WebSocket to the authoritative game server. WebGL uses the browser socket in PoketatoBackend.jslib.
    /// The editor and standalone players use ClientWebSocket. Call Pump from Update.
    /// </summary>
    public sealed class GameSocket
    {
        readonly Queue<string> inbox = new Queue<string>();
#if !UNITY_WEBGL || UNITY_EDITOR
        readonly object gate = new object();
#else
        byte[] buffer = new byte[32768];
        int jsId;
#endif
        bool started;
        bool closeNotified;

        public int ReadyState { get; private set; } = 3;
        public event Action<string> Message;
        public event Action Closed;

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")] static extern int PoketatoWsOpen(string url);
        [System.Runtime.InteropServices.DllImport("__Internal")] static extern int PoketatoWsSend(int id, string msg);
        [System.Runtime.InteropServices.DllImport("__Internal")] static extern void PoketatoWsClose(int id);
        [System.Runtime.InteropServices.DllImport("__Internal")] static extern int PoketatoWsState(int id);
        [System.Runtime.InteropServices.DllImport("__Internal")] static extern int PoketatoWsPop(int id, byte[] buf, int len);
#else
        ClientWebSocket socket;
        CancellationTokenSource cts;
#endif

        public void Connect(string url)
        {
            if (string.IsNullOrEmpty(url) || !(url.StartsWith("ws://") || url.StartsWith("wss://")))
                throw new ArgumentException("websocket url must start with ws:// or wss://");
            Close();
            started = true;
            closeNotified = false;
            ReadyState = 0;
#if UNITY_WEBGL && !UNITY_EDITOR
            jsId = PoketatoWsOpen(url);
#else
            ConnectEditor(url);
#endif
        }

        public void Send(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
#if UNITY_WEBGL && !UNITY_EDITOR
            if (jsId != 0) PoketatoWsSend(jsId, json);
#else
            SendEditor(json);
#endif
        }

        public void QueueRaid(string mode, string characterId, string accessToken, string species, string name)
        {
            var body = JsonValue.Obj()
                .Set("t", "queue")
                .Set("mode", mode)
                .Set("characterId", characterId ?? "")
                .Set("token", accessToken ?? "")
                .Set("species", species ?? "")
                .Set("name", name ?? "");
            Send(body.ToJson());
        }

        public void Input(float moveX, float moveY)
        {
            Input(0, moveX, moveY, moveX, moveY, 0, false);
        }

        /// <summary>Design protocol: move and aim are [x, z], skill 0 is the basic attack, 1-4 are the hotbar.</summary>
        public void Input(int seq, float moveX, float moveY, float aimX, float aimY, int skill, bool interact)
        {
            var culture = System.Globalization.CultureInfo.InvariantCulture;
            Send("{\"t\":\"input\",\"seq\":" + seq.ToString(culture)
                + ",\"move\":[" + moveX.ToString("0.###", culture) + "," + moveY.ToString("0.###", culture)
                + "],\"aim\":[" + aimX.ToString("0.###", culture) + "," + aimY.ToString("0.###", culture)
                + "],\"skill\":" + skill.ToString(culture)
                + ",\"interact\":" + (interact ? "true" : "false") + "}");
        }

        public void Extract() => Send("{\"t\":\"extract\"}");
        public void Leave() => Send("{\"t\":\"leave\"}");

        public void Pump()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (jsId != 0)
            {
                ReadyState = PoketatoWsState(jsId);
                while (true)
                {
                    int n = PoketatoWsPop(jsId, buffer, buffer.Length);
                    if (n == 0) break;
                    if (n < 0)
                    {
                        buffer = new byte[buffer.Length * 2];
                        continue;
                    }
                    Enqueue(Encoding.UTF8.GetString(buffer, 0, n));
                }
            }
#endif
            string[] batch = Drain();
            if (batch != null)
            {
                for (int i = 0; i < batch.Length; i++)
                {
                    try { if (Message != null) Message(batch[i]); }
                    catch (Exception e) { Debug.LogException(e); }
                }
            }
            if (started && ReadyState == 3 && !closeNotified)
            {
                closeNotified = true;
                if (Closed != null) Closed();
            }
        }

        string[] Drain()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (inbox.Count == 0) return null;
            var batch = inbox.ToArray();
            inbox.Clear();
            return batch;
#else
            lock (gate)
            {
                if (inbox.Count == 0) return null;
                var batch = inbox.ToArray();
                inbox.Clear();
                return batch;
            }
#endif
        }

        void Enqueue(string text)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            inbox.Enqueue(text);
#else
            lock (gate) inbox.Enqueue(text);
#endif
        }

        public void Close()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (jsId != 0)
            {
                PoketatoWsClose(jsId);
                jsId = 0;
            }
#else
            if (cts != null) cts.Cancel();
            if (socket != null)
            {
                try { socket.Abort(); } catch (Exception) { }
                socket.Dispose();
                socket = null;
            }
#endif
            ReadyState = 3;
        }

#if !UNITY_WEBGL || UNITY_EDITOR
        async void ConnectEditor(string url)
        {
            cts = new CancellationTokenSource();
            socket = new ClientWebSocket();
            try
            {
                await socket.ConnectAsync(new Uri(url), cts.Token);
                ReadyState = 1;
                ReceiveEditor(cts.Token);
            }
            catch (Exception e)
            {
                ReadyState = 3;
                Debug.LogWarning("[Poketato] websocket: " + e.Message);
            }
        }

        async void ReceiveEditor(CancellationToken token)
        {
            var buf = new byte[8192];
            try
            {
                while (socket != null && socket.State == WebSocketState.Open && !token.IsCancellationRequested)
                {
                    var ms = new System.IO.MemoryStream();
                    WebSocketReceiveResult result;
                    do
                    {
                        result = await socket.ReceiveAsync(new ArraySegment<byte>(buf), token);
                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            ReadyState = 3;
                            return;
                        }
                        ms.Write(buf, 0, result.Count);
                    }
                    while (!result.EndOfMessage);
                    string text = Encoding.UTF8.GetString(ms.ToArray());
                    Enqueue(text);
                }
            }
            catch (Exception)
            {
                ReadyState = 3;
            }
        }

        async void SendEditor(string json)
        {
            if (socket == null || socket.State != WebSocketState.Open) return;
            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes(json);
                await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Poketato] websocket send: " + e.Message);
            }
        }
#endif
    }
}
