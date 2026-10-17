using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Poketato
{
    public class UIManager : MonoBehaviour
    {
        public GameManager gm;
        public RectTransform Root { get; private set; }
        public RectTransform NumbersLayer { get; private set; }

        GameObject title, hud, levelUp, shop, result, pause, bench, perfPanel;
        Text hpText, lvText, moneyText, waveText, timerText, msgText, fieldText, perfText, benchText, titleQuality, titleTod, pauseQuality, pauseTod;
        Image hpFill, xpFill, shieldFill, eliteFill;
        GameObject eliteBar;
        Text eliteName;
        float msgT;
        string selectedLine = "charmander";
        readonly List<(RawImage img, SpeciesInfo sp, float t, int frame)> previews = new();
        readonly Dictionary<string, Image> cardFrames = new();

        class SkillSlotUI
        {
            public GameObject go;
            public Image bg, cd;
            public Text name, lv;
        }

        readonly List<SkillSlotUI> slots = new();

        public void Build()
        {
            UIKit.Font = Resources.Load<Font>("Fonts/FusionPixel12");
            UIKit.Fill = Resources.Load<Sprite>("UI/PanelFill");
            UIKit.Border = Resources.Load<Sprite>("UI/PanelBorder");

            var canvasGo = new GameObject("UI", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            Root = (RectTransform)canvasGo.transform;

            if (!FindAnyObjectByType<EventSystem>())
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<InputSystemUIInputModule>();
            }

            NumbersLayer = UIKit.Rect(Root, "Numbers", Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            BuildHud();
            BuildTitle();
            levelUp = UIKit.Stretch(Root, "LevelUp").gameObject;
            shop = UIKit.Stretch(Root, "Shop").gameObject;
            result = UIKit.Stretch(Root, "Result").gameObject;
            BuildPause();
            BuildBench();
            BuildPerf();
            HideAll();
        }

        void HideAll()
        {
            title.SetActive(false);
            hud.SetActive(false);
            levelUp.SetActive(false);
            shop.SetActive(false);
            result.SetActive(false);
            pause.SetActive(false);
            bench.SetActive(false);
        }

        static void Clear(GameObject go)
        {
            for (int i = go.transform.childCount - 1; i >= 0; i--) Destroy(go.transform.GetChild(i).gameObject);
        }

        static RectTransform Dim(GameObject screen, float a = 0.55f)
        {
            var rt = UIKit.Stretch(screen.transform, "Dim");
            var img = rt.gameObject.AddComponent<Image>();
            img.color = new Color(0.02f, 0.03f, 0.08f, a);
            return rt;
        }

        // ------------------------------------------------------------------ title

        void BuildTitle()
        {
            title = UIKit.Stretch(Root, "Title").gameObject;
            Dim(title, 0.35f);
            var head = UIKit.Box(title.transform, "Head", new Vector2(0.5f, 1f), new Vector2(0, -78), new Vector2(900, 110));
            UIKit.PanelImage(head);
            UIKit.LabelAt(head, "宝可肉鸽 · Poketato", 40, UIKit.Accent, new Vector2(0, 0.45f), Vector2.one, new Vector2(24, 0), new Vector2(-24, -6), TextAnchor.MiddleCenter);
            UIKit.LabelAt(head, "Unity 6 · URP 2.5D 可行性原型：实时光影 / 反射 / 后处理 · 选择你的伙伴", 20, Color.white, Vector2.zero, new Vector2(1, 0.45f), new Vector2(24, 8), new Vector2(-24, 0), TextAnchor.MiddleCenter);

            float x0 = -480;
            foreach (var id in GameData.LineOrder)
            {
                var L = GameData.Lines[id];
                var card = UIKit.Box(title.transform, "Card_" + id, new Vector2(0.5f, 0.5f), new Vector2(x0, 10), new Vector2(300, 330));
                x0 += 320;
                var frame = UIKit.PanelImage(card);
                cardFrames[id] = frame;
                var btn = card.gameObject.AddComponent<Button>();
                btn.targetGraphic = frame;
                btn.onClick.AddListener(() => SelectLine(id));

                var sp = SpriteDb.Get(L.dex);
                var imgRt = UIKit.Box(card, "Sprite", new Vector2(0.5f, 1f), new Vector2(0, -78), new Vector2(112, 112));
                var raw = imgRt.gameObject.AddComponent<RawImage>();
                raw.texture = sp.atlas;
                raw.raycastTarget = false;
                previews.Add((raw, sp, 0, 0));

                var types = new StringBuilder();
                foreach (var t in L.types) types.Append($"<color=#{ColorUtility.ToHtmlStringRGB(Types.ColorOf(t))}>【{Types.NameCn[(int)t]}】</color>");
                UIKit.LabelAt(card, $"{L.name}  {types}", 24, Color.white, new Vector2(0, 1), new Vector2(1, 1), new Vector2(16, -178), new Vector2(-16, -140), TextAnchor.MiddleCenter);
                UIKit.LabelAt(card, L.flavor, 18, new Color(0.85f, 0.9f, 1f), new Vector2(0, 0), new Vector2(1, 1), new Vector2(18, 60), new Vector2(-18, -184), TextAnchor.UpperLeft);
                var sk = new StringBuilder("初始技能：");
                foreach (var s in L.start) sk.Append(GameData.Skills[s].name).Append(' ');
                UIKit.LabelAt(card, sk.ToString(), 18, UIKit.Accent, new Vector2(0, 0), new Vector2(1, 0), new Vector2(18, 16), new Vector2(-18, 56), TextAnchor.MiddleLeft);
            }

            UIKit.Button(title.transform, "开始冒险（3 波）", new Vector2(0.5f, 0f), new Vector2(-150, 112), new Vector2(280, 64), () => gm.StartRun(selectedLine), 28);
            UIKit.Button(title.transform, "WebGL 性能测试", new Vector2(0.5f, 0f), new Vector2(150, 112), new Vector2(280, 64), () => gm.StartBench(), 24);

            var opts = UIKit.Box(title.transform, "Options", new Vector2(0.5f, 0f), new Vector2(0, 40), new Vector2(980, 56));
            UIKit.PanelImage(opts, new Color(0.05f, 0.08f, 0.2f, 0.85f));
            titleQuality = UIKit.LabelAt(opts, "", 20, Color.white, new Vector2(0, 0), new Vector2(0.5f, 1), new Vector2(16, 0), new Vector2(0, 0));
            titleTod = UIKit.LabelAt(opts, "", 20, Color.white, new Vector2(0.5f, 0), new Vector2(1, 1), new Vector2(16, 0), new Vector2(-16, 0));
            for (int i = 0; i < 3; i++)
            {
                int q = i;
                UIKit.Button(opts, QualityController.Names[i], new Vector2(0f, 0.5f), new Vector2(200 + i * 76, 0), new Vector2(66, 40), () => gm.SetQuality(q), 20);
                UIKit.Button(opts, EnvironmentController.Names[i], new Vector2(0.5f, 0.5f), new Vector2(200 + i * 90, 0), new Vector2(80, 40), () => gm.SetTimeOfDay(q), 20);
            }
            var hint = UIKit.Box(title.transform, "Hint", new Vector2(0.5f, 1f), new Vector2(0, -150), new Vector2(1200, 30));
            UIKit.Label(hint, "WASD / 方向键 / 手柄左摇杆 移动 · 技能自动释放 · F3 性能面板 · 1/2/3 画质 · N 昼夜 · Esc 暂停", 18, new Color(0.9f, 0.95f, 1f), TextAnchor.MiddleCenter);
            SelectLine(selectedLine);
        }

        void SelectLine(string id)
        {
            selectedLine = id;
            foreach (var kv in cardFrames) kv.Value.color = kv.Key == id ? new Color(0.2f, 0.32f, 0.68f, 0.97f) : UIKit.PanelColor;
        }

        public void ShowTitle()
        {
            HideAll();
            title.SetActive(true);
            RefreshOptions();
        }

        public void RefreshOptions()
        {
            string q = "画质：" + QualityController.Names[Mathf.Max(0, gm.Quality.Level)];
            string t = "时间：" + EnvironmentController.Names[Mathf.Max(0, gm.Environment.Preset)];
            if (titleQuality) titleQuality.text = q;
            if (titleTod) titleTod.text = t;
            if (pauseQuality) pauseQuality.text = q;
            if (pauseTod) pauseTod.text = t;
        }

        void AnimatePreviews(float dt)
        {
            for (int i = 0; i < previews.Count; i++)
            {
                var (img, sp, t, frame) = previews[i];
                var a = sp.Anim("Idle");
                t += dt * 60f;
                if (t >= a.durations[frame % a.durations.Length]) { t = 0; frame = (frame + 1) % a.frames; }
                img.uvRect = sp.FrameUV(a, frame, 0);
                float aspect = (float)a.w / a.h;
                img.rectTransform.sizeDelta = new Vector2(150 * aspect, 150) * Mathf.Min(1f, 1f);
                previews[i] = (img, sp, t, frame);
            }
        }

        // ------------------------------------------------------------------ HUD

        void BuildHud()
        {
            hud = UIKit.Stretch(Root, "Hud").gameObject;
            var stat = UIKit.Rect(hud.transform, "Status", new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -128), new Vector2(380, -16));
            UIKit.PanelImage(stat);
            lvText = UIKit.LabelAt(stat, "", 22, Color.white, new Vector2(0, 1), new Vector2(1, 1), new Vector2(16, -40), new Vector2(-16, -8));
            hpFill = UIKit.Bar(stat, "HP", new Vector2(0, 1), new Vector2(1, 1), new Vector2(16, -68), new Vector2(-16, -46), new Color(0.35f, 0.9f, 0.4f), out var hpRoot);
            shieldFill = UIKit.Bar(stat, "Shield", new Vector2(0, 1), new Vector2(1, 1), new Vector2(16, -72), new Vector2(-16, -68), new Color(0.5f, 0.75f, 1f), out _);
            hpText = UIKit.Label(hpRoot, "", 18, Color.white, TextAnchor.MiddleCenter);
            xpFill = UIKit.Bar(stat, "XP", new Vector2(0, 1), new Vector2(1, 1), new Vector2(16, -86), new Vector2(-16, -78), new Color(0.4f, 0.75f, 1f), out _);
            moneyText = UIKit.LabelAt(stat, "", 20, UIKit.Accent, new Vector2(0, 0), new Vector2(1, 0), new Vector2(16, 6), new Vector2(-16, 34));

            var waveBox = UIKit.Box(hud.transform, "Wave", new Vector2(0.5f, 1), new Vector2(0, -50), new Vector2(260, 76));
            UIKit.PanelImage(waveBox);
            waveText = UIKit.LabelAt(waveBox, "", 22, Color.white, new Vector2(0, 0.5f), Vector2.one, new Vector2(8, 0), new Vector2(-8, -4), TextAnchor.MiddleCenter);
            timerText = UIKit.LabelAt(waveBox, "", 30, UIKit.Accent, Vector2.zero, new Vector2(1, 0.55f), new Vector2(8, 4), new Vector2(-8, 0), TextAnchor.MiddleCenter);

            fieldText = UIKit.LabelAt(hud.transform, "", 20, new Color(0.7f, 0.9f, 1f), new Vector2(0, 1), new Vector2(0, 1), new Vector2(18, -170), new Vector2(520, -134));
            msgText = UIKit.LabelAt(hud.transform, "", 34, UIKit.Accent, new Vector2(0, 0.62f), new Vector2(1, 0.75f), Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter);

            eliteBar = UIKit.Box(hud.transform, "Elite", new Vector2(0.5f, 1), new Vector2(0, -112), new Vector2(420, 34)).gameObject;
            eliteFill = UIKit.Bar(eliteBar.transform, "Bar", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.9f, 0.3f, 0.3f), out var eb);
            eliteName = UIKit.Label(eb, "", 18, Color.white, TextAnchor.MiddleCenter);

            var bar = UIKit.Box(hud.transform, "Skills", new Vector2(0.5f, 0), new Vector2(0, 50), new Vector2(5 * 92, 84));
            for (int i = 0; i < 5; i++)
            {
                var s = new SkillSlotUI();
                var rt = UIKit.Box(bar, "Slot" + i, new Vector2(0, 0.5f), new Vector2(46 + i * 92, 0), new Vector2(84, 84));
                s.go = rt.gameObject;
                s.bg = UIKit.PanelImage(rt);
                var cdRt = UIKit.Stretch(rt, "Cd", 4);
                s.cd = UIKit.Solid(cdRt, new Color(0, 0, 0, 0.6f));
                s.cd.sprite = UIKit.Fill;
                s.cd.type = Image.Type.Filled;
                s.cd.fillMethod = Image.FillMethod.Radial360;
                s.cd.fillOrigin = (int)Image.Origin360.Top;
                s.name = UIKit.Label(rt, "", 18, Color.white, TextAnchor.MiddleCenter);
                s.lv = UIKit.LabelAt(rt, "", 14, UIKit.Accent, Vector2.zero, new Vector2(1, 0.35f), new Vector2(4, 2), new Vector2(-6, 0), TextAnchor.LowerRight);
                slots.Add(s);
            }
        }

        public void ShowHud()
        {
            HideAll();
            hud.SetActive(true);
        }

        public void Message(string text)
        {
            if (!msgText) return;
            msgText.text = text;
            msgT = 2f;
        }

        public void UpdateHud(GameSim sim)
        {
            var p = sim.player;
            if (p == null || !hud.activeSelf) return;
            lvText.text = $"{p.line.name}  Lv.{p.level}";
            hpFill.fillAmount = Mathf.Clamp01(p.hp / p.d.maxHp);
            shieldFill.fillAmount = Mathf.Clamp01(p.shield / p.d.maxHp);
            hpText.text = $"{Mathf.CeilToInt(Mathf.Max(0, p.hp))} / {p.d.maxHp}";
            xpFill.fillAmount = Mathf.Clamp01(p.xp / GameData.XpToNext(p.level));
            moneyText.text = $"$ {Mathf.FloorToInt(p.money)}    击败 {p.stats.kills}";
            if (sim.benchMode)
            {
                waveText.text = "性能测试";
                timerText.text = gm.BenchLabel;
            }
            else
            {
                waveText.text = $"第 {sim.wave} / {GameData.Waves.Count} 波";
                timerText.text = Mathf.CeilToInt(Mathf.Max(0, sim.waveDur - sim.waveT)).ToString();
            }
            var f = Fields.At(p.pos);
            fieldText.text = f == null ? "" : f.kind == FieldKind.Rain ? "你在：局部阵雨（水/电技能冷却 -20%）" : "你在：熔岩池（火/岩石技能 +25%，其他属性会灼伤）";
            for (int i = 0; i < slots.Count; i++)
            {
                var s = slots[i];
                bool on = i < p.skills.Count;
                s.go.SetActive(on && i < p.line.slots);
                if (!on) continue;
                var sk = p.skills[i];
                var c = Types.ColorOf(sk.def.type);
                s.bg.color = new Color(c.r * 0.55f, c.g * 0.55f, c.b * 0.55f, 0.95f);
                s.name.text = sk.def.name;
                s.lv.text = "Lv" + sk.lv;
                float cd = sk.def.cd * GameData.SkillLevelCd(sk.lv) * p.d.cdMult;
                s.cd.fillAmount = Mathf.Clamp01(sk.cd / Mathf.Max(0.1f, cd));
            }
            Enemy elite = null;
            foreach (var e in sim.enemies) if (e.def.rank != Rank.Minion && !e.showcase) { elite = e; break; }
            eliteBar.SetActive(elite != null);
            if (elite != null)
            {
                eliteFill.fillAmount = Mathf.Clamp01(elite.hp / elite.maxHp);
                eliteName.text = $"精英 {elite.def.name}  {Mathf.CeilToInt(elite.hp)}";
            }
        }

        // ------------------------------------------------------------------ level-up cards

        public void ShowLevelUp(GameSim sim)
        {
            HideAll();
            Clear(levelUp);
            levelUp.SetActive(true);
            Dim(levelUp);
            var p = sim.player;
            var head = UIKit.Box(levelUp.transform, "Head", new Vector2(0.5f, 1), new Vector2(0, -90), new Vector2(760, 80));
            UIKit.PanelImage(head);
            UIKit.Label(head, $"升级！Lv.{p.levelQueue[0]} — 选择一项强化（还有 {p.levelQueue.Count} 次）", 28, UIKit.Accent, TextAnchor.MiddleCenter);
            for (int i = 0; i < sim.cards.Count; i++)
            {
                var c = sim.cards[i];
                int idx = i;
                var card = UIKit.Box(levelUp.transform, "Card" + i, new Vector2(0.5f, 0.5f), new Vector2(-405 + i * 270, -10), new Vector2(250, 300));
                var img = UIKit.PanelImage(card);
                var b = card.gameObject.AddComponent<Button>();
                b.targetGraphic = img;
                b.onClick.AddListener(() => gm.PickCard(idx));
                if (c.stat != null)
                {
                    UIKit.LabelAt(card, c.stat.name, 26, UIKit.TierColors[c.tier], new Vector2(0, 1), Vector2.one, new Vector2(12, -80), new Vector2(-12, -20), TextAnchor.MiddleCenter);
                    UIKit.LabelAt(card, UIKit.Stars(c.tier), 22, UIKit.Accent, new Vector2(0, 1), Vector2.one, new Vector2(12, -120), new Vector2(-12, -80), TextAnchor.MiddleCenter);
                    UIKit.LabelAt(card, $"{c.stat.unit} +{c.amount}", 24, Color.white, Vector2.zero, Vector2.one, new Vector2(12, 40), new Vector2(-12, -130), TextAnchor.MiddleCenter);
                }
                else
                {
                    var s = c.skill;
                    UIKit.LabelAt(card, c.upgrade ? "强化技能" : "领悟新技能", 20, UIKit.Accent, new Vector2(0, 1), Vector2.one, new Vector2(12, -56), new Vector2(-12, -16), TextAnchor.MiddleCenter);
                    UIKit.LabelAt(card, $"<color=#{ColorUtility.ToHtmlStringRGB(Types.ColorOf(s.type))}>{s.name}</color>", 28, Color.white, new Vector2(0, 1), Vector2.one, new Vector2(12, -104), new Vector2(-12, -56), TextAnchor.MiddleCenter);
                    UIKit.LabelAt(card, $"{Types.NameCn[(int)s.type]} · 威力 {s.power} · 冷却 {s.cd}s\n{s.desc}", 18, Color.white, Vector2.zero, Vector2.one, new Vector2(16, 20), new Vector2(-16, -112), TextAnchor.UpperLeft);
                }
            }
        }

        // ------------------------------------------------------------------ shop

        public void ShowShop(GameSim sim, string note = null)
        {
            HideAll();
            Clear(shop);
            shop.SetActive(true);
            Dim(shop);
            var p = sim.player;
            var head = UIKit.Box(shop.transform, "Head", new Vector2(0.5f, 1), new Vector2(0, -64), new Vector2(1000, 76));
            UIKit.PanelImage(head);
            UIKit.LabelAt(head, $"商店 · 第 {sim.wave} 波结束", 28, UIKit.Accent, Vector2.zero, new Vector2(0.6f, 1), new Vector2(24, 0), Vector2.zero);
            UIKit.LabelAt(head, $"$ {Mathf.FloorToInt(p.money)}", 30, UIKit.Accent, new Vector2(0.6f, 0), Vector2.one, Vector2.zero, new Vector2(-24, 0), TextAnchor.MiddleRight);

            for (int i = 0; i < sim.shop.Count; i++)
            {
                var o = sim.shop[i];
                int idx = i;
                var card = UIKit.Box(shop.transform, "Offer" + i, new Vector2(0.5f, 0.5f), new Vector2(-375 + i * 250, 50), new Vector2(232, 290));
                UIKit.PanelImage(card, o.sold ? new Color(0.1f, 0.1f, 0.12f, 0.9f) : UIKit.PanelColor);
                if (o.item != null)
                {
                    UIKit.LabelAt(card, "道具 " + UIKit.Stars(o.item.tier), 18, UIKit.Accent, new Vector2(0, 1), Vector2.one, new Vector2(14, -44), new Vector2(-14, -12), TextAnchor.MiddleCenter);
                    UIKit.LabelAt(card, o.item.name, 26, UIKit.TierColors[o.item.tier], new Vector2(0, 1), Vector2.one, new Vector2(14, -90), new Vector2(-14, -44), TextAnchor.MiddleCenter);
                    UIKit.LabelAt(card, o.item.desc, 20, Color.white, Vector2.zero, Vector2.one, new Vector2(16, 80), new Vector2(-16, -96), TextAnchor.UpperCenter);
                }
                else
                {
                    var s = o.skill;
                    var owned = p.skills.Find(x => x.def == s);
                    UIKit.LabelAt(card, "招式学习器 " + UIKit.Stars(s.tier), 18, UIKit.Accent, new Vector2(0, 1), Vector2.one, new Vector2(14, -44), new Vector2(-14, -12), TextAnchor.MiddleCenter);
                    UIKit.LabelAt(card, $"<color=#{ColorUtility.ToHtmlStringRGB(Types.ColorOf(s.type))}>{s.name}</color>", 26, Color.white, new Vector2(0, 1), Vector2.one, new Vector2(14, -90), new Vector2(-14, -44), TextAnchor.MiddleCenter);
                    string tag = owned != null ? $"（已会，强化到 Lv{owned.lv + 1}）" : p.skills.Count >= p.line.slots ? "（技能栏已满，会替换最弱的技能）" : "";
                    UIKit.LabelAt(card, $"{Types.NameCn[(int)s.type]} · 威力 {s.power} · 冷却 {s.cd}s\n{s.desc}{tag}", 17, Color.white, Vector2.zero, Vector2.one, new Vector2(14, 80), new Vector2(-14, -96), TextAnchor.UpperLeft);
                }
                if (o.sold) UIKit.LabelAt(card, "已购买", 24, new Color(0.6f, 0.6f, 0.6f), Vector2.zero, new Vector2(1, 0), new Vector2(0, 14), new Vector2(0, 66), TextAnchor.MiddleCenter);
                else
                {
                    var b = UIKit.Button(card, $"$ {o.price}", new Vector2(0.5f, 0), new Vector2(0, 42), new Vector2(170, 52), () => gm.Buy(idx), 24);
                    b.interactable = p.money >= o.price;
                }
            }

            var info = UIKit.Box(shop.transform, "Info", new Vector2(0.5f, 0), new Vector2(-170, 120), new Vector2(660, 150));
            UIKit.PanelImage(info);
            var sb = new StringBuilder("技能：");
            foreach (var s in p.skills) sb.Append($"{s.def.name} Lv{s.lv}  ");
            sb.Append($"（{p.skills.Count}/{p.line.slots}）\n道具：");
            if (p.items.Count == 0) sb.Append("无");
            foreach (var kv in p.items) sb.Append($"{GameData.Items[kv.Key].name}×{kv.Value}  ");
            sb.Append($"\nHP {p.d.maxHp} · 物攻 {p.d.atk:0} · 特攻 {p.d.spa:0} · 物防 {p.d.def:0} · 特防 {p.d.spd:0} · 速度 {p.d.spe:0} · 暴击 {p.d.crit * 100:0}%");
            if (!string.IsNullOrEmpty(note)) sb.Append($"\n<color=#ffd040>{note}</color>");
            var infoText = UIKit.Label(info, sb.ToString(), 18, Color.white, TextAnchor.UpperLeft);
            infoText.rectTransform.offsetMin = new Vector2(16, 10);
            infoText.rectTransform.offsetMax = new Vector2(-16, -12);

            UIKit.Button(shop.transform, $"刷新 ${sim.RerollCost}", new Vector2(0.5f, 0), new Vector2(300, 160), new Vector2(240, 60), () => gm.Reroll(), 24);
            string next = sim.wave >= GameData.Waves.Count ? "查看结算" : $"第 {sim.wave + 1} 波 →";
            UIKit.Button(shop.transform, next, new Vector2(0.5f, 0), new Vector2(300, 84), new Vector2(240, 64), () => gm.NextWave(), 28);
        }

        // ------------------------------------------------------------------ result

        public void ShowResult(GameSim sim, bool won, float seconds)
        {
            HideAll();
            Clear(result);
            result.SetActive(true);
            Dim(result, 0.6f);
            var p = sim.player;
            var box = UIKit.Box(result.transform, "Box", new Vector2(0.5f, 0.5f), new Vector2(0, 20), new Vector2(760, 520));
            UIKit.PanelImage(box);
            UIKit.LabelAt(box, won ? "通关！" : "倒下了……", 40, won ? UIKit.Accent : new Color(1f, 0.5f, 0.5f), new Vector2(0, 1), Vector2.one, new Vector2(20, -80), new Vector2(-20, -16), TextAnchor.MiddleCenter);
            var sb = new StringBuilder();
            sb.Append($"伙伴：{p.line.name}  Lv.{p.level}\n");
            sb.Append($"完成波次：{(won ? sim.wave : Mathf.Max(0, sim.wave - 1))} / {GameData.Waves.Count}    用时：{seconds:0} 秒\n");
            sb.Append($"击败：{p.stats.kills}（精英 {p.stats.eliteKills}）    造成伤害：{p.stats.damage:0}    承受伤害：{p.stats.taken:0}\n");
            sb.Append($"暴击：{p.stats.crits}    回复：{p.stats.healed:0}    获得金钱：${p.stats.moneyEarned:0}\n\n技能伤害占比：\n");
            float total = Mathf.Max(1, p.stats.damage);
            var list = new List<KeyValuePair<string, float>>(p.stats.skillDmg);
            list.Sort((a, b) => b.Value.CompareTo(a.Value));
            foreach (var kv in list)
            {
                int bars = Mathf.RoundToInt(kv.Value / total * 30);
                sb.Append($"{kv.Key,-6} {new string('█', bars)} {kv.Value / total * 100:0}%\n");
            }
            var t = UIKit.Label(box, sb.ToString(), 20, Color.white, TextAnchor.UpperLeft);
            t.rectTransform.offsetMin = new Vector2(36, 90);
            t.rectTransform.offsetMax = new Vector2(-36, -96);
            UIKit.Button(box, "再来一局", new Vector2(0.5f, 0), new Vector2(-130, 50), new Vector2(220, 58), () => gm.StartRun(p.line.id), 26);
            UIKit.Button(box, "返回标题", new Vector2(0.5f, 0), new Vector2(130, 50), new Vector2(220, 58), () => gm.ToTitle(), 26);
        }

        // ------------------------------------------------------------------ pause / bench / perf

        void BuildPause()
        {
            pause = UIKit.Stretch(Root, "Pause").gameObject;
            Dim(pause, 0.5f);
            var box = UIKit.Box(pause.transform, "Box", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(520, 380));
            UIKit.PanelImage(box);
            UIKit.LabelAt(box, "暂停", 36, UIKit.Accent, new Vector2(0, 1), Vector2.one, new Vector2(0, -70), new Vector2(0, -14), TextAnchor.MiddleCenter);
            pauseQuality = UIKit.LabelAt(box, "", 22, Color.white, new Vector2(0, 1), Vector2.one, new Vector2(30, -120), new Vector2(-30, -84));
            pauseTod = UIKit.LabelAt(box, "", 22, Color.white, new Vector2(0, 1), Vector2.one, new Vector2(30, -186), new Vector2(-30, -150));
            for (int i = 0; i < 3; i++)
            {
                int q = i;
                UIKit.Button(box, QualityController.Names[i], new Vector2(0.5f, 1), new Vector2(-10 + i * 86, -102), new Vector2(78, 42), () => gm.SetQuality(q), 20);
                UIKit.Button(box, EnvironmentController.Names[i], new Vector2(0.5f, 1), new Vector2(-10 + i * 86, -168), new Vector2(78, 42), () => gm.SetTimeOfDay(q), 20);
            }
            UIKit.Button(box, "继续", new Vector2(0.5f, 0), new Vector2(-120, 60), new Vector2(200, 58), () => gm.TogglePause(), 26);
            UIKit.Button(box, "放弃，返回标题", new Vector2(0.5f, 0), new Vector2(120, 60), new Vector2(220, 58), () => gm.ToTitle(), 22);
        }

        public void SetPause(bool on)
        {
            pause.SetActive(on);
            RefreshOptions();
        }

        void BuildBench()
        {
            bench = UIKit.Stretch(Root, "Bench").gameObject;
            var box = UIKit.Rect(bench.transform, "Box", new Vector2(0, 0), new Vector2(0, 1), new Vector2(16, 120), new Vector2(720, -150));
            UIKit.PanelImage(box, new Color(0.04f, 0.06f, 0.15f, 0.88f));
            benchText = UIKit.Label(box, "", 17, Color.white, TextAnchor.UpperLeft);
            benchText.rectTransform.offsetMin = new Vector2(16, 12);
            benchText.rectTransform.offsetMax = new Vector2(-16, -12);
            UIKit.Button(bench.transform, "返回标题", new Vector2(1, 0), new Vector2(-130, 50), new Vector2(220, 56), () => gm.ToTitle(), 24);
        }

        public void ShowBench(string text, bool hudToo)
        {
            title.SetActive(false);
            hud.SetActive(hudToo);
            bench.SetActive(true);
            benchText.text = text;
        }

        void BuildPerf()
        {
            perfPanel = UIKit.Rect(Root, "Perf", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-330, -250), new Vector2(-12, -12)).gameObject;
            UIKit.PanelImage((RectTransform)perfPanel.transform, new Color(0.03f, 0.05f, 0.12f, 0.8f));
            perfText = UIKit.Label(perfPanel.transform, "", 16, new Color(0.8f, 1f, 0.8f), TextAnchor.UpperLeft);
            perfText.rectTransform.offsetMin = new Vector2(12, 8);
            perfText.rectTransform.offsetMax = new Vector2(-12, -10);
            perfPanel.SetActive(false);
        }

        public bool PerfVisible
        {
            get => perfPanel.activeSelf;
            set => perfPanel.SetActive(value);
        }

        public void UpdatePerf(string text)
        {
            if (perfPanel.activeSelf) perfText.text = text;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (title && title.activeSelf) AnimatePreviews(dt);
            if (msgT > 0 && msgText)
            {
                msgT -= dt;
                var c = msgText.color;
                c.a = Mathf.Clamp01(msgT * 1.5f);
                msgText.color = c;
            }
        }
    }
}
