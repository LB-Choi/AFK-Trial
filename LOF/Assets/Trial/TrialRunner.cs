using UnityEngine;
using UnityEngine.SceneManagement;

namespace AFKTrial
{
    public sealed class TrialRunner : MonoBehaviour
    {
        private TrialBattle battle;
        private float accumulator, restartDelay;
        private bool paused;
        private GUIStyle title, label, small, button;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (SceneManager.GetActiveScene().name != "SampleScene") return;
            if (FindFirstObjectByType<TrialRunner>() == null)
                new GameObject("AFK Trial Test Runner").AddComponent<TrialRunner>();
        }

        private void Awake() { battle = new TrialBattle(); }

        private void Update()
        {
            if (paused) return;
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
            }
            Matrix4x4 oldMatrix = GUI.matrix;
            float scale = Mathf.Min(Screen.width / 540f, Screen.height / 960f);
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 540 * scale) / 2,
                (Screen.height - 960 * scale) / 2, 0), Quaternion.identity, Vector3.one * scale);
            Fill(new Rect(0, 0, 540, 960), new Color(0.055f, 0.08f, 0.13f));
            GUI.Label(new Rect(24, 20, 500, 40), "AFK TRIAL / FUNCTION TEST", title);
            GUI.Label(new Rect(24, 66, 500, 28), TrialBattle.StageName(battle.Stage) +
                (battle.Farming ? "  [FARM]" : "  [CHALLENGE]"), label);
            GUI.Label(new Rect(24, 100, 500, 28), "Gold " + battle.Gold + "    XP " + battle.Experience +
                "    Team Lv." + battle.Level, label);
            GUI.Label(new Rect(24, 136, 500, 28), "Time " + Mathf.Max(0, battle.Limit - battle.Elapsed).ToString("F1") +
                "    Battle Lv." + battle.BattleLevel + (paused ? "    PAUSED" : ""), label);
            Fill(new Rect(24, 184, 492, 440), new Color(0.09f, 0.14f, 0.21f));
            foreach (TrialBattle.Unit unit in battle.Units)
            {
                float x = 270 + unit.Position.x * 95;
                float y = 396 - unit.Position.y * 72;
                Color color = !unit.Alive ? Color.gray : unit.Ally ? new Color(0.2f, 0.8f, 0.85f) : new Color(1, 0.4f, 0.35f);
                Fill(new Rect(x - 24, y - 24, 48, 48), color);
                Fill(new Rect(x - 39, y - 40, 78, 7), Color.black);
                Fill(new Rect(x - 39, y - 40, 78 * unit.Hp / unit.MaxHp, 7), Color.green);
                GUI.Label(new Rect(x - 42, y + 26, 100, 23), unit.Name, small);
                GUI.Label(new Rect(x - 42, y + 47, 100, 23), Mathf.CeilToInt(unit.Hp) + " HP", small);
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
            GUI.Label(new Rect(24, 866, 492, 76),
                "TEMP VALUES / BASIC ATTACKS ONLY\nGrowth applies next battle. Session-only progress.\nNo save, skills, gacha, equipment or idle rewards yet.", small);
            GUI.matrix = oldMatrix;
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
