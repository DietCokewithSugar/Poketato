using System;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Poketato.Backend
{
    /// <summary>
    /// Public client settings. The anon key is allowed here; a service-role key is refused.
    /// Load order: Resources/backend.json, then window.POKETATO_BACKEND, then ?api= on the page.
    /// </summary>
    public sealed class SupabaseConfig
    {
        public string Url = "";
        public string AnonKey = "";
        public string WsUrl = "";
        public string ApiBase = "";

        public bool IsUsable =>
            !IsBlank(Url) && !IsBlank(AnonKey);

        public static SupabaseConfig Load()
        {
            var cfg = new SupabaseConfig();
            var asset = Resources.Load<TextAsset>("backend");
            if (asset != null) cfg.ApplyJson(asset.text);
#if UNITY_WEBGL && !UNITY_EDITOR
            string js = ReadJsConfig();
            if (!string.IsNullOrEmpty(js)) cfg.ApplyJson(js);
#endif
            string api = QueryValue("api");
            if (!IsBlank(api)) cfg.ApiBase = TrimSlash(api);
            cfg.Normalize();
            return cfg;
        }

        public void ApplyJson(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            JsonValue root;
            try { root = JsonValue.Parse(json); }
            catch (FormatException) { return; }
            string url = root["url"].AsString();
            string anon = root["anonKey"].AsString();
            string gameServer = root["gameServer"].AsString();
            string wsUrl = root["wsUrl"].AsString();
            string apiBase = root["apiBase"].AsString();
            if (!IsBlank(url)) Url = url.Trim();
            if (!IsBlank(anon)) AnonKey = anon.Trim();
            if (!IsBlank(wsUrl)) WsUrl = wsUrl.Trim();
            if (!IsBlank(apiBase)) ApiBase = TrimSlash(apiBase);
            if (!IsBlank(gameServer)) ApplyGameServer(gameServer.Trim());
        }

        public void Normalize()
        {
            Url = (Url ?? "").Trim().TrimEnd('/');
            AnonKey = (AnonKey ?? "").Trim();
            WsUrl = WsUrl ?? "";
            ApiBase = TrimSlash(ApiBase);
            if (IsBlank(ApiBase) && !IsBlank(WsUrl)) ApiBase = HttpBaseFromWs(WsUrl);
        }

        /// <summary>Fills Url, AnonKey and WsUrl from the game server's /api/public-config.</summary>
        public async System.Threading.Tasks.Task FetchPublicConfig()
        {
            if (IsBlank(ApiBase)) return;
            string body = await HttpText.Get(TrimSlash(ApiBase) + "/api/public-config");
            if (string.IsNullOrEmpty(body)) return;
            JsonValue root = JsonValue.Parse(body);
            string url = root["supabaseUrl"].AsString();
            string anon = root["anonKey"].AsString();
            string ws = root["wsUrl"].AsString();
            if (!IsBlank(url)) Url = url.Trim().TrimEnd('/');
            if (!IsBlank(anon))
            {
                EnsureAnonKey(anon.Trim());
                AnonKey = anon.Trim();
            }
            if (!IsBlank(ws)) WsUrl = ws.Trim();
        }

        public void EnsureSafe()
        {
            if (!IsUsable) throw new SupabaseException(0, "Supabase URL or anon key is missing");
            if (!(Url.StartsWith("https://") || Url.StartsWith("http://localhost") || Url.StartsWith("http://127.0.0.1")))
                throw new SupabaseException(0, "Supabase URL must be https");
            EnsureAnonKey(AnonKey);
        }

        public static void EnsureAnonKey(string key)
        {
            if (string.IsNullOrEmpty(key)) throw new SupabaseException(0, "missing anon key");
            if (key.StartsWith("sb_secret_")) throw new SupabaseException(0, "service role key must not be used in the client");
            if (JwtRole(key) == "service_role") throw new SupabaseException(0, "service role key must not be used in the client");
        }

        public static string JwtRole(string token)
        {
            if (string.IsNullOrEmpty(token)) return null;
            string raw = token.StartsWith("Bearer ") ? token.Substring(7) : token;
            string[] parts = raw.Split('.');
            if (parts.Length < 2) return null;
            try
            {
                var json = JsonValue.Parse(Encoding.UTF8.GetString(Base64Url(parts[1])));
                return json["role"].AsString();
            }
            catch (Exception)
            {
                return null;
            }
        }

        void ApplyGameServer(string gameServer)
        {
            if (gameServer.StartsWith("ws://") || gameServer.StartsWith("wss://"))
            {
                WsUrl = gameServer;
                ApiBase = HttpBaseFromWs(gameServer);
                return;
            }
            ApiBase = TrimSlash(gameServer);
            WsUrl = ToWs(ApiBase);
        }

        static string HttpBaseFromWs(string wsUrl)
        {
            string rest = wsUrl;
            if (rest.StartsWith("wss://")) rest = "https://" + rest.Substring(6);
            else if (rest.StartsWith("ws://")) rest = "http://" + rest.Substring(5);
            int slash = rest.IndexOf('/', 8);
            if (slash >= 0) rest = rest.Substring(0, slash);
            return TrimSlash(rest);
        }

        static string ToWs(string httpUrl)
        {
            string baseUrl = TrimSlash(httpUrl);
            if (baseUrl.StartsWith("https://")) return "wss://" + baseUrl.Substring(8) + "/ws";
            if (baseUrl.StartsWith("http://")) return "ws://" + baseUrl.Substring(7) + "/ws";
            return baseUrl + "/ws";
        }

        static string QueryValue(string key)
        {
            string abs = Application.absoluteURL;
            if (string.IsNullOrEmpty(abs)) return null;
            int q = abs.IndexOf('?');
            if (q < 0) return null;
            string[] parts = abs.Substring(q + 1).Split('&');
            for (int i = 0; i < parts.Length; i++)
            {
                string[] kv = parts[i].Split(new[] { '=' }, 2);
                if (kv.Length == 2 && Uri.UnescapeDataString(kv[0]) == key) return Uri.UnescapeDataString(kv[1]);
            }
            return null;
        }

        static bool IsBlank(string value)
        {
            if (string.IsNullOrEmpty(value)) return true;
            string trimmed = value.Trim();
            return trimmed.Length == 0 || trimmed.StartsWith("YOUR_") || trimmed.Contains("YOUR_PROJECT");
        }

        static string TrimSlash(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return value.Trim().TrimEnd('/');
        }

        static byte[] Base64Url(string value)
        {
            string padded = value.Replace('-', '+').Replace('_', '/');
            switch (padded.Length % 4)
            {
                case 2: padded += "=="; break;
                case 3: padded += "="; break;
            }
            return Convert.FromBase64String(padded);
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        static extern int PoketatoBackendConfig(byte[] buf, int len);

        static string ReadJsConfig()
        {
            var buf = new byte[4096];
            int n = PoketatoBackendConfig(buf, buf.Length);
            if (n <= 0) return null;
            return Encoding.UTF8.GetString(buf, 0, n);
        }
#endif
    }

    public static class HttpText
    {
        public static async System.Threading.Tasks.Task<string> Get(string url)
        {
            using (var req = UnityWebRequest.Get(url))
            {
                req.timeout = 15;
                var op = req.SendWebRequest();
                while (!op.isDone) await System.Threading.Tasks.Task.Yield();
                if (req.result != UnityWebRequest.Result.Success) return null;
                return req.downloadHandler.text;
            }
        }
    }
}
