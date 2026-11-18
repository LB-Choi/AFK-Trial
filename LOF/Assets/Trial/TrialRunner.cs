using UnityEngine;
using UnityEngine.SceneManagement;

namespace AFKTrial
{
    public sealed class TrialRunner : MonoBehaviour
    {
        private TrialBattle battle;
        private readonly Texture2D[] portraits = new Texture2D[TrialBattle.Heroes.Count];
        private readonly int[] draft = new int[3];
        private bool editing;
        private int selectedSlot;
        private bool sideBattle;
        private float accumulator, restartDelay;
        private bool paused;
        private GUIStyle title, label, small, button, damage;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            string scene = SceneManager.GetActiveScene().name;
            if (scene != "SampleScene" && scene != "SideBattle") return;
            if (FindFirstObjectByType<TrialRunner>() == null)
                new GameObject("AFK Trial Test Runner").AddComponent<TrialRunner>();
        }

        private void OnEnable()
        {
            if (battle != null) return;
            sideBattle = gameObject.scene.name == "SideBattle";
            for (int i = 0; i < portraits.Length; i++)
            {
                string key = i == 0 ? "IceKnightIdle" : TrialBattle.Heroes[i].Replace(" ", "");
                portraits[i] = Resources.Load<Texture2D>(key);
            }
            battle = new TrialBattle();
            accumulator = restartDelay = 0;
        }

        private void Update()
        {
            if (paused || editing) return;
            if (battle.Finished)
            {
                restartDelay += Time.deltaTime;
                if (restartDelay >= 1.5f) { battle.StartBattle(); restartDelay = 0; }
                return;
            }
            accumulator += Time.deltaTime;
            while (accumulator >= 0.05f && !battle.Finished)
            { battle.Tick(0.05f); accumulator -= 0.05f; }
            if (battle.Finished) accumulator = 0;
        }

        private void OnGUI()
        {
            if (battle == null) return;
            if (title == null)
            {
                title = new GUIStyle(GUI.skin.label) { fontSize = 27, fontStyle = FontStyle.Bold };
                label = new GUIStyle(GUI.skin.label) { fontSize = 19 };
                small = new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = true };
                button = new GUIStyle(GUI.skin.button) { fontSize = 18 };
                damage = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter };
                damage.normal.textColor = new Color(1f, 0.27f, 0.27f);
            }
            Matrix4x4 oldMatrix = GUI.matrix;
            if (sideBattle)
            {
                float fit = Mathf.Min(Screen.width / 960f, Screen.height / 540f);
                GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 960 * fit) / 2,
                    (Screen.height - 540 * fit) / 2, 0), Quaternion.identity, Vector3.one * fit);
                if (editing) DrawFormation(960, 540); else DrawLandscape();
                GUI.matrix = oldMatrix;
                return;
            }
            float scale = Mathf.Min(Screen.width / 540f, Screen.height / 960f);
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 540 * scale) / 2,
                (Screen.height - 960 * scale) / 2, 0), Quaternion.identity, Vector3.one * scale);
            if (editing)
            {
                DrawFormation(540, 960);
                GUI.matrix = oldMatrix;
                return;
            }
            Fill(new Rect(0, 0, 540, 960), new Color(0.055f, 0.08f, 0.13f));
            GUI.Label(new Rect(24, 20, 500, 40), "AFK TRIAL / FUNCTION TEST", title);
            GUI.Label(new Rect(24, 66, 500, 28), TrialBattle.StageName(battle.Stage) +
                (battle.Farming ? "  [FARM]" : "  [CHALLENGE]"), label);
            GUI.Label(new Rect(24, 100, 500, 28), "Gold " + battle.Gold + "    XP " + battle.Experience +
                "    Team Lv." + battle.Level, label);
            GUI.Label(new Rect(24, 136, 500, 28), "Time " + Mathf.Max(0, battle.Limit - battle.Elapsed).ToString("F1") +
                "    Battle Lv." + battle.BattleLevel + (paused ? "    PAUSED" : ""), label);
            Fill(new Rect(24, 184, 492, 440), new Color(0.09f, 0.14f, 0.21f));
            for (int i = 0; i < battle.Units.Count; i++)
            {
                var unit = battle.Units[i];
                float x = 270 + unit.Position.y * 95;
                float y = 396 - unit.Position.x * 72;
                Color color = !unit.Alive ? Color.gray : unit.Ally ? new Color(0.2f, 0.8f, 0.85f) : new Color(1, 0.4f, 0.35f);
                if (unit.Ally) DrawHero(i, unit, new Rect(x - 38, y - 41, 76, 76));
                else
                {
                    bool hit = battle.Elapsed - unit.LastHitTime < 0.15f;
                    Fill(new Rect(x - 24, y - 24, 48, 48), hit ? Color.white : color);
                }
                float infoX = x - 39;
                Fill(new Rect(infoX, y - 40, 78, 7), Color.black);
                Fill(new Rect(infoX, y - 40, 78 * unit.Hp / unit.MaxHp, 7), Color.green);
                GUI.Label(new Rect(infoX - 3, y + 26, 100, 23), unit.Name, small);
                GUI.Label(new Rect(infoX - 3, y + 47, 100, 23), Mathf.CeilToInt(unit.Hp) + " HP", small);
                DrawDamage(unit, x, y - 42);
            }
            GUI.Label(new Rect(24, 635, 492, 48), battle.Result, small);
            GUI.enabled = battle.Gold >= battle.Cost && battle.Experience >= battle.XpCost;
            if (GUI.Button(new Rect(24, 690, 492, 48), "Team level +1   /   " + battle.Cost + " G + " + battle.XpCost + " XP", button)) battle.Upgrade();
            GUI.enabled = battle.Farming;
            if (GUI.Button(new Rect(24, 748, 492, 48), "Retry: " + TrialBattle.StageName(battle.Next), button))
            { battle.Retry(); accumulator = 0; restartDelay = 0; }
            GUI.enabled = true;
            if (GUI.Button(new Rect(24, 806, 238, 44), paused ? "Resume" : "Pause", button)) paused = !paused;
            GUI.enabled = !battle.Finished;
            if (GUI.Button(new Rect(278, 806, 238, 44), "Test: force defeat", button)) battle.ForceTimeout();
            GUI.enabled = true;
            if (GUI.Button(new Rect(24, 862, 492, 42), "Formation", button)) OpenFormation();
            GUI.Label(new Rect(24, 911, 492, 76),
                "Basic attacks only. Session-only progress.", small);
            GUI.matrix = oldMatrix;
        }

        private void DrawLandscape()
        {
            Fill(new Rect(0, 0, 960, 540), new Color(0.055f, 0.08f, 0.13f));
            GUI.Label(new Rect(18, 8, 295, 38), "AFK TRIAL", title);
            if (GUI.Button(new Rect(830, 10, 115, 35), "Formation", button)) OpenFormation();
            GUI.Label(new Rect(184, 12, 345, 32), TrialBattle.StageName(battle.Stage) +
                (battle.Farming ? "  [FARM]" : "  [CHALLENGE]"), label);
            GUI.Label(new Rect(540, 12, 286, 32), $"Gold {battle.Gold}    XP {battle.Experience}    Team Lv.{battle.Level}", label);
            Fill(new Rect(122, 58, 716, 358), new Color(0.09f, 0.14f, 0.21f));
            for (int i = 0; i < battle.Units.Count; i++)
            {
                TrialBattle.Unit unit = battle.Units[i];
                float x = 480 + unit.Position.x * 138;
                float y = 237 - unit.Position.y * 94;
                Color team = unit.Ally ? new Color(0.2f, 0.8f, 0.85f) : new Color(1, 0.4f, 0.35f);
                if (unit.Ally) DrawHero(i, unit, new Rect(x - 60, y - 55, 120, 110));
                else
                {
                    bool hit = battle.Elapsed - unit.LastHitTime < 0.15f;
                    Fill(new Rect(x - 24, y - 24, 48, 48), !unit.Alive ? Color.gray : hit ? Color.white : team);
                }
                float panelX = unit.Ally ? 12 : 848;
                float panelY = 350 - (i % 3) * 113;
                GUI.Label(new Rect(panelX, panelY - 34, 112, 34), unit.Name, small);
                Fill(new Rect(panelX, panelY, 100, 7), Color.black);
                Fill(new Rect(panelX, panelY, 100 * unit.Hp / unit.MaxHp, 7), team);
                GUI.Label(new Rect(panelX, panelY + 8, 104, 25), Mathf.CeilToInt(unit.Hp) + " HP", small);
                DrawDamage(unit, x, y - (unit.Ally ? 56 : 24));
            }
            GUI.Label(new Rect(16, 424, 470, 32), battle.Result, label);
            GUI.Label(new Rect(515, 424, 430, 32), $"Time {Mathf.Max(0, battle.Limit - battle.Elapsed):F1}    Battle Lv.{battle.BattleLevel}" +
                (paused ? "    PAUSED" : ""), label);
            GUI.enabled = battle.Gold >= battle.Cost && battle.Experience >= battle.XpCost;
            if (GUI.Button(new Rect(16, 470, 320, 50), $"Team level +1 / {battle.Cost} G + {battle.XpCost} XP", button)) battle.Upgrade();
            GUI.enabled = battle.Farming;
            if (GUI.Button(new Rect(348, 470, 260, 50), "Retry: " + TrialBattle.StageName(battle.Next), button))
            { battle.Retry(); accumulator = 0; restartDelay = 0; }
            GUI.enabled = true;
            if (GUI.Button(new Rect(620, 470, 120, 50), paused ? "Resume" : "Pause", button)) paused = !paused;
            GUI.enabled = !battle.Finished;
            if (GUI.Button(new Rect(752, 470, 192, 50), "Test: force defeat", button)) battle.ForceTimeout();
            GUI.enabled = true;
        }

        private void DrawHero(int slot, TrialBattle.Unit unit, Rect area)
        {
            Color previous = GUI.color;
            GUI.color = unit.Alive ? Color.white : Color.gray;
            GUI.DrawTexture(area, portraits[battle.HeroAt(slot)], ScaleMode.ScaleToFit);
            GUI.color = previous;
        }

        private void OpenFormation()
        {
            for (int i = 0; i < draft.Length; i++) draft[i] = battle.HeroAt(i);
            selectedSlot = 0;
            editing = true;
        }

        private void DrawFormation(int width, int height)
        {
            Fill(new Rect(0, 0, width, height), new Color(0.055f, 0.08f, 0.13f));
            GUI.Label(new Rect(20, 12, width - 40, 40), "Formation", title);
            GUI.Label(new Rect(20, 54, width - 40, 28), "Choose a slot, then a hero.", label);
            float slotWidth = (width - 52) / 3f;
            for (int i = 0; i < draft.Length; i++)
            {
                var rect = new Rect(20 + i * (slotWidth + 6), 90, slotWidth, 54);
                if (GUI.Toggle(rect, selectedSlot == i, $"Slot {i + 1}  /  {TrialBattle.Heroes[draft[i]]}", button))
                    selectedSlot = i;
            }
            int columns = sideBattle ? 5 : 2;
            int rows = (portraits.Length + columns - 1) / columns;
            float cardWidth = (width - 40f - (columns - 1) * 6) / columns;
            float cardHeight = (height - 236f) / rows - 6;
            for (int hero = 0; hero < portraits.Length; hero++)
            {
                float x = 20 + hero % columns * (cardWidth + 6);
                float y = 158 + hero / columns * (cardHeight + 6);
                int occupied = System.Array.IndexOf(draft, hero);
                if (GUI.Button(new Rect(x, y, cardWidth, cardHeight), GUIContent.none, button))
                {
                    if (occupied >= 0) draft[occupied] = draft[selectedSlot];
                    draft[selectedSlot] = hero;
                }
                GUI.DrawTexture(new Rect(x + 5, y + 5, cardWidth - 10, cardHeight - 36),
                    portraits[hero], ScaleMode.ScaleToFit);
                string prefix = occupied < 0 ? "" : $"[{occupied + 1}] ";
                GUI.Label(new Rect(x + 8, y + cardHeight - 28, cardWidth - 12, 24),
                    prefix + TrialBattle.Heroes[hero], small);
            }
            if (GUI.Button(new Rect(20, height - 60, 120, 42), "Cancel", button)) editing = false;
            if (GUI.Button(new Rect(width - 245, height - 60, 225, 42), "Apply & restart battle", button))
            {
                battle.ApplyFormation(draft);
                accumulator = restartDelay = 0;
                editing = false;
            }
        }

        private void DrawDamage(TrialBattle.Unit unit, float x, float headY)
        {
            float age = battle.Elapsed + restartDelay - unit.LastHitTime;
            if (age >= 0.7f || unit.LastDamage <= 0) return;
            Rect bounds = new Rect(x - 45, headY - 30 - age * 22, 90, 30);
            GUI.Label(bounds, "-" + unit.LastDamage.ToString("0.##"), damage);
        }

        private static void Fill(Rect rect, Color color)
        {
            Color old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = old;
        }
    }
}
