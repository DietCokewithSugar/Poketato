using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Poketato
{
    /// <summary>Title, HUD, level-up, shop and result screens, all in the 480x320 Emerald window style.</summary>
    public class GameScreens : MonoBehaviour
    {
        public GameManager gm;
        public RawImage View;
        public bool PerfVisible { get; set; }

        RectTransform root, frame, chrome, perfPanel;
        CanvasScaler scaler;
        GameObject title, hud, levelUp, shop, result, pause, bench;
        PixelMenu menu;
        Text moneyText, waveText, timerText, msgText, fieldText, perfText, flavorText, eliteText;
        Image hpFill, xpFill, msgWindow, preview;
        SpeciesInfo previewSpecies;
        AnimInfo previewAnim;
        int previewFrame;
        float previewT, msgT;
        readonly List<Text> skillTexts = new();
        readonly List<Image> skillBgs = new();
        string shownLine;

        public void Build(Camera worldCamera)
        {
            PixelArt.Build();
            PixelUI.Font = Resources.Load<Font>("Fonts/FusionPixel12");

            root = PixelUI.CreateCanvas(worldCamera, 100);
            scaler = root.GetComponent<CanvasScaler>();
            frame = PixelUI.Box(root, 0, 0, PixelUI.Width, PixelUI.Height, "Frame");
            View = PixelUI.Box(frame, 0, 0, PixelUI.Width, PixelUI.Height, "View").gameObject.AddComponent<RawImage>();
            View.raycastTarget = false;

            chrome = PixelUI.Box(frame, 0, 0, PixelUI.Width, PixelUI.Height, "Chrome");
            if (!FindAnyObjectByType<EventSystem>())
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<InputSystemUIInputModule>();
            }
            BuildHud();
            BuildTitle();
            levelUp = PixelUI.Box(chrome, 0, 0, PixelUI.Width, PixelUI.Height, "LevelUp").gameObject;
            shop = PixelUI.Box(chrome, 0, 0, PixelUI.Width, PixelUI.Height, "Shop").gameObject;
            result = PixelUI.Box(chrome, 0, 0, PixelUI.Width, PixelUI.Height, "Result").gameObject;
            BuildPause();
            BuildBench();
            BuildPerf();
            HideAll();
        }

        public void ApplyPixelScale()
        {
            int s = Mathf.Max(1, Mathf.Min(Screen.width / PixelUI.Width, Screen.height / PixelUI.Height));
            if (scaler.scaleFactor != s) scaler.scaleFactor = s;
            float cw = Screen.width / scaler.scaleFactor;
            float ch = Screen.height / scaler.scaleFactor;
            frame.anchoredPosition = new Vector2((cw - PixelUI.Width) / 2f, -((ch - PixelUI.Height) / 2f));
            PixelUI.PointFilterFont();
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
            menu = null;
        }

        static void Clear(GameObject go)
        {
            for (int i = go.transform.childCount - 1; i >= 0; i--) Destroy(go.transform.GetChild(i).gameObject);
        }

        void Dim(Transform parent)
        {
            PixelUI.Solid(parent, 0, 0, PixelUI.Width, PixelUI.Height, new Color(0.05f, 0.08f, 0.05f, 0.45f));
        }

        // ------------------------------------------------------------------ title

        void BuildTitle()
        {
            var rt = PixelUI.Box(chrome, 0, 0, PixelUI.Width, PixelUI.Height, "Title");
            title = rt.gameObject;
            PixelUI.Window(rt, 96, 8, 288, 40);
            PixelUI.Label(rt, 96, 14, 288, 16, "宝可肉鸽", PixelUI.Text, TextAnchor.UpperCenter);
            PixelUI.Label(rt, 96, 28, 288, 16, "GBA 绿宝石像素风", new Color(0.35f, 0.45f, 0.7f), TextAnchor.UpperCenter);

            PixelUI.Window(rt, 16, 56, 168, 200);
            preview = PixelUI.Box(rt, 48, 68, 104, 88, "Preview").gameObject.AddComponent<Image>();
            preview.preserveAspect = true;
            preview.raycastTarget = false;
            flavorText = PixelUI.Label(rt, 26, 162, 148, 84, "", PixelUI.Text, TextAnchor.UpperLeft);

            PixelUI.Window(rt, 196, 56, 268, 200);
            PixelUI.Label(rt, 206, 230, 248, 16, "方向键选择    Z / 回车确定", new Color(0.3f, 0.35f, 0.45f), TextAnchor.UpperCenter);
            menu = new PixelMenu(rt, 212, 68, 236, 22);
            foreach (var id in GameData.LineOrder)
            {
                var line = GameData.Lines[id];
                var captured = id;
                menu.Add(line.name, () => gm.StartRun(captured));
            }
            menu.Add("性能测试", () => gm.StartBench());
            menu.onHighlight = i => ShowPreview(i < GameData.LineOrder.Count ? GameData.LineOrder[i] : shownLine);
            ShowPreview(GameData.LineOrder[0]);
            titleMenu = menu;
        }

        PixelMenu titleMenu;

        public void ShowTitle()
        {
            HideAll();
            title.SetActive(true);
            menu = titleMenu;
            menu.Highlight(menu.index);
        }

        void ShowPreview(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            shownLine = id;
            var line = GameData.Lines[id];
            previewSpecies = SpriteDb.Get(line.dex);
            previewAnim = previewSpecies.Anim("Idle");
            previewFrame = 0;
            previewT = 0;
            preview.sprite = previewSpecies.Frame(previewAnim, 0, 0);
            var types = new StringBuilder();
            foreach (var t in line.types)
                types.Append($"<color=#{ColorUtility.ToHtmlStringRGB(Types.ColorOf(t))}>{Types.NameCn[(int)t]}</color> ");
            flavorText.text = $"{types}\n{line.flavor}";
        }

        // ------------------------------------------------------------------ HUD

        void BuildHud()
        {
            var rt = PixelUI.Box(chrome, 0, 0, PixelUI.Width, PixelUI.Height, "Hud");
            hud = rt.gameObject;
            PixelUI.Window(rt, 8, 8, 176, 52, "info");
            PixelUI.Label(rt, 16, 12, 160, 14, "", PixelUI.Text, TextAnchor.UpperLeft).name = "Name";
            hpFill = PixelUI.HpBar(rt, 16, 28, 120);
            xpFill = PixelUI.Bar(rt, 16, 40, 160, 3, PixelUI.ExpBlue);
            moneyText = PixelUI.Label(rt, 16, 44, 160, 14, "", PixelUI.Text);

            PixelUI.Window(rt, 196, 8, 88, 36);
            waveText = PixelUI.Label(rt, 196, 12, 88, 14, "", PixelUI.Text, TextAnchor.UpperCenter);
            timerText = PixelUI.Label(rt, 196, 26, 88, 14, "", new Color(0.75f, 0.35f, 0.15f), TextAnchor.UpperCenter);

            fieldText = PixelUI.Label(rt, 8, 64, 300, 14, "", new Color(0.2f, 0.35f, 0.7f));
            eliteText = PixelUI.Label(rt, 300, 12, 172, 28, "", PixelUI.Text, TextAnchor.UpperRight);

            PixelUI.Window(rt, 8, 286, 464, 28);
            msgWindow = PixelUI.Window(rt, 48, 248, 384, 32);
            msgText = PixelUI.Label(msgWindow.transform, 12, 8, 360, 18, "", PixelUI.Text, TextAnchor.UpperLeft);
            msgWindow.gameObject.SetActive(false);
        }

        Text NameText => hud.transform.Find("Name").GetComponent<Text>();

        public void ShowHud()
        {
            HideAll();
            hud.SetActive(true);
            RebuildSkills();
        }

        void RebuildSkills()
        {
            foreach (var t in skillTexts) if (t) Destroy(t.gameObject);
            foreach (var b in skillBgs) if (b) Destroy(b.gameObject);
            skillTexts.Clear();
            skillBgs.Clear();
            var p = gm.Sim?.player;
            if (p == null) return;
            for (int i = 0; i < p.skills.Count; i++)
            {
                var bg = PixelUI.Solid(hud.transform, 14 + i * 90, 290, 86, 18, new Color(1, 1, 1, 0.65f));
                bg.type = Image.Type.Filled;
                bg.fillMethod = Image.FillMethod.Horizontal;
                skillBgs.Add(bg);
                skillTexts.Add(PixelUI.Label(hud.transform, 14 + i * 90, 292, 86, 16, "", PixelUI.Text, TextAnchor.UpperCenter, shadow: false));
            }
        }

        public void Message(string text)
        {
            if (!msgText) return;
            msgText.text = text;
            msgWindow.gameObject.SetActive(true);
            msgT = 2.2f;
        }

        public void UpdateHud(GameSim sim)
        {
            var p = sim.player;
            if (p == null || !hud.activeSelf) return;
            if (skillTexts.Count != p.skills.Count) RebuildSkills();
            NameText.text = $"{p.line.name}  Lv{p.level}";
            PixelUI.SetHp(hpFill, p.hp / p.d.maxHp);
            xpFill.fillAmount = Mathf.Clamp01(p.xp / GameData.XpToNext(p.level));
            moneyText.text = $"${Mathf.FloorToInt(p.money)}   击败 {p.stats.kills}";
            if (sim.benchMode)
            {
                waveText.text = "测试";
                timerText.text = gm.BenchLabel;
            }
            else
            {
                waveText.text = $"第 {sim.wave}/{GameData.Waves.Count} 波";
                timerText.text = Mathf.CeilToInt(Mathf.Max(0, sim.waveDur - sim.waveT)).ToString();
            }
            var f = Fields.At(p.pos);
            fieldText.text = f == null ? "" : f.kind == FieldKind.Rain ? "阵雨中：水/电冷却缩短" : "熔岩中：火/岩石更强，其他属性灼伤";
            Enemy elite = null;
            foreach (var e in sim.enemies)
                if (e.def.rank != Rank.Minion && !e.showcase) { elite = e; break; }
            eliteText.text = elite == null ? "" : $"{elite.def.name}\n{Mathf.CeilToInt(elite.hp)}/{Mathf.CeilToInt(elite.maxHp)}";
            for (int i = 0; i < skillTexts.Count; i++)
            {
                var sk = p.skills[i];
                float cd = sk.def.cd * GameData.SkillLevelCd(sk.lv) * p.d.cdMult;
                bool ready = sk.cd <= 0;
                skillTexts[i].text = sk.def.name;
                skillTexts[i].color = ready ? Types.ColorOf(sk.def.type) : new Color(0.55f, 0.55f, 0.55f);
                skillBgs[i].fillAmount = ready ? 1f : 1f - Mathf.Clamp01(sk.cd / Mathf.Max(0.1f, cd));
            }
        }

        // ------------------------------------------------------------------ level-up / shop / result

        public void ShowLevelUp(GameSim sim)
        {
            HideAll();
            Clear(levelUp);
            levelUp.SetActive(true);
            var rt = levelUp.GetComponent<RectTransform>();
            Dim(rt);
            PixelUI.Window(rt, 70, 36, 340, 248);
            PixelUI.Label(rt, 82, 48, 316, 18, $"升到了 Lv{sim.player.levelQueue[0]}！要提升哪一项？", PixelUI.Text, TextAnchor.UpperCenter);
            menu = new PixelMenu(rt, 90, 78, 300, 36);
            for (int i = 0; i < sim.cards.Count; i++)
            {
                int idx = i;
                var c = sim.cards[i];
                string label, right;
                if (c.stat != null)
                {
                    label = $"{c.stat.name}  {new string('★', c.tier)}";
                    right = $"+{c.amount:0}";
                }
                else
                {
                    label = (c.upgrade ? "强化 " : "领悟 ") + c.skill.name;
                    right = Types.NameCn[(int)c.skill.type];
                }
                menu.Add(label, () => gm.PickCard(idx), right);
            }
            PixelUI.Label(rt, 82, 246, 316, 28, "方向键选择，Z / 回车确定", new Color(0.35f, 0.4f, 0.5f), TextAnchor.UpperCenter);
        }

        public void ShowShop(GameSim sim, string note)
        {
            HideAll();
            Clear(shop);
            shop.SetActive(true);
            var rt = shop.GetComponent<RectTransform>();
            Dim(rt);
            PixelUI.Window(rt, 36, 16, 408, 288);
            PixelUI.Label(rt, 48, 26, 250, 16, $"商店  ·  第 {sim.wave} 波结束", PixelUI.Text);
            PixelUI.Label(rt, 250, 26, 180, 16, $"${Mathf.FloorToInt(sim.player.money)}", PixelUI.Text, TextAnchor.UpperRight);
            menu = new PixelMenu(rt, 52, 52, 376, 28);
            for (int i = 0; i < sim.shop.Count; i++)
            {
                int idx = i;
                var o = sim.shop[i];
                if (o.sold) { menu.Add(OfferName(o, sim) + "（已买）", () => { }, enabled: false); continue; }
                bool afford = sim.player.money >= o.price;
                menu.Add(OfferName(o, sim), () => gm.Buy(idx), $"${o.price}", afford);
            }
            menu.Add($"刷新  ${sim.RerollCost}", () => gm.Reroll(), enabled: sim.player.money >= sim.RerollCost);
            string next = sim.wave >= GameData.Waves.Count ? "查看结算" : "下一波";
            menu.Add(next, () => gm.NextWave());
            if (!string.IsNullOrEmpty(note))
                PixelUI.Label(rt, 52, 268, 376, 16, note, new Color(0.15f, 0.4f, 0.2f), TextAnchor.UpperCenter);
        }

        static string OfferName(ShopOffer o, GameSim sim)
        {
            if (o.item != null) return $"道具  {o.item.name}";
            var owned = sim.player.skills.Find(s => s.def == o.skill);
            return (owned != null ? "强化  " : "招式  ") + o.skill.name;
        }

        public void ShowResult(GameSim sim, bool won, float seconds)
        {
            HideAll();
            Clear(result);
            result.SetActive(true);
            var rt = result.GetComponent<RectTransform>();
            Dim(rt);
            PixelUI.Window(rt, 50, 24, 380, 272);
            PixelUI.Label(rt, 62, 36, 356, 20, won ? "通关了！" : "倒下了……", PixelUI.Text, TextAnchor.UpperCenter);
            var p = sim.player;
            var sb = new StringBuilder();
            sb.Append($"{p.line.name}  Lv{p.level}\n");
            sb.Append($"波次 {(won ? sim.wave : Mathf.Max(0, sim.wave - 1))}/{GameData.Waves.Count}    用时 {seconds:0} 秒\n");
            sb.Append($"击败 {p.stats.kills}（精英 {p.stats.eliteKills}）\n");
            sb.Append($"伤害 {p.stats.damage:0}    受伤 {p.stats.taken:0}    暴击 {p.stats.crits}\n");
            float total = Mathf.Max(1, p.stats.damage);
            var list = new List<KeyValuePair<string, float>>(p.stats.skillDmg);
            list.Sort((a, b) => b.Value.CompareTo(a.Value));
            foreach (var kv in list) sb.Append($"{kv.Key}  {kv.Value / total * 100f:0}%\n");
            PixelUI.Label(rt, 70, 66, 340, 150, sb.ToString());
            menu = new PixelMenu(rt, 70, 224, 340, 24);
            menu.Add("再来一局", () => gm.StartRun(p.line.id));
            menu.Add("返回标题", () => gm.ToTitle());
        }

        void BuildPause()
        {
            var rt = PixelUI.Box(chrome, 0, 0, PixelUI.Width, PixelUI.Height, "Pause");
            pause = rt.gameObject;
        }

        public void SetPause(bool on)
        {
            if (!on) { pause.SetActive(false); if (hud.activeSelf) menu = null; return; }
            Clear(pause);
            pause.SetActive(true);
            var rt = pause.GetComponent<RectTransform>();
            Dim(rt);
            PixelUI.Window(rt, 140, 80, 200, 140);
            PixelUI.Label(rt, 140, 92, 200, 18, "暂停", PixelUI.Text, TextAnchor.UpperCenter);
            menu = new PixelMenu(rt, 156, 120, 168, 28);
            menu.Add("继续", () => gm.TogglePause());
            menu.Add("返回标题", () => gm.ToTitle());
            menu.onCancel = () => gm.TogglePause();
        }

        void BuildBench()
        {
            var rt = PixelUI.Box(chrome, 8, 180, 300, 100, "Bench");
            bench = rt.gameObject;
            PixelUI.Window(rt, 0, 0, 300, 100, "winDark");
        }

        public void ShowBench(string text, bool withHud)
        {
            title.SetActive(false);
            hud.SetActive(withHud);
            bench.SetActive(true);
            Clear(bench);
            var rt = bench.GetComponent<RectTransform>();
            PixelUI.Window(rt, 0, 0, 300, 100, "winDark");
            PixelUI.Label(rt, 8, 8, 284, 84, text, PixelUI.Light, TextAnchor.UpperLeft, shadow: true, shadowColor: PixelUI.DarkShadow);
            menu = null;
        }

        void BuildPerf()
        {
            perfPanel = PixelUI.Box(chrome, 292, 8, 180, 96, "Perf");
            PixelUI.Window(perfPanel, 0, 0, 180, 96, "winDark");
            perfText = PixelUI.Label(perfPanel, 8, 6, 164, 86, "", PixelUI.Light, TextAnchor.UpperLeft, shadow: false);
            perfPanel.gameObject.SetActive(false);
        }

        public void UpdatePerf(string text)
        {
            perfPanel.gameObject.SetActive(PerfVisible);
            if (PerfVisible) perfText.text = text;
        }

        public void SetChrome(bool on) => chrome.gameObject.SetActive(on);

        // ------------------------------------------------------------------ input + preview

        void Update()
        {
            if (!root) return;
            ApplyPixelScale();
            AnimatePreview(Time.unscaledDeltaTime);
            if (msgT > 0 && (msgT -= Time.unscaledDeltaTime) <= 0) msgWindow.gameObject.SetActive(false);
            PollMenu();
        }

        void AnimatePreview(float dt)
        {
            if (previewSpecies == null || !title.activeSelf) return;
            previewT += dt * 60f;
            float dur = previewAnim.durations[Mathf.Min(previewFrame, previewAnim.durations.Length - 1)];
            if (previewT < dur) return;
            previewT = 0;
            previewFrame = (previewFrame + 1) % previewAnim.frames;
            preview.sprite = previewSpecies.Frame(previewAnim, previewFrame, 0);
        }

        void PollMenu()
        {
            if (menu == null) return;
            var k = Keyboard.current;
            var g = Gamepad.current;
            bool up = (k != null && (k.upArrowKey.wasPressedThisFrame || k.wKey.wasPressedThisFrame)) || (g != null && g.dpad.up.wasPressedThisFrame);
            bool down = (k != null && (k.downArrowKey.wasPressedThisFrame || k.sKey.wasPressedThisFrame)) || (g != null && g.dpad.down.wasPressedThisFrame);
            bool ok = (k != null && (k.enterKey.wasPressedThisFrame || k.zKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame)) || (g != null && g.buttonSouth.wasPressedThisFrame);
            bool cancel = k != null && k.xKey.wasPressedThisFrame && menu.onCancel != null;
            if (up) menu.Move(-1);
            if (down) menu.Move(1);
            if (ok) menu.Confirm();
            if (cancel) menu.onCancel();
        }
    }
}
