using System.Threading.Tasks;
using UnityEngine;

namespace Poketato.Backend
{
    /// <summary>
    /// Boots the Supabase client. It does not open the raid socket until gameplay calls Connect.
    /// </summary>
    public sealed class BackendRunner : MonoBehaviour
    {
        static readonly TaskCompletionSource<bool> ready = new TaskCompletionSource<bool>();

        public static BackendRunner Instance { get; private set; }
        public static SupabaseConfig Config { get; private set; }
        public static SupabaseClient Client { get; private set; }
        public static PoketatoApi Api { get; private set; }
        public static GameSocket Socket { get; private set; }
        public static Task Ready => ready.Task;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Ensure()
        {
            if (FindAnyObjectByType<BackendRunner>() != null) return;
            var go = new GameObject("PoketatoBackend");
            DontDestroyOnLoad(go);
            go.AddComponent<BackendRunner>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            Socket = new GameSocket();
        }

        async void Start()
        {
            try
            {
                Config = SupabaseConfig.Load();
                if (!Config.IsUsable && !string.IsNullOrEmpty(Config.ApiBase))
                    await Config.FetchPublicConfig();
                if (Config.IsUsable)
                {
                    Client = new SupabaseClient(Config);
                    Client.RestoreSession();
                    Api = new PoketatoApi(Client);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Poketato] backend config: " + e.Message);
            }
            ready.TrySetResult(true);
        }

        void Update()
        {
            if (Socket != null) Socket.Pump();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (Socket != null) Socket.Close();
        }
    }
}
