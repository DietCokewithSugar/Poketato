using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Poketato.Backend
{
    public sealed class SupabaseException : Exception
    {
        public readonly long Status;
        public SupabaseException(long status, string message) : base(message) { Status = status; }
    }

    public sealed class SupabaseSession
    {
        public string AccessToken;
        public string RefreshToken;
        public string UserId;
        public string Email;
        public long ExpiresAtUnix;
    }

    /// <summary>
    /// Supabase Auth and PostgREST over UnityWebRequest, so the same code runs in WebGL.
    /// Calls use the anon key plus the player JWT. Row level security is what keeps inventories apart.
    /// </summary>
    public sealed class SupabaseClient
    {
        const string PrefKey = "poketato.supabase.session";

        public readonly SupabaseConfig Config;
        public SupabaseSession Session { get; private set; }

        public SupabaseClient(SupabaseConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            config.Normalize();
            config.EnsureSafe();
            Config = config;
        }

        public async Task<SupabaseSession> SignUp(string email, string password, string displayName)
        {
            var body = JsonValue.Obj()
                .Set("email", email)
                .Set("password", password)
                .Set("data", JsonValue.Obj().Set("display_name", displayName));
            JsonValue json = await SendAsync("POST", "/auth/v1/signup", body.ToJson(), false);
            return Adopt(json);
        }

        public async Task<SupabaseSession> SignIn(string email, string password)
        {
            var body = JsonValue.Obj().Set("email", email).Set("password", password);
            JsonValue json = await SendAsync("POST", "/auth/v1/token?grant_type=password", body.ToJson(), false);
            var session = Adopt(json);
            if (session == null) throw new SupabaseException(401, "login did not return a session");
            return session;
        }

        public async Task SignOut()
        {
            try
            {
                if (Session != null) await SendAsync("POST", "/auth/v1/logout", "{}", true);
            }
            catch (SupabaseException)
            {
                // The local session is cleared either way.
            }
            Session = null;
            PlayerPrefs.DeleteKey(PrefKey);
            PlayerPrefs.Save();
        }

        public void RestoreSession()
        {
            string raw = PlayerPrefs.GetString(PrefKey, "");
            if (string.IsNullOrEmpty(raw)) return;
            try { Session = ReadSession(JsonValue.Parse(raw)); }
            catch (Exception)
            {
                Session = null;
                PlayerPrefs.DeleteKey(PrefKey);
            }
        }

        public Task<JsonValue> Get(string table, string query)
        {
            string path = "/rest/v1/" + table + (string.IsNullOrEmpty(query) ? "" : "?" + query);
            return SendAsync("GET", path, null, true);
        }

        public Task<JsonValue> Rpc(string name, JsonValue args)
        {
            return SendAsync("POST", "/rest/v1/rpc/" + name, (args ?? JsonValue.Obj()).ToJson(), true);
        }

        public async Task<JsonValue> SendAsync(string method, string path, string body, bool userAuth)
        {
            await RefreshIfNeeded();
            string url = Config.Url + path;
            using (var req = new UnityWebRequest(url, method))
            {
                req.downloadHandler = new DownloadHandlerBuffer();
                req.timeout = 15;
                if (!string.IsNullOrEmpty(body))
                {
                    req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                    req.SetRequestHeader("Content-Type", "application/json");
                }
                req.SetRequestHeader("apikey", Config.AnonKey);
                req.SetRequestHeader("Accept", "application/json");
                string bearer = null;
                if (userAuth && Session != null && !string.IsNullOrEmpty(Session.AccessToken)) bearer = Session.AccessToken;
                else if (Config.AnonKey.StartsWith("eyJ")) bearer = Config.AnonKey;
                if (!string.IsNullOrEmpty(bearer)) req.SetRequestHeader("Authorization", "Bearer " + bearer);
                var op = req.SendWebRequest();
                while (!op.isDone) await Task.Yield();
                string text = req.downloadHandler != null ? req.downloadHandler.text : "";
                long code = req.responseCode;
                if (code == 0 || req.result == UnityWebRequest.Result.ConnectionError)
                    throw new SupabaseException(0, string.IsNullOrEmpty(req.error) ? "network error" : req.error);
                if (code >= 300) throw new SupabaseException(code, ExtractMessage(text));
                if (string.IsNullOrEmpty(text)) return JsonValue.Null;
                return JsonValue.Parse(text);
            }
        }

        async Task RefreshIfNeeded()
        {
            if (Session == null || string.IsNullOrEmpty(Session.RefreshToken)) return;
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (Session.ExpiresAtUnix - now > 60) return;
            var body = JsonValue.Obj().Set("refresh_token", Session.RefreshToken);
            string url = Config.Url + "/auth/v1/token?grant_type=refresh_token";
            using (var req = new UnityWebRequest(url, "POST"))
            {
                req.downloadHandler = new DownloadHandlerBuffer();
                req.timeout = 15;
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body.ToJson()));
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("apikey", Config.AnonKey);
                if (Config.AnonKey.StartsWith("eyJ")) req.SetRequestHeader("Authorization", "Bearer " + Config.AnonKey);
                var op = req.SendWebRequest();
                while (!op.isDone) await Task.Yield();
                string text = req.downloadHandler != null ? req.downloadHandler.text : "";
                if (req.responseCode >= 300 || req.responseCode == 0)
                {
                    Session = null;
                    PlayerPrefs.DeleteKey(PrefKey);
                    throw new SupabaseException(req.responseCode, ExtractMessage(text));
                }
                Adopt(JsonValue.Parse(text));
            }
        }

        SupabaseSession Adopt(JsonValue json)
        {
            if (json == null || json["access_token"].IsNull) return null;
            var session = ReadSession(json);
            Session = session;
            PlayerPrefs.SetString(PrefKey, SessionJson(session).ToJson());
            PlayerPrefs.Save();
            return session;
        }

        static SupabaseSession ReadSession(JsonValue json)
        {
            long expiresIn = json["expires_in"].AsLong();
            if (expiresIn <= 0) expiresIn = json["expiresAt"].AsLong() > 0
                ? Math.Max(0, json["expiresAt"].AsLong() - DateTimeOffset.UtcNow.ToUnixTimeSeconds())
                : 3600;
            long expiresAt = json["expiresAt"].AsLong();
            if (expiresAt <= 0) expiresAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + expiresIn;
            return new SupabaseSession
            {
                AccessToken = json["access_token"].IsNull ? json["accessToken"].AsString() : json["access_token"].AsString(),
                RefreshToken = json["refresh_token"].IsNull ? json["refreshToken"].AsString() : json["refresh_token"].AsString(),
                UserId = FirstString(json["user"]["id"], json["userId"]),
                Email = FirstString(json["user"]["email"], json["email"]),
                ExpiresAtUnix = expiresAt,
            };
        }

        static JsonValue SessionJson(SupabaseSession session)
        {
            return JsonValue.Obj()
                .Set("accessToken", session.AccessToken)
                .Set("refreshToken", session.RefreshToken)
                .Set("userId", session.UserId)
                .Set("email", session.Email)
                .Set("expiresAt", session.ExpiresAtUnix);
        }

        static string FirstString(JsonValue a, JsonValue b)
        {
            string left = a.AsString();
            return string.IsNullOrEmpty(left) ? b.AsString() : left;
        }

        static string ExtractMessage(string text)
        {
            if (string.IsNullOrEmpty(text)) return "request failed";
            try
            {
                var json = JsonValue.Parse(text);
                string msg = FirstString(json["msg"], json["message"]);
                if (string.IsNullOrEmpty(msg)) msg = json["error_description"].AsString();
                if (string.IsNullOrEmpty(msg)) msg = json["error"].AsString();
                if (!string.IsNullOrEmpty(msg)) return msg;
            }
            catch (FormatException) { }
            return text.Length > 300 ? text.Substring(0, 300) : text;
        }
    }
}
