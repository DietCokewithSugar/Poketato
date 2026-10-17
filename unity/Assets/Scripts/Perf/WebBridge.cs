using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Poketato
{
    /// <summary>Tiny bridge to the page: URL query parameters, timing marks and benchmark reports.</summary>
    public static class WebBridge
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void PoketatoReport(string json);
        [DllImport("__Internal")] static extern void PoketatoMark(string name);
        [DllImport("__Internal")] static extern string PoketatoQuery();
#else
        static void PoketatoReport(string json) { }
        static void PoketatoMark(string name) { }
        static string PoketatoQuery() => "";
#endif

        static Dictionary<string, string> query;

        public static void Report(string json)
        {
            Debug.Log("POKETATO_BENCH " + json);
            PoketatoReport(json);
        }

        public static void Mark(string name) => PoketatoMark(name);

        /// <summary>Query parameters from the page URL in WebGL, or "-key=value" command-line args elsewhere.</summary>
        public static string Param(string key, string fallback = null)
        {
            if (query == null)
            {
                query = new Dictionary<string, string>();
                string q = PoketatoQuery();
                if (!string.IsNullOrEmpty(q))
                {
                    foreach (var part in q.TrimStart('?').Split('&'))
                    {
                        if (part.Length == 0) continue;
                        int i = part.IndexOf('=');
                        string k = i < 0 ? part : part.Substring(0, i);
                        string v = i < 0 ? "1" : System.Uri.UnescapeDataString(part.Substring(i + 1));
                        query[k] = v;
                    }
                }
                foreach (var arg in System.Environment.GetCommandLineArgs())
                {
                    if (!arg.StartsWith("-poketato-")) continue;
                    var kv = arg.Substring(10).Split('=');
                    query[kv[0]] = kv.Length > 1 ? kv[1] : "1";
                }
            }
            return query.TryGetValue(key, out var r) ? r : fallback;
        }
    }
}
