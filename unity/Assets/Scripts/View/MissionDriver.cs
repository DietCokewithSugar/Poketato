using System.Collections.Generic;
using Poketato.Extraction;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Poketato
{
    /// <summary>
    /// Plays one extraction mission in a 3D room with lit sprite billboards.
    /// The rules live in Poketato.Extraction.RunSession, which does not reference Unity.
    /// </summary>
    public class MissionDriver : MonoBehaviour
    {
        public string missionId = "foggy-forest";
        public int speciesId = 6;
        public int level = 20;
        public bool autopilot = true;

        RunSession session;
        Transform world;
        Camera cam;
        Font font;
        readonly Dictionary<int, Transform> bodies = new Dictionary<int, Transform>();
        readonly Dictionary<PokeType, Texture2D> faces = new Dictionary<PokeType, Texture2D>();
        Material lit;
        Material cutout;
        int skill;
        bool interact;

        void Start()
        {
            font = Resources.Load<Font>("Fonts/FusionPixel12");
            missionId = WebBridge.Param("mission", missionId);
            speciesId = Parse(WebBridge.Param("species"), speciesId);
            level = Parse(WebBridge.Param("level"), level);
            if (WebBridge.Param("auto") == "0") autopilot = false;
            int seed = Parse(WebBridge.Param("seed"), 1);
            session = RunSession.Start(missionId, seed);
            session.rules.noCrit = false;
            session.TryAddPlayer(new PlayerJoin
            {
                speciesId = speciesId,
                level = level,
                autopilot = autopilot
            });
            SetupStage();
            BuildDungeon();
        }

        void Update()
        {
            if (session == null || session.players.Count == 0) return;
            var player = session.players[0];
            ReadKeys(player);
            PlayerInput[] inputs = null;
            if (!player.autopilot && player.alive && !player.extracted)
            {
                Vector2 move = ReadMove();
                Vector3 aim = AimOnFloor();
                inputs = new[]
                {
                    new PlayerInput
                    {
                        playerId = player.id,
                        moveX = move.x,
                        moveZ = move.y,
                        aimX = aim.x,
                        aimZ = aim.z,
                        hasAim = true,
                        skill = skill,
                        interact = interact
                    }
                };
            }
            skill = -1;
            interact = false;
            session.Tick(Time.deltaTime, inputs);
            SyncBodies();
            Follow();
        }

        void SetupStage()
        {
            cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }
            cam.orthographic = false;
            cam.fieldOfView = 40f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 250f;
            cam.allowHDR = true;
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.GetUniversalAdditionalCameraData().renderShadows = true;

            var sun = FindAnyObjectByType<Light>();
            if (sun == null || sun.type != LightType.Directional)
            {
                var go = new GameObject("Sun");
                sun = go.AddComponent<Light>();
                sun.type = LightType.Directional;
            }
            sun.color = new Color(1f, 0.96f, 0.88f);
            sun.intensity = 1.15f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.85f;
            sun.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = missionId == "dark-hill"
                ? new Color(0.12f, 0.12f, 0.16f)
                : new Color(0.34f, 0.36f, 0.4f);

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            lit = new Material(shader);
            lit.SetFloat("_Smoothness", 0.82f);
            lit.SetFloat("_Metallic", 0.22f);
            cutout = new Material(shader);
            cutout.SetFloat("_AlphaClip", 1f);
            cutout.SetFloat("_Cutoff", 0.3f);
            cutout.EnableKeyword("_ALPHATEST_ON");
            cutout.SetFloat("_Cull", 0f);
        }

        void BuildDungeon()
        {
            world = new GameObject("Dungeon").transform;
            var mission = session.mission;
            float minX = 999f, maxX = -999f, minZ = 999f, maxZ = -999f;
            bool water = mission.reflectiveWater;
            Material waterMat = null;
            if (water)
            {
                var waterShader = Shader.Find("Poketato/PlanarWater");
                if (waterShader != null)
                {
                    waterMat = new Material(waterShader);
                    waterMat.SetColor("_Color", new Color(mission.cr * 0.6f, mission.cg * 0.8f, mission.cb, 1f));
                    var planar = gameObject.AddComponent<PlanarWater>();
                    planar.source = cam;
                    planar.SetMaterial(waterMat);
                }
            }
            foreach (var room in mission.rooms)
            {
                float ox = room.gx * (DungeonLayout.RoomW + DungeonLayout.Gap);
                float oz = room.gz * (DungeonLayout.RoomD + DungeonLayout.Gap);
                var color = new Color(mission.cr, mission.cg, mission.cb);
                if (room.kind == RoomKind.Boss) color = Color.Lerp(color, new Color(0.7f, 0.25f, 0.2f), 0.35f);
                if (room.kind == RoomKind.Extract || room.kind == RoomKind.EarlyExtract)
                    color = Color.Lerp(color, new Color(0.85f, 0.75f, 0.35f), 0.45f);
                var floor = Cube("Floor " + room.id, new Vector3(ox + 8f, -0.1f, oz + 6f), new Vector3(16f, 0.2f, 12f), color, lit);
                floor.transform.SetParent(world, true);
                minX = Mathf.Min(minX, ox);
                maxX = Mathf.Max(maxX, ox + 16f);
                minZ = Mathf.Min(minZ, oz);
                maxZ = Mathf.Max(maxZ, oz + 12f);
                if (waterMat != null)
                {
                    var pond = Cube("Water " + room.id, new Vector3(ox + 8f, 0.12f, oz + 6f), new Vector3(14.5f, 0.04f, 10.5f),
                        new Color(mission.cr * 0.55f, mission.cg * 0.75f, mission.cb, 1f), waterMat);
                    pond.layer = 4;
                    pond.transform.SetParent(world, true);
                }
                BuildWalls(room, ox, oz, color);
                var lampGo = new GameObject("Lamp " + room.id);
                lampGo.transform.SetParent(world, true);
                lampGo.transform.position = new Vector3(ox + 8f, 4.2f, oz + 6f);
                var lamp = lampGo.AddComponent<Light>();
                lamp.type = LightType.Point;
                lamp.range = 16f;
                lamp.intensity = room.kind == RoomKind.Boss ? 4f : 2.2f;
                lamp.color = Color.Lerp(Color.white, color, 0.35f);
                lamp.shadows = room.kind == RoomKind.Boss ? LightShadows.Soft : LightShadows.None;
            }

            var probeGo = GameObject.Find("Reflection Probe");
            if (probeGo == null) probeGo = new GameObject("Reflection Probe");
            probeGo.transform.SetParent(world, true);
            probeGo.transform.position = new Vector3((minX + maxX) * 0.5f, 3f, (minZ + maxZ) * 0.5f);
            var probe = probeGo.GetComponent<ReflectionProbe>();
            if (probe == null) probe = probeGo.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
            probe.boxProjection = true;
            probe.size = new Vector3(Mathf.Max(20f, maxX - minX + 8f), 16f, Mathf.Max(20f, maxZ - minZ + 8f));
            probe.resolution = 128;
            probe.hdr = true;
            probe.RenderProbe();
        }

        void BuildWalls(RoomDef room, float ox, float oz, Color color)
        {
            Wall(room, ox, oz, 1, 0, color);
            Wall(room, ox, oz, -1, 0, color);
            Wall(room, ox, oz, 0, 1, color);
            Wall(room, ox, oz, 0, -1, color);
        }

        void Wall(RoomDef room, float ox, float oz, int dx, int dz, Color color)
        {
            bool door = HasNeighbor(room, dx, dz);
            var wall = Color.Lerp(color, Color.black, 0.35f);
            if (dx != 0)
            {
                float x = ox + (dx > 0 ? 16f : 0f);
                if (!door) Cube("Wall", new Vector3(x, 1.25f, oz + 6f), new Vector3(0.4f, 2.5f, 12f), wall, lit).transform.SetParent(world, true);
                else
                {
                    Cube("Wall", new Vector3(x, 1.25f, oz + 2.2f), new Vector3(0.4f, 2.5f, 4.4f), wall, lit).transform.SetParent(world, true);
                    Cube("Wall", new Vector3(x, 1.25f, oz + 9.8f), new Vector3(0.4f, 2.5f, 4.4f), wall, lit).transform.SetParent(world, true);
                }
            }
            else
            {
                float z = oz + (dz > 0 ? 12f : 0f);
                if (!door) Cube("Wall", new Vector3(ox + 8f, 1.25f, z), new Vector3(16f, 2.5f, 0.4f), wall, lit).transform.SetParent(world, true);
                else
                {
                    Cube("Wall", new Vector3(ox + 3.2f, 1.25f, z), new Vector3(6.4f, 2.5f, 0.4f), wall, lit).transform.SetParent(world, true);
                    Cube("Wall", new Vector3(ox + 12.8f, 1.25f, z), new Vector3(6.4f, 2.5f, 0.4f), wall, lit).transform.SetParent(world, true);
                }
            }
        }

        bool HasNeighbor(RoomDef room, int dx, int dz)
        {
            foreach (var link in room.links)
            {
                foreach (var other in session.mission.rooms)
                {
                    if (other.id != link) continue;
                    if (other.gx - room.gx == dx && other.gz - room.gz == dz) return true;
                }
            }
            return false;
        }

        GameObject Cube(string name, Vector3 position, Vector3 scale, Color color, Material source)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.position = position;
            go.transform.localScale = scale;
            var material = new Material(source);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            else material.color = color;
            go.GetComponent<Renderer>().sharedMaterial = material;
            var collider = go.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            return go;
        }

        void SyncBodies()
        {
            var alive = new HashSet<int>();
            for (int i = 0; i < session.actors.Count; i++)
            {
                var actor = session.actors[i];
                if (!actor.alive) continue;
                alive.Add(actor.id);
                Transform body;
                if (!bodies.TryGetValue(actor.id, out body))
                {
                    var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    go.name = actor.species.nameZh;
                    var collider = go.GetComponent<Collider>();
                    if (collider != null) Destroy(collider);
                    var material = new Material(cutout);
                    var face = Face(actor.species.primary);
                    if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", face);
                    else material.mainTexture = face;
                    go.GetComponent<Renderer>().sharedMaterial = material;
                    body = go.transform;
                    body.SetParent(world, true);
                    float height = actor.isBoss ? 2.4f : 1.5f;
                    body.localScale = new Vector3(height * 0.8f, height, 1f);
                    bodies[actor.id] = body;
                }
                body.position = new Vector3(actor.x, actor.isBoss ? 1.3f : 0.9f, actor.z);
                var flat = cam.transform.forward;
                flat.y = 0f;
                if (flat.sqrMagnitude > 0.001f) body.rotation = Quaternion.LookRotation(flat);
            }
            var stale = new List<int>();
            foreach (var pair in bodies)
                if (!alive.Contains(pair.Key)) stale.Add(pair.Key);
            for (int i = 0; i < stale.Count; i++)
            {
                Destroy(bodies[stale[i]].gameObject);
                bodies.Remove(stale[i]);
            }
        }

        Texture2D Face(PokeType type)
        {
            Texture2D texture;
            if (faces.TryGetValue(type, out texture)) return texture;
            texture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
            var color = TypeColor(type);
            var clear = new Color(0f, 0f, 0f, 0f);
            for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
            {
                float dx = x - 15.5f;
                float dy = y - 14f;
                bool body = dx * dx + dy * dy < 150f;
                bool eye = (x == 11 || x == 20) && y > 16 && y < 21;
                texture.SetPixel(x, y, !body ? clear : eye ? Color.white : color);
            }
            texture.Apply();
            faces[type] = texture;
            return texture;
        }

        static Color TypeColor(PokeType type)
        {
            switch (type)
            {
                case PokeType.Fire: return new Color(0.9f, 0.35f, 0.15f);
                case PokeType.Water: return new Color(0.25f, 0.45f, 0.9f);
                case PokeType.Electric: return new Color(0.95f, 0.85f, 0.2f);
                case PokeType.Grass: return new Color(0.3f, 0.7f, 0.28f);
                case PokeType.Ice: return new Color(0.6f, 0.85f, 0.95f);
                case PokeType.Fighting: return new Color(0.7f, 0.25f, 0.2f);
                case PokeType.Poison: return new Color(0.6f, 0.3f, 0.7f);
                case PokeType.Ground: return new Color(0.65f, 0.5f, 0.28f);
                case PokeType.Flying: return new Color(0.7f, 0.75f, 0.9f);
                case PokeType.Psychic: return new Color(0.9f, 0.4f, 0.6f);
                case PokeType.Bug: return new Color(0.55f, 0.65f, 0.2f);
                case PokeType.Rock: return new Color(0.55f, 0.48f, 0.35f);
                case PokeType.Ghost: return new Color(0.4f, 0.3f, 0.6f);
                case PokeType.Dragon: return new Color(0.35f, 0.4f, 0.85f);
                default: return new Color(0.75f, 0.7f, 0.6f);
            }
        }

        void Follow()
        {
            if (session.players.Count == 0) return;
            var actor = session.players[0].actor;
            var focus = new Vector3(actor.x, 0f, actor.z);
            cam.transform.position = focus + new Vector3(0f, 14f, -11f);
            cam.transform.rotation = Quaternion.Euler(52f, 0f, 0f);
        }

        void ReadKeys(PlayerState player)
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.tKey.wasPressedThisFrame) player.autopilot = !player.autopilot;
            if (player.autopilot) return;
            if (keyboard.digit1Key.wasPressedThisFrame) skill = 1;
            if (keyboard.digit2Key.wasPressedThisFrame) skill = 2;
            if (keyboard.digit3Key.wasPressedThisFrame) skill = 3;
            if (keyboard.digit4Key.wasPressedThisFrame) skill = 4;
            if (keyboard.jKey.wasPressedThisFrame || (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame))
                skill = 0;
            if (keyboard.eKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame) interact = true;
        }

        static Vector2 ReadMove()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return Vector2.zero;
            float x = 0f, z = 0f;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) x -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) x += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) z -= 1f;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) z += 1f;
            return new Vector2(x, z);
        }

        Vector3 AimOnFloor()
        {
            if (Mouse.current == null || cam == null) return Vector3.zero;
            var ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (Mathf.Abs(ray.direction.y) < 0.001f) return Vector3.zero;
            float t = -ray.origin.y / ray.direction.y;
            return ray.origin + ray.direction * t;
        }

        void OnGUI()
        {
            if (session == null || session.players.Count == 0) return;
            if (font != null)
            {
                GUI.skin.label.font = font;
                GUI.skin.box.font = font;
            }
            GUI.skin.label.fontSize = 16;
            var player = session.players[0];
            var roomName = player.roomId;
            foreach (var room in session.mission.rooms)
                if (room.id == player.roomId) roomName = room.nameZh;
            string state = player.extracted ? "已撤离" : !player.alive ? "倒下，本局掉落已丢失" : session.failed ? "任务失败" : "搜索中";
            GUI.Label(new Rect(16, 12, 900, 28), session.mission.nameZh + "  " + player.species.nameZh + "  " + state);
            GUI.Label(new Rect(16, 36, 900, 28),
                "房间 " + roomName + "   生命 " + Mathf.CeilToInt(player.actor.hp) + "/" + Mathf.CeilToInt(player.actor.maxHp)
                + "   本局金币 " + player.runGold + "   背包 " + player.bag.Count);
            GUI.Label(new Rect(16, 60, 1100, 28),
                (player.autopilot ? "自动战斗（T 关闭）" : "手动：WASD 移动，鼠标瞄准，J/左键普攻，1-4 技能，E 交互")
                + "    死亡只丢本局掉落");
        }

        static int Parse(string text, int fallback)
        {
            int value;
            return int.TryParse(text, out value) ? value : fallback;
        }
    }
}
