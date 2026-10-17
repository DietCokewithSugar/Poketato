using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Poketato
{
    /// <summary>
    /// Run flow for the Emerald-styled 2D build: title, waves, level-up cards, shop, result.
    /// URL parameters: ?bench=1&secs=8  ?auto=1  ?start=pikachu  ?photo=1..3  ?ui=0  ?perf=1
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        enum State { Title, Wave, LevelUp, Shop, Result, Bench }

        public ArenaMap map;
        public ArenaView view;
        public GameScreens ui;
        public PerfStats perf;
        public PixelScreen screen;

        public GameSim Sim => sim;
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
            PixelArt.Build();
            map.Build();
            view.Init();
            view.sim = null;
            view.onMessage = ui.Message;
            view.onLevelUp = _ => { };
            ui.gm = this;
            ui.Build(Camera.main);
            screen.view = ui.View;
            screen.scaler = ui.View.canvas.GetComponent<UnityEngine.UI.CanvasScaler>();
            screen.worldCamera = Camera.main;
            autopilot = WebBridge.Param("auto") == "1";
            ui.PerfVisible = WebBridge.Param("perf") == "1";
            if (WebBridge.Param("ui") == "0") ui.SetChrome(false);

            NewSim();
            if (WebBridge.Param("bench") == "1") StartBench();
            else if (WebBridge.Param("photo") != null) StartPhoto(ParseInt(WebBridge.Param("photo"), 1));
            else if (WebBridge.Param("start") != null) StartRun(WebBridge.Param("start"));
            else ToTitle();
        }

        static int ParseInt(string s, int fallback) => int.TryParse(s, out var v) ? v : fallback;

        void NewSim()
        {
            photo = false;
            if (sim != null) sim.ClearField();
            sim = new GameSim(Random.Range(1, int.MaxValue)) { fx = view };
            view.sim = sim;
        }

        public void ToTitle()
        {
            StopAllCoroutines();
            paused = false;
            Time.timeScale = 1f;
            NewSim();
            view.ClearNumbers();
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
            view.ClearNumbers();
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
            ui.ShowShop(sim, shopNote);
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
            view.ClearNumbers();
            ui.ShowHud();
            sim.StartWave(sim.wave + 1);
            state = State.Wave;
        }

        public void TogglePause()
        {
            if (state != State.Wave && !paused) return;
            if (state != State.Wave) return;
            paused = !paused;
            Time.timeScale = paused ? 0f : 1f;
            ui.SetPause(paused);
        }

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
                Vector2 input = photo ? Vector2.zero : state == State.Bench ? BenchPilot() : state == State.Wave ? ReadMove() : Vector2.zero;
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
                PixelActor.TickAll(dt);
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
            if (k.escapeKey.wasPressedThisFrame && state == State.Wave) TogglePause();
            if (k.tKey.wasPressedThisFrame && state == State.Wave)
            {
                autopilot = !autopilot;
                ui.Message(autopilot ? "AI 辅助战斗：开" : "AI 辅助战斗：关");
            }
            var g = Gamepad.current;
            if (g != null && g.startButton.wasPressedThisFrame && state == State.Wave) TogglePause();
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
                    if (sim.NextCards()) { state = State.LevelUp; ui.ShowLevelUp(sim); }
                    else EnterShop();
                    break;
            }
        }

        string PerfText()
        {
            var sb = new StringBuilder();
            sb.Append($"FPS {perf.SmoothedFps:0.0}\n");
            sb.Append($"CPU {perf.LastCpuMs:0.0} ms\n");
            sb.Append($"模拟 {perf.LastSimMs:0.00} ms\n");
            sb.Append($"Draw {Fmt(perf.DrawCalls)}\n");
            sb.Append($"敌人 {sim.enemies.Count}  精灵 {PixelActor.Active.Count}\n");
            sb.Append($"480x320 x{Mathf.Max(1, Mathf.Min(Screen.width / 480, Screen.height / 320))}");
            return sb.ToString();
        }

        static string Fmt(long v) => v < 0 ? "n/a" : v.ToString();

        public void StartPhoto(int variant)
        {
            StopAllCoroutines();
            NewSim();
            photo = true;
            string line = WebBridge.Param("start") ?? "charmander";
            if (!GameData.Lines.ContainsKey(line)) line = "charmander";
            SetupShowcase(line);
            Vector2[] spots = { new(196, 336), new(384, 288), new(590, 196) };
            sim.player.pos = spots[Mathf.Clamp(variant, 0, spots.Length - 1)];
            view.SpawnPlayer();
            SpawnRoster();
            state = State.Bench;
            BenchLabel = "";
            ui.ShowHud();
        }

        void SetupShowcase(string line)
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
            sim.benchTarget = 0;
            sim.StartWave(4);
        }

        void SpawnRoster()
        {
            var others = new List<string> { "squirtle", "bulbasaur", "pikachu", "charmander", "caterpie", "rattata", "pidgey", "zubat", "oddish", "beedrill" };
            others.Remove(sim.player.line.id);
            for (int i = 0; i < others.Count; i++)
            {
                float a = Mathf.PI * 2 * i / others.Count;
                sim.SpawnEnemy(others[i], sim.player.pos + new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.7f) * (50 + (i % 3) * 16), true);
            }
        }

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
            var scenarios = new List<(string id, string label, int count)>
            {
                ("roster10", "10 种同屏", 9),
                ("combat60", "60 只敌人", 60),
                ("horde150", "150 只敌人", 150),
            };
            var log = new StringBuilder("像素风性能测试\n");
            ui.ShowBench(log.ToString(), false);
            foreach (var sc in scenarios)
            {
                NewSim();
                view.ClearNumbers();
                SetupShowcase("charmander");
                sim.showcaseOnly = sc.id == "roster10";
                sim.benchTarget = sc.count;
                view.SpawnPlayer();
                if (sim.showcaseOnly) SpawnRoster();
                state = State.Bench;
                BenchLabel = sc.label;
                ui.ShowBench(log + "\n> " + sc.label, true);
                yield return new WaitForSecondsRealtime(2f);
                perf.ResetWindow();
                yield return new WaitForSecondsRealtime(secs);
                var r = perf.Summarize(sc.id);
                r.quality = "gba";
                r.timeOfDay = "day";
                r.enemies = sim.enemies.Count;
                r.actors = PixelActor.Active.Count;
                r.projectiles = sim.projs.Count;
                r.transients = view.FxCount;
                WebBridge.Report(JsonUtility.ToJson(r));
                log.Append($"\n{sc.label}  {r.avgFps:0.0} FPS  CPU {r.cpuMs:0.0} ms");
                ui.ShowBench(log.ToString(), false);
            }
            WebBridge.Mark("benchDone");
            UnityEngine.Debug.Log("POKETATO_BENCH_DONE");
        }
    }
}
