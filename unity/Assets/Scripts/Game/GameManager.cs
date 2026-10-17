using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Poketato
{
    /// <summary>
    /// Owns the run: title -> waves -> level-up cards -> shop -> ... -> result, plus the WebGL benchmark.
    /// URL parameters (WebGL): ?bench=1&secs=8&tiers=0,1,2  ?q=0..2  ?tod=0..2  ?auto=1 (autopilot)  ?start=pikachu
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        enum State { Title, Wave, LevelUp, Shop, Result, Bench }

        public GameView view;
        public UIManager ui;
        public PerfStats perf;
        public QualityController quality;
        public EnvironmentController environment;
        public DamageNumbers numbers;

        public QualityController Quality => quality;
        public EnvironmentController Environment => environment;
        public string BenchLabel { get; private set; } = "";

        GameSim sim;
        State state;
        bool paused, autopilot, photo;
        float runTime;
        string shopNote;
        readonly Stopwatch watch = new();
        float perfTextT;
        bool firstFrame;

        void Start()
        {
            Application.targetFrameRate = -1;
            ui.gm = this;
            ui.Build();
            numbers.cam = Camera.main;
            numbers.Init(ui.NumbersLayer, UIKit.Font);
            view.numbers = numbers;
            view.Init();
            view.onMessage = ui.Message;
            quality.Apply(ParseInt(WebBridge.Param("q"), QualitySettings.GetQualityLevel()));
            environment.Apply(ParseInt(WebBridge.Param("tod"), 1));
            autopilot = WebBridge.Param("auto") == "1";
            ui.PerfVisible = WebBridge.Param("perf") == "1";

            NewSim();
            if (WebBridge.Param("ui") == "0") ui.Root.gameObject.SetActive(false);
            if (WebBridge.Param("debugrefl") == "1") ui.StartCoroutine(ShowReflectionTexture());
            if (WebBridge.Param("bench") == "1") StartBench();
            else if (WebBridge.Param("photo") != null) StartPhoto(ParseInt(WebBridge.Param("photo"), 1));
            else if (WebBridge.Param("start") != null) StartRun(WebBridge.Param("start"));
            else ToTitle();
        }

        IEnumerator ShowReflectionTexture()
        {
            yield return null;
            yield return null;
            var rt = UIKit.Box(ui.Root, "ReflectionDebug", new Vector2(0, 0), new Vector2(180, 120), new Vector2(320, 180));
            var img = rt.gameObject.AddComponent<UnityEngine.UI.RawImage>();
            img.texture = Shader.GetGlobalTexture("_PlanarReflectionTex");
        }

        static int ParseInt(string s, int fallback) => int.TryParse(s, out var v) ? v : fallback;

        void NewSim()
        {
            photo = false;
            if (sim != null) sim.ClearField();
            sim = new GameSim(Random.Range(1, int.MaxValue)) { fx = view };
            view.sim = sim;
        }

        // ------------------------------------------------------------------ flow (called by UI)

        public void ToTitle()
        {
            StopAllCoroutines();
            paused = false;
            Time.timeScale = 1f;
            NewSim();
            numbers.ClearAll();
            // Idle showcase behind the title: the four partners wander around the plaza.
            sim.StartRun("charmander");
            view.SpawnPlayer();
            sim.benchMode = true;
            sim.showcaseOnly = true;
            sim.godMode = true;
            sim.StartWave(1);
            foreach (var id in new[] { "squirtle", "bulbasaur", "pikachu", "caterpie", "pidgey", "rattata" })
                sim.SpawnEnemy(id, sim.player.pos + Random.insideUnitCircle * 90f, true);
            state = State.Title;
            ui.ShowTitle();
        }

        public void StartRun(string lineId)
        {
            StopAllCoroutines();
            paused = false;
            Time.timeScale = 1f;
            NewSim();
            numbers.ClearAll();
            if (!GameData.Lines.ContainsKey(lineId)) lineId = "charmander";
            sim.StartRun(lineId);
            view.SpawnPlayer();
            runTime = 0;
            ui.ShowHud();
            sim.StartWave(1);
            state = State.Wave;
        }

        public void PickCard(int i)
        {
            if (state != State.LevelUp) return;
            sim.PickCard(i);
            if (sim.cards != null) ui.ShowLevelUp(sim);
            else EnterShop();
        }

        void EnterShop()
        {
            if (sim.wave >= GameData.Waves.Count && sim.phase == SimPhase.Won)
            {
                state = State.Result;
                ui.ShowResult(sim, true, runTime);
                return;
            }
            sim.shopRerolls = 0;
            sim.RollShop();
            shopNote = null;
            state = State.Shop;
            ui.ShowShop(sim);
        }

        public void Buy(int i)
        {
            if (state != State.Shop) return;
            shopNote = sim.Buy(i);
            ui.ShowShop(sim, shopNote);
        }

        public void Reroll()
        {
            if (state != State.Shop) return;
            shopNote = sim.Reroll();
            ui.ShowShop(sim, shopNote);
        }

        public void NextWave()
        {
            if (state != State.Shop) return;
            numbers.ClearAll();
            ui.ShowHud();
            sim.StartWave(sim.wave + 1);
            state = State.Wave;
        }

        public void SetQuality(int q)
        {
            quality.Apply(q);
            ui.RefreshOptions();
            ui.Message("画质：" + QualityController.Names[q]);
        }

        public void SetTimeOfDay(int t)
        {
            environment.Apply(t);
            ui.RefreshOptions();
        }

        public void TogglePause()
        {
            if (state != State.Wave) return;
            paused = !paused;
            Time.timeScale = paused ? 0f : 1f;
            ui.SetPause(paused);
        }

        // ------------------------------------------------------------------ frame

        Vector2 ReadMove()
        {
            Vector2 v = Vector2.zero;
            var k = Keyboard.current;
            if (k != null)
            {
                if (k.aKey.isPressed || k.leftArrowKey.isPressed) v.x -= 1;
                if (k.dKey.isPressed || k.rightArrowKey.isPressed) v.x += 1;
                if (k.wKey.isPressed || k.upArrowKey.isPressed) v.y -= 1;
                if (k.sKey.isPressed || k.downArrowKey.isPressed) v.y += 1;
            }
            var g = Gamepad.current;
            if (g != null)
            {
                var s = g.leftStick.ReadValue();
                if (s.sqrMagnitude > 0.04f) v += new Vector2(s.x, -s.y);
            }
            return v;
        }

        /// <summary>Simple potential-field pilot (dodge nearby enemies, grab candy, orbit the arena centre).</summary>
        Vector2 Autopilot()
        {
            var p = sim.player;
            Vector2 force = Vector2.zero;
            foreach (var e in sim.enemies)
            {
                if (e.showcase) continue;
                Vector2 d = p.pos - e.pos;
                float m = d.sqrMagnitude;
                if (m < 110 * 110 && m > 1) force += d / m * 2600f;
            }
            Pickup best = null;
            float bd = 160 * 160;
            foreach (var pk in sim.pickups)
            {
                float m = (pk.pos - p.pos).sqrMagnitude;
                if (m < bd) { bd = m; best = pk; }
            }
            if (best != null) force += (best.pos - p.pos).normalized * 0.8f;
            Vector2 c = new Vector2(GameData.ArenaW / 2, GameData.ArenaH / 2) - p.pos;
            force += c * 0.004f + new Vector2(-c.y, c.x).normalized * 0.45f;
            return force.sqrMagnitude > 1 ? force.normalized : force;
        }

        Vector2 BenchPilot()
        {
            var p = sim.player;
            Vector2 c = new Vector2(GameData.ArenaW / 2, GameData.ArenaH / 2);
            float a = Time.time * 0.55f;
            Vector2 goal = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.7f) * 120f;
            Vector2 d = goal - p.pos;
            return d.magnitude > 6 ? d.normalized : Vector2.zero;
        }

        void Update()
        {
            if (!firstFrame)
            {
                firstFrame = true;
                WebBridge.Mark("firstFrame");
            }
            HandleHotkeys();
            float dt = Mathf.Min(Time.deltaTime, 1f / 20f);
            if (!paused && sim != null)
            {
                Vector2 input = photo ? Vector2.zero : state == State.Bench ? BenchPilot() : state == State.Title ? Vector2.zero : ReadMove();
                if (autopilot && state == State.Wave && input.sqrMagnitude < 0.01f) input = Autopilot();
                watch.Restart();
                sim.Step(dt, input);
                watch.Stop();
                perf.RecordSim((float)watch.Elapsed.TotalMilliseconds);
                if (state == State.Wave) runTime += dt;
                CheckPhase();
            }
            if (sim != null)
            {
                view.Sync(dt);
                SpriteActor.TickAll(dt);
                ui.UpdateHud(sim);
            }
            perfTextT -= Time.unscaledDeltaTime;
            if (perfTextT <= 0 && ui.PerfVisible)
            {
                perfTextT = 0.25f;
                ui.UpdatePerf(PerfText());
            }
        }

        void HandleHotkeys()
        {
            var k = Keyboard.current;
            if (k == null) return;
            if (k.f3Key.wasPressedThisFrame || k.backquoteKey.wasPressedThisFrame) ui.PerfVisible = !ui.PerfVisible;
            if (k.digit1Key.wasPressedThisFrame) SetQuality(0);
            if (k.digit2Key.wasPressedThisFrame) SetQuality(1);
            if (k.digit3Key.wasPressedThisFrame) SetQuality(2);
            if (k.nKey.wasPressedThisFrame) SetTimeOfDay((environment.Preset + 1) % 3);
            if (k.escapeKey.wasPressedThisFrame) TogglePause();
            if (k.tKey.wasPressedThisFrame && state == State.Wave)
            {
                autopilot = !autopilot;
                ui.Message(autopilot ? "AI 辅助战斗：开" : "AI 辅助战斗：关");
            }
            var g = Gamepad.current;
            if (g != null && g.startButton.wasPressedThisFrame) TogglePause();
        }

        void CheckPhase()
        {
            if (state != State.Wave) return;
            switch (sim.phase)
            {
                case SimPhase.Over:
                    state = State.Result;
                    ui.ShowResult(sim, false, runTime);
                    break;
                case SimPhase.Won:
                case SimPhase.Inter:
                    if (sim.NextCards())
                    {
                        state = State.LevelUp;
                        ui.ShowLevelUp(sim);
                    }
                    else EnterShop();
                    break;
            }
        }

        string PerfText()
        {
            var sb = new StringBuilder();
            sb.Append($"FPS {perf.SmoothedFps:0.0}  ({1000f / Mathf.Max(1, perf.SmoothedFps):0.0} ms)\n");
            sb.Append($"主线程 CPU {perf.LastCpuMs:0.0} ms · 模拟 {perf.LastSimMs:0.00} ms\n");
            sb.Append($"Draw {Fmt(perf.DrawCalls)} · Batch {Fmt(perf.Batches)} · SetPass {Fmt(perf.SetPass)}\n");
            sb.Append($"三角形 {Fmt(perf.Triangles)}\n");
            sb.Append($"敌人 {sim.enemies.Count} · 精灵 {SpriteActor.Active.Count} · 弹体 {sim.projs.Count}\n");
            sb.Append($"动态光源 {view.lights.ActiveCount}/{view.lights.Capacity} · 特效 {view.TransientCount}\n");
            sb.Append($"画质 {QualityController.Names[Mathf.Max(0, quality.Level)]} · {EnvironmentController.Names[Mathf.Max(0, environment.Preset)]}\n");
            sb.Append($"{Screen.width}x{Screen.height} · 堆 {PerfStats.HeapMB} MB");
            return sb.ToString();
        }

        static string Fmt(long v) => v < 0 ? "n/a" : v.ToString();

        // ------------------------------------------------------------------ photo mode

        /// <summary>All ten species around the player at a chosen spot, skills firing, nothing dies (for screenshots).</summary>
        public void StartPhoto(int variant)
        {
            StopAllCoroutines();
            NewSim();
            photo = true;
            string line = WebBridge.Param("start") ?? "charmander";
            if (!GameData.Lines.ContainsKey(line)) line = "charmander";
            SetupShowcase(line, 9);
            Vector2[] spots = { new(384, 288), new(292, 334), new(384, 288), new(548, 236) };
            sim.player.pos = spots[Mathf.Clamp(variant, 0, spots.Length - 1)];
            view.SpawnPlayer();
            SpawnRoster();
            state = State.Bench;
            BenchLabel = "拍照模式";
            ui.ShowHud();
        }

        void SetupShowcase(string line, int target)
        {
            sim.StartRun(line);
            sim.player.skills.Clear();
            string[] build = line switch
            {
                "squirtle" => new[] { "water_gun", "bubble_beam", "water_pulse", "aqua_tail", "swift" },
                "bulbasaur" => new[] { "vine_whip", "razor_leaf", "seed_bomb", "poison_powder", "swift" },
                "pikachu" => new[] { "thunder_shock", "electro_ball", "thunderbolt", "discharge", "spark" },
                _ => new[] { "ember", "flamethrower", "fire_spin", "dragon_breath", "swift" },
            };
            foreach (var id in build) sim.player.skills.Add(new SkillSlot { def = GameData.Skills[id], lv = 2 });
            sim.player.levelMods.haste = 30;
            sim.Recompute();
            sim.benchMode = true;
            sim.godMode = true;
            sim.showcaseOnly = true;
            sim.benchTarget = target;
            sim.StartWave(4);
        }

        void SpawnRoster()
        {
            var others = new List<string> { "squirtle", "bulbasaur", "pikachu", "charmander", "caterpie", "rattata", "pidgey", "zubat", "oddish", "beedrill" };
            others.Remove(sim.player.line.id);
            for (int i = 0; i < others.Count; i++)
            {
                float a = Mathf.PI * 2 * i / others.Count;
                sim.SpawnEnemy(others[i], sim.player.pos + new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.7f) * (60 + (i % 3) * 18), true);
            }
        }

        // ------------------------------------------------------------------ benchmark

        public void StartBench()
        {
            StopAllCoroutines();
            StartCoroutine(Bench());
        }

        IEnumerator Bench()
        {
            paused = false;
            Time.timeScale = 1f;
            float secs = Mathf.Max(2f, ParseInt(WebBridge.Param("secs"), 8));
            var tiers = new List<int>();
            foreach (var s in (WebBridge.Param("tiers") ?? "0,1,2").Split(','))
                if (int.TryParse(s, out var t)) tiers.Add(Mathf.Clamp(t, 0, 2));
            var scenarios = new List<(string id, string label, int count)>
            {
                ("roster10", "10 种宝可梦同屏（全部特效）", 9),
                ("combat60", "战斗：60 只敌人", 60),
                ("horde150", "战斗：150 只敌人（原版单人上限 100）", 150),
            };
            var results = new List<PerfSummary>();
            var log = new StringBuilder("<color=#ffd040>WebGL 性能测试</color>  每项预热 2 秒，采样 " + secs + " 秒\n");
            int tod = environment.Preset;
            ui.ShowBench(log.ToString(), true);
            WebBridge.Mark("benchStart");
            foreach (int tier in tiers)
            {
                quality.Apply(tier);
                ui.RefreshOptions();
                foreach (var sc in scenarios)
                {
                    NewSim();
                    numbers.ClearAll();
                    // A mid-run build so skill VFX density matches wave 10+ of the web game.
                    SetupShowcase("charmander", sc.count);
                    sim.showcaseOnly = sc.id == "roster10";
                    view.SpawnPlayer();
                    if (sim.showcaseOnly) SpawnRoster();
                    state = State.Bench;
                    BenchLabel = $"{QualityController.Names[tier]} · {sc.label}";
                    ui.ShowBench(log + $"\n▶ 正在测试：{BenchLabel}", true);
                    yield return new WaitForSecondsRealtime(2f);
                    perf.ResetWindow();
                    yield return new WaitForSecondsRealtime(secs);
                    var r = perf.Summarize(sc.id);
                    r.quality = QualityController.Ids[tier];
                    r.timeOfDay = EnvironmentController.Ids[Mathf.Max(0, tod)];
                    r.enemies = sim.enemies.Count;
                    r.actors = SpriteActor.Active.Count;
                    r.projectiles = sim.projs.Count;
                    r.lights = view.lights.ActiveCount;
                    r.transients = view.TransientCount;
                    results.Add(r);
                    WebBridge.Report(JsonUtility.ToJson(r));
                    log.Append($"\n{QualityController.Names[tier]} · {sc.label}\n  {r.avgFps:0.0} FPS · 平均 {r.avgMs:0.0} ms · p95 {r.p95Ms:0.0} · p99 {r.p99Ms:0.0} · 最大 {r.maxMs:0}\n  卡顿(>50ms) {r.hitches50} 帧 · CPU {r.cpuMs:0.0} ms · 模拟 {r.simMs:0.00} ms · 精灵 {r.actors} · Draw {Fmt(r.drawCalls)}");
                    ui.ShowBench(log.ToString(), true);
                }
            }
            WebBridge.Mark("benchDone");
            UnityEngine.Debug.Log("POKETATO_BENCH_DONE");
            log.Append("\n\n<color=#ffd040>完成</color>（结果也输出到浏览器控制台 POKETATO_BENCH）");
            ui.ShowBench(log.ToString(), false);
        }
    }
}
