using System.IO;
using UnityEditor;
using UnityEngine;
using Poketato.Backend;

namespace Poketato.Backend.EditorTools
{
    /// <summary>Writes Resources/backend.json from POKETATO_SUPABASE_URL and POKETATO_SUPABASE_ANON_KEY. Never reads a service-role key.</summary>
    public static class BackendProjectConfig
    {
        [MenuItem("Poketato/Backend/Write Config From Environment")]
        public static void WriteFromEnv()
        {
            string url = System.Environment.GetEnvironmentVariable("POKETATO_SUPABASE_URL");
            string anon = System.Environment.GetEnvironmentVariable("POKETATO_SUPABASE_ANON_KEY");
            string server = System.Environment.GetEnvironmentVariable("POKETATO_GAME_SERVER");
            if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(anon))
            {
                Debug.LogError("[Poketato] set POKETATO_SUPABASE_URL and POKETATO_SUPABASE_ANON_KEY. Do not put the service role key in the client.");
                return;
            }
            if (anon.StartsWith("sb_secret_") || SupabaseConfig.JwtRole(anon) == "service_role")
            {
                Debug.LogError("[Poketato] refusing to write a service role key into the client.");
                return;
            }
            string dir = Path.Combine(Application.dataPath, "Scripts/Backend/Resources");
            Directory.CreateDirectory(dir);
            string json = "{\n  \"url\": " + Quote(url) + ",\n  \"anonKey\": " + Quote(anon) + ",\n  \"gameServer\": " + Quote(server ?? "") + "\n}\n";
            File.WriteAllText(Path.Combine(dir, "backend.json"), json);
            AssetDatabase.Refresh();
            Debug.Log("[Poketato] wrote Assets/Scripts/Backend/Resources/backend.json");
        }

        static string Quote(string value)
        {
            return "\"" + (value ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }
    }
}
