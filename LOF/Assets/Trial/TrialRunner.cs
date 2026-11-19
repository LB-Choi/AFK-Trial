using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AFKTrial
{
    public sealed partial class TrialRunner : MonoBehaviour
    {
        private TrialBattle battle;
        private readonly Dictionary<string, TrialCharacterRig> rigs = new Dictionary<string, TrialCharacterRig>();
        private readonly List<(int slot, float x, float y)> fieldDrawOrder = new List<(int, float, float)>();
        private Texture2D[] portraits;
        private Texture2D hitFlash;
        private string loadError;
        private readonly int[] draft = new int[3];
        private bool editing;
        private int selectedSlot;
        private bool sideBattle;
        private float accumulator, restartDelay;
        private float BattleAnimationTime => battle.Elapsed + restartDelay;
        private bool heroesFromMenu;
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
            sideBattle = gameObject.scene.name == "SideBattle";
            try
            {
                var data = TrialData.Load();
                portraits = new Texture2D[data.Heroes.Count];
                for (int i = 0; i < portraits.Length; i++)
                    portraits[i] = LoadTexture(data.Heroes[i].Portrait);
                hitFlash = LoadTexture(data.HitTexture);
                LoadBattleArt();
                LoadFormationArt();
                rigs.Clear();
                foreach (var entry in data.RigResources)
                    rigs.Add(entry.Key, new TrialCharacterRig(entry.Value));
                battle = new TrialBattle(data);
                OpenProgressSession();
                loadError = null;
            }
            catch (System.Exception error)
            {
                battle = null;
                loadError = error.Message;
                Debug.LogException(error);
                return;
            }
            accumulator = restartDelay = 0;
        }

        private static Texture2D LoadTexture(string key)
        {
            var texture = Resources.Load<Texture2D>(key);
            if (texture == null) throw new System.IO.InvalidDataException("Missing texture in Resources: " + key);
            return texture;
        }

        private void Update()
        {
            if (battle == null || progressLock == null || suspended) return;
            if (Time.realtimeSinceStartupAsDouble >= nextCheckpoint) CheckpointProgress();
            if (editing || HasPendingIdleReward) return;
            if (battle.Finished)
            {
                restartDelay += Time.deltaTime;
                if (restartDelay >= battle.Data.RestartDelay) { battle.StartBattle(); restartDelay = 0; }
                return;
            }
            accumulator += Time.deltaTime;
            while (accumulator >= 0.05f && !battle.Finished)
            { battle.Tick(0.05f); accumulator -= 0.05f; }
            if (battle.Finished)
            {
                accumulator = 0;
                CheckpointProgress(battle.Won);
            }
        }

        private void OnGUI()
        {
            if (battle == null)
            {
                if (loadError != null) GUI.Box(new Rect(12, 12, Screen.width - 24, 100), "Data loading failed\n" + loadError);
                return;
            }
            if (title == null)
            {
                title = new GUIStyle(GUI.skin.label) { fontSize = 27, fontStyle = FontStyle.Bold };
                label = new GUIStyle(GUI.skin.label) { fontSize = 19 };
                small = new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = true };
                button = new GUIStyle(GUI.skin.button) { fontSize = 18 };
                damage = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter };
                damage.normal.textColor = new Color(1f, 0.27f, 0.27f);
                InitKnightSkin();
            }
            Matrix4x4 oldMatrix = GUI.matrix;
            ApplyKnightSkin();
            try { DrawGameGui(); }
            finally { GUI.matrix = oldMatrix; RestoreKnightSkin(); }
        }

        private void DrawGameGui()
        {
            if (sideBattle)
            {
                float fit = Mathf.Min(Screen.width / 960f, Screen.height / 540f);
                GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 960 * fit) / 2,
                    (Screen.height - 540 * fit) / 2, 0), Quaternion.identity, Vector3.one * fit);
                if (HasPendingIdleReward) DrawIdleReturnPopup(960, 540);
                else if (editing) DrawHeroes(960, 540);
                else DrawLandscape();

                return;
            }
            float scale = Mathf.Min(Screen.width / 540f, Screen.height / 960f);
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 540 * scale) / 2,
                (Screen.height - 960 * scale) / 2, 0), Quaternion.identity, Vector3.one * scale);
            if (HasPendingIdleReward)
            {
                DrawIdleReturnPopup(540, 960);
                return;
            }
            if (editing)
            {
                DrawHeroes(540, 960);

                return;
            }
            DrawBattleGround(540, 960);
            GUI.Label(new Rect(24, 12, 330, 42), "Castle Gate", title);
            if (GUI.Button(new Rect(393, 15, 123, 38), "Menu", button)) OpenMenu();
            DrawStage(new Rect(24, 62, 492, 43));
            DrawWallet(new Rect(24, 121, 267, 38));
            DrawTimer(new Rect(302, 118, 214, 44));
            DrawBattlefield(new Rect(24, 184, 492, 440));
            DrawBattleUnits();
            DrawBattleStatus(new Rect(24, 639, 492, 51));
            DrawTeamUpgrade(new Rect(24, 707, 492, 56));
            DrawBattleRetry(new Rect(24, 774, 492, 49));
            if (GUI.Button(new Rect(24, 834, 492, 49), "Heroes", button)) OpenHeroes();
            DrawDefeatTest(new Rect(324, 903, 192, 33));

        }

        private void DrawLandscape()
        {
            DrawBattleGround(960, 540);
            GUI.Label(new Rect(13, 9, 175, 41), "Castle Gate", title);
            DrawStage(new Rect(200, 7, 330, 42));
            DrawWallet(new Rect(552, 10, 265, 35));
            if (GUI.Button(new Rect(829, 9, 118, 38), "Menu", button)) OpenMenu();
            DrawBattlefield(new Rect(122, 58, 716, 358));
            DrawFlankHeader(8, true);
            DrawFlankHeader(844, false);
            DrawBattleUnits();
            DrawBattleStatus(new Rect(12, 423, 555, 41));
            DrawTimer(new Rect(585, 423, 362, 41));
            DrawTeamUpgrade(new Rect(12, 477, 273, 49));
            DrawBattleRetry(new Rect(296, 477, 258, 49));
            if (GUI.Button(new Rect(565, 477, 196, 49), "Heroes", button)) OpenHeroes();
            DrawDefeatTest(new Rect(776, 486, 171, 32));
        }

        private void PrepareFieldDrawOrder()
        {
            fieldDrawOrder.Clear();
            for (int slot = 0; slot < battle.Units.Count; slot++)
            {
                Vector2 position = battle.Units[slot].Position;
                fieldDrawOrder.Add(sideBattle
                    ? (slot, 480 + position.x * 138, 269 - position.y * 68)
                    : (slot, 270 + position.y * 95, 396 - position.x * 54));
            }
            // IMGUI paints back to front. Keep the original slot order when ground positions are level.
            fieldDrawOrder.Sort((a, b) =>
            {
                int byHeight = a.y.CompareTo(b.y);
                return byHeight != 0 ? byHeight : a.slot.CompareTo(b.slot);
            });
        }

        private void DrawBattleUnits()
        {
            if (Event.current.type != EventType.Repaint) return;
            PrepareFieldDrawOrder();
            foreach (var entry in fieldDrawOrder)
                DrawCombatant(entry.slot, battle.Units[entry.slot], entry.x, entry.y);

            // Overlapping rigs and their weapon trails must be behind every victim's hit slash.
            float flashSize = sideBattle ? battle.Data.LandscapeSize : battle.Data.PortraitSize;
            foreach (var entry in fieldDrawOrder)
                DrawHitFlash(battle.Units[entry.slot], entry.x, entry.y, flashSize);

            foreach (var entry in fieldDrawOrder)
            {
                var unit = battle.Units[entry.slot];
                if (sideBattle) DrawFlankUnit(unit, entry.slot % 3);
                else
                {
                    DrawHp(new Rect(entry.x - 39, entry.y - 44, 78, 8), unit);
                    DrawFieldName(new Rect(entry.x - 54, entry.y + 29, 108, 35), unit);
                }
            }
            foreach (var entry in fieldDrawOrder)
            {
                var unit = battle.Units[entry.slot];
                float headOffset = sideBattle ? (unit.Ally || !string.IsNullOrEmpty(unit.Stats.Animation) ? 56 : 24) : 42;
                DrawDamage(unit, entry.x, entry.y - headOffset);
            }
        }

        private Rect CombatantArea(float x, float y) => sideBattle
            ? new Rect(x - 60, y - 55, 120, 110) : new Rect(x - 38, y - 41, 76, 76);

        private void DrawCombatant(int index, TrialBattle.Unit unit, float x, float y)
        {
            if (!unit.Ally && string.IsNullOrEmpty(unit.Stats.Animation))
            {
                bool hit = BattleAnimationTime - unit.LastHitTime < 0.15f;
                Color tint = unit.Alive ? new Color(1, 0.4f, 0.35f) : Color.gray;
                Fill(new Rect(x - 24, y - 24, 48, 48), hit && (unit.Alive || !sideBattle) ? Color.white : tint);
                return;
            }
            var area = CombatantArea(x, y);
            Color previous = GUI.color;
            GUI.color = unit.Alive ? Color.white : Color.gray;
            var portrait = unit.Ally ? portraits[battle.HeroAt(index)] : null;
            if (rigs.TryGetValue(unit.Stats.Animation, out var rig)) rig.Draw(area, unit, BattleAnimationTime, portrait);
            else GUI.DrawTexture(area, portrait, ScaleMode.ScaleToFit);
            GUI.color = previous;
        }

        private enum HeroView { List, Info, Formation, Menu, GearPicker }
        private HeroView heroView;

        private void OpenHeroes()
        {
            featurePage = -1;
            heroView = HeroView.List;
            heroesFromMenu = false;
            editing = true;
        }

        private void DrawHeroes(int width, int height)
        {

            if (formationName == null)
                formationName = new GUIStyle(small) { alignment = TextAnchor.MiddleCenter };
            if (featurePage >= 0) { DrawFeatureScreen(width, height); return; }
            DrawKnightBackdrop(width, height);
            if (heroView == HeroView.Menu) { DrawMenu(width, height); return; }
            if (heroView == HeroView.Formation) { DrawHeroSlots(width, height); return; }
            if (heroView == HeroView.GearPicker)
            {
                GUI.Label(new Rect(20, 12, width - 180, 42), battle.Heroes[featureHero], title);
                if (GUI.Button(new Rect(width - 145, 12, 125, 42), "Hero info", button)) heroView = HeroView.Info;
                GUI.BeginGroup(new Rect((width - 500) / 2f, (height - 410) / 2f, 500, 410));
                DrawInventory(true);
                GUI.EndGroup();
                return;
            }
            if (heroView == HeroView.Info)
            {
                DrawKnightPanel(new Rect((width - 516) / 2f, (height - 416) / 2f, 516, 408));
                GUI.Label(new Rect(20, 12, 280, 42), "Knight dossier", title);
                if (GUI.Button(new Rect(width - 145, 12, 125, 42), "Heroes", button)) heroView = HeroView.List;
                GUI.BeginGroup(new Rect((width - 500) / 2f, (height - 410) / 2f, 500, 410));
                DrawSkillPage();
                GUI.EndGroup();
                if (GUI.Button(new Rect(width / 2f - 235, height - 58, 220, 42), "Edit formation", button))
                    BeginHeroPlacement();
                if (GUI.Button(new Rect(width / 2f + 15, height - 58, 220, 42), "OK - Return to battle", button))
                    editing = false;
                return;
            }
            GUI.Label(new Rect(20, 12, 240, 42), "Hero roster", title);
            DrawRosterCount(new Rect(width - 290, 12, 140, 42));

            if (GUI.Button(new Rect(width - 140, 12, 120, 42), heroesFromMenu ? "Menu" : "Battle", button))
            {
                if (heroesFromMenu) OpenMenu(); else editing = false;
            }
            DrawHeroGrid(width, 70, height - 90, false);
        }

        private void DrawHeroGrid(int width, float top, float availableHeight, bool placing)
        {
            int columns = sideBattle ? 5 : 2;
            int rows = (portraits.Length + columns - 1) / columns;
            float cardWidth = (width - 40f - (columns - 1) * 6) / columns;
            float cardHeight = availableHeight / rows - 6;
            for (int hero = 0; hero < portraits.Length; hero++)
            {
                float x = 20 + hero % columns * (cardWidth + 6);
                float y = top + hero / columns * (cardHeight + 6);
                var card = new Rect(x, y, cardWidth, cardHeight);
                GUI.enabled = battle.Owned(hero);
                if (GUI.Button(card, GUIContent.none, GUIStyle.none))
                {
                    if (placing) PlaceHero(hero, selectedSlot);
                    else { featureHero = hero; selectedSkill = -1; heroView = HeroView.Info; }
                }
                Fill(new Rect(x + 4, y + 3, cardWidth - 8, cardHeight - 6), new Color(0.065f, 0.12f, 0.17f, 0.92f));
                GUI.DrawTexture(card, cardFrame);
                GUI.DrawTexture(new Rect(x + 5, y + 5, cardWidth - 10, cardHeight - 36),
                    portraits[hero], ScaleMode.ScaleToFit);
                for (int slot = 0; slot < draft.Length; slot++)
                    if ((placing ? draft[slot] : battle.HeroAt(slot)) == hero)
                        GUI.DrawTexture(new Rect(x + 6, y + 5, 28, 32), deploymentBadges[slot]);
                var role = battle.Data.Heroes[hero].Role;
                if (role != TrialData.Role.None && battle.Owned(hero))
                    Picture("RoleUI/" + RoleIcons[(int)role - 1], card.xMax - 31, y + 6, 24, 24);
                if (placing && selectedSlot >= 0 && hero == draft[selectedSlot]) GUI.DrawTexture(card, cardSelection);
                GUI.Label(new Rect(x + 8, card.yMax - 28, cardWidth - 16, 25), battle.Owned(hero) ? battle.Heroes[hero] : "Locked", formationName);
                GUI.enabled = true;
            }
        }

        private void BeginHeroPlacement()
        {
            for (int slot = 0; slot < draft.Length; slot++) draft[slot] = battle.HeroAt(slot);
            selectedSlot = -1;
            heroView = HeroView.Formation;
        }

        private void SelectHeroSlot(int slot)
        {
            for (int i = 0; i < draft.Length; i++) draft[i] = battle.HeroAt(i);
            PlaceHero(featureHero, slot);
            selectedSlot = slot;
        }

        private void PlaceHero(int hero, int slot)
        {
            int occupied = System.Array.IndexOf(draft, hero);
            if (occupied >= 0) draft[occupied] = draft[slot];
            draft[slot] = hero;
        }

        private void DrawHeroSlots(int width, int height)
        {
            GUI.Label(new Rect(20, 12, width - 40, 42), "Edit formation", title);
            GUI.Label(new Rect(20, 65, width - 40, 40), "Choose a slot for " + battle.Heroes[featureHero], label);
            float w = (width - 56f) / draft.Length;
            for (int i = 0; i < draft.Length; i++)
            {
                var card = new Rect(20 + i * (w + 8), (height - 220) / 2f, w, 220);
                if (GUI.Button(card, GUIContent.none, GUIStyle.none)) SelectHeroSlot(i);
                GUI.DrawTexture(card, cardFrame);
                GUI.DrawTexture(new Rect(card.x + 8, card.y + 8, w - 16, 170), portraits[draft[i]], ScaleMode.ScaleToFit);
                GUI.DrawTexture(new Rect(card.x + 8, card.y + 8, 28, 32), deploymentBadges[i]);
                if (selectedSlot == i) GUI.DrawTexture(card, cardSelection);
                GUI.Label(new Rect(card.x + 5, card.yMax - 33, w - 10, 28), battle.Heroes[draft[i]], formationName);
            }
            DrawSynergyLine(20, 108, "Active", battle.ActiveSynergy);
            if (selectedSlot >= 0) DrawSynergyLine(20, (height + 220) / 2f + 12, "After applying", battle.FormationSynergy(draft));
            if (GUI.Button(new Rect(20, height - 60, 120, 42), "Cancel", button)) heroView = HeroView.Info;
            GUI.enabled = selectedSlot >= 0 && battle.HeroAt(selectedSlot) != featureHero;
            if (GUI.Button(new Rect(width - 245, height - 60, 225, 42), "Apply & restart battle", button))
            {
                battle.ApplyFormation(draft);
                accumulator = restartDelay = 0;
                editing = false;
            }
            GUI.enabled = true;
        }
        private void DrawHitFlash(TrialBattle.Unit unit, float x, float y, float size)
        {
            if (unit.LastDamage <= 0 || Event.current.type != EventType.Repaint) return;
            var data = battle.Data;
            float age = BattleAnimationTime - unit.LastHitTime;
            int frame = Mathf.FloorToInt(age * data.HitFps);
            if (frame < 0 || frame >= data.HitFrames) return;
            var uv = new Rect((frame % data.HitColumns) / (float)data.HitColumns,
                1 - (frame / data.HitColumns + 1f) / data.HitRows, 1f / data.HitColumns, 1f / data.HitRows);
            var topLeft = new Vector2(x - size / 2, y - size / 2);
            if (unit.LastHitHeight > 0)
            {
                var contact = TrialCharacterRig.ScreenPointAtHeight(CombatantArea(x, y), unit.LastHitHeight, out _);
                topLeft = contact - data.HitSparkPivot * size;
            }
            GUI.DrawTextureWithTexCoords(new Rect(topLeft, new Vector2(size, size)), hitFlash, uv);
        }

        private void DrawDamage(TrialBattle.Unit unit, float x, float headY)
        {
            float age = BattleAnimationTime - unit.LastHitTime;
            if (age >= 0.7f || unit.LastDamage <= 0) return;
            float top = Mathf.Max(sideBattle ? 58 : 184, headY - 30 - age * 22);
            Rect bounds = new Rect(x - 45, top, 90, 30);
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
