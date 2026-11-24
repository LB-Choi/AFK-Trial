using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AFKTrial
{
    public sealed partial class TrialRunner : MonoBehaviour
    {
        private TrialBattle battle;
        private readonly Dictionary<string, TrialCharacterRig> bladeRigs = new Dictionary<string, TrialCharacterRig>();
        private readonly List<(int slot, float x, float y, bool alive)> fieldDrawOrder = new List<(int, float, float, bool)>();
        private Texture2D[] portraits;
        private Texture2D hitFlash;
        private string loadError;
        private readonly int[] draft = new int[3];
        private bool editing;
        private bool atTitle, showRecruit;
        private int selectedSlot;
        private bool sideBattle;
        private float accumulator, restartDelay;
        private float finishedAnimationAge;
        private float BattleAnimationTime => battle.Elapsed + Mathf.Max(restartDelay, finishedAnimationAge);
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
            InitializeLifecycle();
            sideBattle = gameObject.scene.name == "SideBattle";
            ResetGuideUI();
            resultDungeon = null;
            atTitle = true;
            ceremony = CeremonyPhase.Closed;
            ceremonyPulls = null;
            showRecruit = false;
            bossReview = bossPrevious = null;
            bossResultPending = false;
            systemMenu = SystemMenu.Closed;
            LoadDisplaySettings();
            try
            {
                var data = TrialData.Load();
                portraits = new Texture2D[data.Heroes.Count];
                for (int i = 0; i < portraits.Length; i++)
                    portraits[i] = LoadUnifiedPortrait(data.Heroes[i].Animation);
                hitFlash = LoadTexture(data.HitTexture);
                LoadBattleArt();
                LoadFormationArt();
                bladeRigs.Clear();
                foreach (var entry in data.RigResources)
                    if (entry.Value == TrialCharacterRig.FrostBladeResource)
                        bladeRigs.Add(entry.Key, new TrialCharacterRig(entry.Value));
                battle = new TrialBattle(data);
                LoadSkillEffects();
                battle.SkillApplied += PlaySkillEffect;
                loadError = null;
            }
            catch (System.Exception error)
            {
                battle = null;
                loadError = "게임을 시작하지 못했습니다. 데이터·저장 파일과 다른 실행 창을 확인해 주세요.";
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
            if (Application.isPlaying) EnsureAudio();
            if (battle == null || progressLock == null) return;
            if (!battle.Finished) finishedAnimationAge = 0;
            else if (!suspended) finishedAnimationAge = Mathf.Min(2f, finishedAnimationAge + Time.deltaTime);
            if (RefreshLifecycle() || suspended) return;
            if (ceremony != CeremonyPhase.Closed)
            {
                UpdateSummonCeremony(Time.unscaledDeltaTime);
                if (Time.realtimeSinceStartupAsDouble >= nextCheckpoint) CheckpointProgress();
                return;
            }
            UpdateGuides();
            if (Time.realtimeSinceStartupAsDouble >= nextCheckpoint) CheckpointProgress();
            if (GuideOpen) return;
            if (!atTitle && battle.CurrentBossGuide.HasValue &&
                !battle.GuideSeen(battle.CurrentBossGuide.Value)) return;
            if (HasPendingIdleReward || HasDungeonResult || showRecruit || SystemMenuOpen ||
                (HasBossReview && !BossReviewAllowsFarming)) return;
            ResumeBattleAfterAway();
            if (battle.Finished)
            {
                if (battle.ActiveDungeon != null) { ReturnFromDungeon(); return; }
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
                if (battle.ActiveDungeon != null) ReturnFromDungeon();
                else
                {
                    battle.ObserveGuideConditions();
                    CheckpointProgress(battle.Won);
                    showRecruit = battle.RecruitReward.HasValue;
                    ShowCompletedBoss();
                }
            }
        }

        private void EnterDungeon(TrialData.DungeonTier tier)
        {
            if (!battle.TryStartDungeon(tier.Kind, tier.Index)) return;
            accumulator = restartDelay = 0;
            editing = false;
        }

        private void OnGUI()
        {
            if (battle == null)
            {
                if (loadError != null) GUI.Box(new Rect(12, 12, Screen.width - 24, 100), "게임 데이터를 불러오지 못했습니다. 데이터 파일을 확인해 주세요.");
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
            int width = sideBattle ? 960 : 540, height = sideBattle ? 540 : 960;
            float scale = Mathf.Min((float)Screen.width / width, (float)Screen.height / height);
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - width * scale) / 2,
                (Screen.height - height * scale) / 2, 0), Quaternion.identity, Vector3.one * scale);
            if (ceremony != CeremonyPhase.Closed)
            {
                DrawSummonCeremony(width, height);
                return;
            }
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            {
                if (GuideOpen) DismissGuide(true);
                else if (SystemMenuOpen) BackFromSystemMenu();
                else if (CanOpenSystemMenu) OpenSystemMenu();
                Event.current.Use();
            }
            if (GuideOpen)
            {
                if (Event.current.type == EventType.Repaint)
                {
                    if (guideReplay) DrawLiveBattle(width, height);
                    else DrawGameContent(width, height);
                }
                DrawGuide(width, height);
                return;
            }
            if (SystemMenuOpen)
            {
                // Draw only: no click, scroll or keyboard events reach the covered game.
                if (Event.current.type == EventType.Repaint)
                {
                    if (systemMenu == SystemMenu.Help) DrawLiveBattle(width, height);
                    else DrawGameContent(width, height);
                }
                DrawSystemMenu(width, height);
                return;
            }
            DrawGameContent(width, height);
        }

        private void DrawGameContent(int width, int height)
        {
            if (atTitle)
            {
                DrawEntryScreen(width, height);
                return;
            }
            if (showRecruit)
            {
                DrawRecruitScreen(width, height);
                return;
            }
            if (HasPendingIdleReward)
            {
                DrawIdleReturnPopup(width, height);
                return;
            }
            if (HasBossReview)
            {
                if (Event.current.type == EventType.Repaint) DrawLiveBattle(width, height);
                DrawBossReview(width, height);
                return;
            }
            if (editing)
            {
                DrawHeroes(width, height);
                return;
            }
            // While the result is open the underlying battlefield is visible but receives no input.
            if (!HasDungeonResult || Event.current.type == EventType.Repaint)
            {
                DrawLiveBattle(width, height);
            }
            if (HasDungeonResult) DrawDungeonResult(width, height);
        }

        private void BeginPlay()
        {
            try
            {
                OpenProgressSession();
                atTitle = false;
                if (battle.PendingSummons != null) { OpenMenu(); featurePage = 1; }
            }
            catch (System.Exception error)
            {
                loadError = "게임을 시작하지 못했습니다. 데이터·저장 파일과 다른 실행 창을 확인해 주세요.";
                Debug.LogException(error);
            }
        }

        private void DrawEntryScreen(int width, int height)
        {
            GUI.DrawTexture(new Rect(0, 0, width, height), FeatureTexture("KnightUI/ChapterHall"), ScaleMode.ScaleAndCrop);
            ManuscriptFade(new Rect(0, 0, width, height), new Color32(11, 18, 31, 55), new Color32(7, 14, 25, 220));
            DrawHallKnight(1, width * .51f, height * .87f, height * .54f, true);
            DrawHallKnight(2, width * .87f, height * .89f, height * .64f, true);
            DrawHallKnight(0, width * .72f, height * .96f, height * .78f, false);
            float left = width * .075f, top = height * .29f;
            Picture("KnightUI/OrderSeal", left, top - 52, 48, 48);
            CourtHeading(new Rect(left, top, width * .83f, 90), Application.productName, sideBattle ? 60 : 48, TextAnchor.MiddleLeft);
            CourtRule(new Rect(left, top + 91, width * .30f, 1), HallAmber);
            if (ArcadeTab(new Rect(left, top + 123, 205, 51), "게임 시작", true)) BeginPlay();
            if (loadError != null) GUI.Label(new Rect(left, top + 182, width * .45f, 80), loadError, small);
            if (GUI.Button(new Rect(width - 142, 16, 126, 38), "설정", button)) OpenSystemMenu();
        }

        private void DrawRecruitScreen(int width, int height)
        {
            var reward = battle.RecruitReward.Value;
            DrawKnightBackdrop(width, height);
            float mid = width * .5f;
            var art = portraits[reward.Hero];
            GUI.DrawTexture(new Rect(mid - 325, height * .1f, 320, height * .8f), art, ScaleMode.ScaleToFit);
            ArcadeText(new Rect(mid + 12, height * .33f, 260, 24),
                reward.IsNew ? "새 기사 영입" : "기사 레벨 상승", 16, HallAmber, TextAnchor.MiddleLeft);
            CourtHeading(new Rect(mid + 12, height * .38f, width * .46f, 67),
                battle.Heroes[reward.Hero], 39, TextAnchor.MiddleLeft);
            GUI.Label(new Rect(mid + 14, height * .52f, 232, 32), "레벨 " + reward.Level, label);
            if (ArcadeTab(new Rect(mid + 12, height * .63f, 194, 48), "계속", true)) showRecruit = false;
        }

        private void DrawLiveBattle(int width, int height)
        {
            float trayTop = sideBattle ? 420 : 696;
            DrawBattlefield(new Rect(0, 0, width, trayTop));
            DrawBattleUnits();
            DrawStageHeader(width);
            bool showRetry = battle.Farming && !battle.ActiveRepeat && battle.RepeatStage == 0 && battle.ActiveDungeon == null;
            ManuscriptFade(new Rect(0, trayTop - 62, width, 62), new Color32(9, 20, 30, 0), new Color32(9, 20, 30, 237));
            float statusWidth = battle.Finished ? Mathf.Min(490, width - (showRetry ? 172 : 24)) : 291;
            var statusArea = new Rect(16, trayTop - 39, statusWidth, 34);
            DrawBattleStatus(statusArea);
            if (battle.GearReward != null)
            {
                var loot = new Rect((width - 286) / 2f, statusArea.y - 31, 286, 30);
                DrawKnightPanel(loot);
                GUI.Label(loot, GearNameKorean(battle.GearReward.Name) + (battle.GearConverted ? "  /  골드로 전환" : "  /  획득"), HudCentered);
            }
            if (showRetry)
                DrawBattleRetry(new Rect(width - 148, trayTop - 43, 134, 40));
            ManuscriptFade(new Rect(0, trayTop, width, height - trayTop), new Color32(33, 51, 59, 255), new Color32(10, 23, 35, 255));
            CourtRule(new Rect(0, trayTop, width, 1), new Color32(168, 150, 112, 255));
            DrawPartyStrip(sideBattle ? new Rect(16, trayTop + 10, 480, 99) : new Rect(16, trayTop + 13, width - 32, 154));
            if (battle.ActiveDungeon != null)
            {
                DrawDungeonSpoils(sideBattle ? new Rect(516, trayTop + 26, 427, 63) : new Rect(14, 882, width - 28, 58));
                return;
            }
            DrawBattleDock(sideBattle ? new Rect(513, trayTop + 9, width - 524, 102) : new Rect(5, 876, width - 10, 78));
        }

        private void PrepareFieldDrawOrder()
        {
            fieldDrawOrder.Clear();
            for (int slot = 0; slot < battle.Units.Count; slot++)
            {
                var unit = battle.Units[slot];
                if (unit.Ally && unit.HeroIndex < 0) continue;
                var point = UnitScreenPoint(unit);
                fieldDrawOrder.Add((slot, point.x, point.y, unit.Alive));
            }
            // Fallen units are scenery. Living bodies paint back to front at their displayed positions.
            fieldDrawOrder.Sort((a, b) =>
            {
                int byAlive = a.alive.CompareTo(b.alive);
                if (byAlive != 0) return byAlive;
                int byHeight = a.y.CompareTo(b.y);
                return byHeight != 0 ? byHeight : a.slot.CompareTo(b.slot);
            });
        }

        private Vector2 UnitScreenPoint(TrialBattle.Unit unit)
        {
            var point = FieldPoint(unit.Position);
            if (sideBattle)
            {
                // Give each lane some visual depth without displacing another fighter.
                float homeY = 228f - (unit.Lane - 1) * battle.Data.LaneSpacing * 48f;
                point.y = Mathf.Lerp(point.y, homeY, .4f);
                return new Vector2(Mathf.Clamp(point.x, 73f, 887f), Mathf.Clamp(point.y, 135f, 313f));
            }
            return new Vector2(Mathf.Clamp(point.x, 59f, 481f), Mathf.Clamp(point.y, 195f, 596f));
        }

        private void DrawBattleUnits()
        {
            if (Event.current.type != EventType.Repaint) return;
            PrepareFieldDrawOrder();
            foreach (var entry in fieldDrawOrder)
                DrawCombatant(entry.slot, battle.Units[entry.slot], entry.x, entry.y);

            DrawSkillEffects();

            // Overlapping rigs and their weapon trails must be behind every victim's hit slash.
            float flashSize = sideBattle ? battle.Data.LandscapeSize : battle.Data.PortraitSize;
            foreach (var entry in fieldDrawOrder)
                DrawHitFlash(battle.Units[entry.slot], entry.x, entry.y, flashSize);

            foreach (var entry in fieldDrawOrder)
            {
                var unit = battle.Units[entry.slot];
                if (unit.Alive)
                    DrawUnitHealth(unit, entry.x, entry.y);
            }
            foreach (var entry in fieldDrawOrder)
            {
                var unit = battle.Units[entry.slot];
                float headOffset = sideBattle ? (unit.Ally || !string.IsNullOrEmpty(unit.Stats.Animation) ? 56 : 24) : 42;
                DrawDamage(unit, entry.x, entry.y - headOffset);
            }
            // Important next-attack information must stay above floating damage numbers.
            if (sideBattle)
                foreach (var entry in fieldDrawOrder)
                    DrawBossWarning(battle.Units[entry.slot], entry.x, entry.y);
        }

        private Rect CombatantArea(float x, float y) => sideBattle
            ? new Rect(x - 73, y - 67, 146, 134) : new Rect(x - 59, y - 61, 118, 118);

        private Vector2 FieldPoint(Vector2 position) => sideBattle
            ? new Vector2(480 + position.x * 192, 228 - position.y * 48)
            : new Vector2(270 + position.y * 110 + Mathf.Clamp(position.x * 75, -55, 55), 398 - position.x * 75);

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
            var portrait = unit.Ally ? portraits[unit.HeroIndex] : null;
            if (DrawSemiSD(area, unit)) { }
            // Missing new art must never silently switch back to the old character design.
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
                if (DrawRosterHeader(width, battle.Heroes[featureHero], "영웅 정보")) heroView = HeroView.Info;
                GUI.BeginGroup(new Rect(0, 64, width, height - 64));
                DrawInventory(true, width, height - 64);
                GUI.EndGroup();
                return;
            }
            if (heroView == HeroView.Info)
            {
                float dossierWidth = Mathf.Min(770, width - 40);
                float dossierHeight = height > width ? height - 150 : 410;
                var dossier = new Rect((width - dossierWidth) / 2, height > width ? 73 : (height - dossierHeight) / 2,
                    dossierWidth, dossierHeight);
                DrawKnightPanel(new Rect(dossier.x - 6, dossier.y - 4, dossier.width + 12, dossier.height + 6));
                if (DrawRosterHeader(width, "영웅 상세", "영웅")) heroView = HeroView.List;
                GUI.BeginGroup(dossier);
                DrawHeroDossier(dossierWidth, dossierHeight);
                GUI.EndGroup();
                GUI.enabled = battle.Owned(featureHero);
                if (ArcadeTab(new Rect(width / 2f - 235, height - 58, 220, 42), battle.Owned(featureHero) ? "편성 변경" : "영입 후 편성 가능", false))
                    BeginHeroPlacement();
                GUI.enabled = true;
                if (ArcadeTab(new Rect(width / 2f + 15, height - 58, 220, 42), "전투로 돌아가기", true))
                    editing = false;
                return;
            }
            if (DrawRosterHeader(width, "영웅 도감", heroesFromMenu ? "기사단 홀" : "전투"))
            {
                if (heroesFromMenu) OpenMenu(); else editing = false;
            }
            DrawRosterCount(new Rect(width - 282, 10, 140, 42));
            DrawKnightGallery(width, height);
            DrawHallDock(width, height, 0);
        }

        private void DrawHeroGrid(int width, float top, float availableHeight, bool placing)
        {
            DrawHeroGrid(new Rect(20, top, width - 40, availableHeight), width > 600 ? 5 : 3, placing);
        }

        private void DrawHeroGrid(Rect area, int columns, bool placing)
        {
            const int gap = 8;
            int rows = (portraits.Length + columns - 1) / columns;
            float cardWidth = (area.width - (columns - 1) * gap) / columns;
            float cardHeight = (area.height - (rows - 1) * gap) / rows;
            for (int hero = 0; hero < portraits.Length; hero++)
            {
                int row = hero / columns;
                int rowCount = Mathf.Min(columns, portraits.Length - row * columns);
                float x = area.x + ((columns - rowCount) * 0.5f + hero % columns) * (cardWidth + gap);
                float y = area.y + row * (cardHeight + gap);
                var card = new Rect(x, y, cardWidth, cardHeight);
                if (!placing && card.Contains(Event.current.mousePosition))
                    galleryHero = hero;
                if ((!placing || battle.Owned(hero)) && GUI.Button(card, GUIContent.none, GUIStyle.none))
                {
                    if (placing) PlaceHero(hero, selectedSlot);
                    else ShowKnightDossier(hero);
                }
                int deployedSlot = -1;
                for (int slot = 0; slot < draft.Length; slot++)
                    if ((placing ? draft[slot] : battle.HeroAt(slot)) == hero)
                        deployedSlot = slot;
                DrawRosterCard(card, hero, deployedSlot, placing && selectedSlot >= 0 && selectedSlot == deployedSlot);
            }
        }

        private void BeginHeroPlacement()
        {
            if (!battle.Owned(featureHero)) return;
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
            if (!battle.Owned(hero) || slot < 0 || slot >= draft.Length) return;
            int occupied = System.Array.IndexOf(draft, hero);
            if (occupied >= 0) draft[occupied] = draft[slot];
            draft[slot] = hero;
        }

        private void DrawHeroSlots(int width, int height)
        {
            GUI.Label(new Rect(20, 12, width - 40, 42), "편성 변경", title);
            GUI.Label(new Rect(20, 65, width - 40, 40), "배치할 영웅: " + battle.Heroes[featureHero] + "   ·   중앙 전열은 주는 피해·받는 피해 +20%", label);
            var board = new Rect(width * .18f, 148, width * .64f, height - 268);
            for (int i = 0; i < draft.Length; i++)
            {
                var card = FormationSlotRect(board, i);
                if (GUI.Button(card, GUIContent.none, GUIStyle.none)) SelectHeroSlot(i);
                DrawRosterCard(card, draft[i], i, selectedSlot == i);
            }
            DrawSynergyLine(20, 108, "현재", battle.ActiveSynergy);
            if (selectedSlot >= 0) DrawSynergyLine(20, board.yMax + 7, "다음 전투", battle.FormationSynergy(draft));
            if (GUI.Button(new Rect(20, height - 60, 120, 42), "취소", button)) heroView = HeroView.Info;
            GUI.enabled = selectedSlot >= 0 && battle.HeroAt(selectedSlot) != featureHero;
            if (GUI.Button(new Rect(width - 245, height - 60, 225, 42), "다음 전투에 적용", button))
                ConfirmFormation();
            GUI.enabled = true;
        }
        private void ConfirmFormation()
        {
            battle.ApplyFormation(draft);
            CheckpointProgress();
            editing = false;
        }

        private void DrawHitFlash(TrialBattle.Unit unit, float x, float y, float size)
        {
            if (unit.LastDamageWasBleed || unit.LastDamage <= 0 || Event.current.type != EventType.Repaint) return;
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
            if (!showDamageNumbers || age >= 0.7f || unit.LastDamage <= 0) return;
            bool combo = unit.DamageComboCaster != null && BattleAnimationTime - unit.DamageComboStart < 1.7f;
            if (combo) { var anchor = FieldPoint(unit.DamageComboPosition); x = anchor.x; headY = anchor.y; }
            float top = Mathf.Max(65, headY - 45 - (combo ? 0 : age * 26));
            Rect bounds = new Rect(x - 45, top, 90, 30);
            string number = "-" + (combo ? unit.DamageComboTotal : unit.LastDamage).ToString("0.##");
            float opacity = Mathf.Clamp01((0.7f - age) * 5);
            damage.normal.textColor = new Color(0.04f, 0.06f, 0.09f, opacity);
            GUI.Label(new Rect(bounds.x + 1, bounds.y + 2, bounds.width, bounds.height), number, damage);
            var tone = unit.Ally ? new Color32(251, 174, 154, 255) : new Color32(255, 225, 169, 255);
            damage.normal.textColor = combo && unit.DamageComboFinal ? new Color(.85f, .16f, .06f, opacity) : new Color(tone.r / 255f, tone.g / 255f, tone.b / 255f, opacity);
            GUI.Label(bounds, number, damage);
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
