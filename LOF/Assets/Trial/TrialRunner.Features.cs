using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace AFKTrial
{
    public sealed partial class TrialRunner
    {
        private readonly Dictionary<string, Texture2D> featureTextures = new Dictionary<string, Texture2D>();
        private static readonly string[] FeatureTabs = { "Formation", "Summon", "Gear", "Dungeons", "Idle" };
        private static readonly string[] RoleIcons = { "AttackRole", "DefenseRole", "SupportRole" };

        private static readonly string[] Equipment = { "MightSigil", "WardSigil", "LifeSigil" };
        private TrialData.GearSlot gearSlot;
        private TrialBattle.Item pendingSale;
        private int gearFilter = -1, inventoryPage;
        private TrialBattle.Item selectedGear;
        private readonly List<TrialBattle.Item> visibleGear = new List<TrialBattle.Item>();
        private static readonly Color InventoryGold = new Color(0.77f, 0.54f, 0.25f);
        private static readonly Color InventoryPanel = new Color(0.065f, 0.103f, 0.15f);
        private static readonly string[] GearFilters = { "All", "Weapons", "Armor", "Necklaces" };
        private int featurePage = -1, featureHero;
        private TrialData.DungeonKind? selectedDungeon;
        private int dungeonDifficulty;
        private int selectedSkill = -1;
        private Vector2 skillScroll;
        private TrialBattle.Pull[] lastPulls;
        private float animationStart = float.NegativeInfinity;

        private Texture2D FeatureTexture(string key)
        {
            if (!featureTextures.TryGetValue(key, out var texture))
            {
                texture = Resources.Load<Texture2D>(key);
                featureTextures.Add(key, texture);
            }
            return texture;
        }

        private void Picture(string key, float x, float y, float w, float h)
        {
            var texture = FeatureTexture(key);
            if (texture) GUI.DrawTexture(new Rect(x, y, w, h), texture, ScaleMode.ScaleToFit);
        }

        private void Sheet(string key, Rect rect)
        {
            int frame = Mathf.FloorToInt((Time.unscaledTime - animationStart) * 12);
            if (frame < 0 || frame >= 8) return;
            var sheet = FeatureTexture(key);
            if (sheet) GUI.DrawTextureWithTexCoords(rect, sheet, new Rect(frame / 8f, 0, 1f / 8, 1));
        }

        private void OpenMenu()
        {
            choosingRepeat = false;
            pendingSale = null;
            editing = true;
            featurePage = -1;
            heroView = HeroView.Menu;
            animationStart = float.NegativeInfinity;
        }

        private static readonly string[] PageTitles =
            { "Formation", "Oath chamber", "Equipment inventory", "Dungeon gates", "Idle rewards" };
        private static readonly Color HallInk = new Color32(19, 25, 40, 246);
        private static readonly Color HallLine = new Color32(123, 111, 88, 255);
        private static readonly Color HallAmber = new Color32(228, 198, 143, 255);
        private static readonly Color HallLight = new Color32(249, 243, 224, 255);
        private GUIStyle summonName, summonLevel;

        // Thin engraved silhouettes share one 32-unit design grid and retain their edges at UI scale.
        private static readonly int[][][] CourtEmblemPaths =
        {
            new[] { new[] { 5,17, 6,10, 10,6, 16,3, 22,6, 26,11, 27,17, 23,18, 22,24, 16,29, 10,24, 9,18, 5,17 },
                new[] { 5,15, 16,18, 27,15 }, new[] { 16,18, 16,26 }, new[] { 16,5, 16,13 } },
            new[] { new[] { 7,28, 7,4, 23,4, 26,8, 23,11, 24,18, 15,15, 7,17 },
                new[] { 7,8, 16,8 }, new[] { 4,28, 10,28 }, new[] { 11,20, 18,18, 27,21, 26,14 } },
            new[] { new[] { 15,3, 18,12, 27,15, 18,18, 15,28, 12,18, 3,15, 12,12, 15,3 },
                new[] { 24,3, 25,6, 28,7, 25,8, 24,11, 23,8, 21,7, 23,6, 24,3 } },
            new[] { new[] { 4,9, 10,4, 13,5, 14,8, 18,8, 19,5, 22,4, 28,9, 26,14, 23,13, 23,23, 16,28, 9,23, 9,13, 6,14, 4,9 },
                new[] { 10,13, 16,16, 22,13 }, new[] { 16,16, 16,23 }, new[] { 10,20, 16,23, 22,20 } },
            new[] { new[] { 4,27, 4,16, 6,10, 10,6, 16,3, 22,6, 26,10, 28,16, 28,27, 23,27, 23,16, 21,12, 16,9, 11,12, 9,16, 9,27, 4,27 },
                new[] { 16,4, 16,8 }, new[] { 7,10, 11,13 }, new[] { 25,10, 21,13 }, new[] { 12,27, 20,27 } },
            new[] { new[] { 6,4, 26,4 }, new[] { 6,28, 26,28 }, new[] { 9,5, 9,10, 13,14, 17,16, 23,22, 23,27 },
                new[] { 23,5, 23,10, 19,14, 15,16, 9,22, 9,27 }, new[] { 12,9, 20,9 }, new[] { 12,26, 16,22, 20,26 } },
            new[] { new[] { 4,27, 4,10, 8,10, 8,13, 12,13, 12,5, 15,5, 15,8, 17,8, 17,5, 20,5, 20,13, 24,13, 24,10, 28,10, 28,27, 4,27 },
                new[] { 13,27, 13,21, 16,18, 19,21, 19,27 }, new[] { 7,17, 7,20 }, new[] { 25,17, 25,20 } }
        };
        private readonly Texture2D[] courtEmblems = new Texture2D[CourtEmblemPaths.Length];

        private void DrawCourtEmblem(Rect area, int index, Color ink)
        {
            if (Event.current.type != EventType.Repaint) return;
            if (courtEmblems[index] == null)
            {
                const int resolution = 64;
                var pixels = new Color32[resolution * resolution];
                foreach (int[] path in CourtEmblemPaths[index])
                    for (int node = 2; node < path.Length; node += 2)
                    {
                        Vector2 a = new Vector2(path[node - 2], path[node - 1]) * 2;
                        Vector2 b = new Vector2(path[node], path[node + 1]) * 2;
                        Vector2 direction = b - a;
                        int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, b.x)) - 2);
                        int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, b.y)) - 2);
                        int x1 = Mathf.Min(resolution - 1, Mathf.CeilToInt(Mathf.Max(a.x, b.x)) + 2);
                        int y1 = Mathf.Min(resolution - 1, Mathf.CeilToInt(Mathf.Max(a.y, b.y)) + 2);
                        for (int y = y0; y <= y1; y++)
                            for (int x = x0; x <= x1; x++)
                            {
                                Vector2 offset = new Vector2(x + 0.5f, y + 0.5f) - a;
                                float t = Mathf.Clamp01(Vector2.Dot(offset, direction) / direction.sqrMagnitude);
                                float coverage = Mathf.Clamp01(2.05f - (offset - t * direction).magnitude);
                                int p = (resolution - y - 1) * resolution + x;
                                byte alpha = (byte)Mathf.RoundToInt(coverage * 255);
                                if (alpha > pixels[p].a) pixels[p] = new Color32(255, 255, 255, alpha);
                            }
                    }
                var glyph = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false)
                {
                    hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear
                };
                glyph.SetPixels32(pixels);
                glyph.Apply(false, true);
                skinPlates.Add(glyph);
                courtEmblems[index] = glyph;
            }
            var tint = GUI.color;
            GUI.color = ink;
            GUI.DrawTexture(area, courtEmblems[index], ScaleMode.ScaleToFit);
            GUI.color = tint;
        }

        private int OwnedCount()
        {
            int count = 0;
            for (int i = 0; i < portraits.Length; i++)
                if (battle.Owned(i)) count++;
            return count;
        }

        private void DrawRosterCount(Rect area)
        {
            GUI.Label(area, OwnedCount() + " / " + portraits.Length + " owned", HudCentered);
        }

        private void DrawMenu(int width, int height)
        {
            if (choosingRepeat) { DrawRepeatStages(width, height); return; }
            bool portrait = height > width;
            float dockTop = height - HallDockHeight(portrait);
            GUI.DrawTexture(new Rect(0, 0, width, dockTop), FeatureTexture("KnightUI/ChapterHall"), ScaleMode.ScaleAndCrop);
            float floor = dockTop - (portrait ? 68 : 6);
            float center = width * 0.5f;
            float rearHeight = portrait ? 395 : 300;
            DrawHallKnight(1, width * (portrait ? 0.10f : 0.26f), floor - (portrait ? 110 : 26), rearHeight, true);
            DrawHallKnight(2, width * 0.76f, floor - (portrait ? 88 : 26), rearHeight, true);
            DrawHallKnight(0, center, floor, portrait ? 540 : 405, false);

            ManuscriptFade(new Rect(0, 0, width, portrait ? 128 : 113),
                new Color32(10, 15, 28, 238), new Color32(10, 15, 28, 0));
            var heading = new Rect(18, 13, portrait ? 250 : 317, 67);
            Picture("KnightUI/OrderSeal", heading.x, heading.y + 3, 47, 47);
            ArcadeText(new Rect(74, 12, 200, 18), battle.StageName(battle.ActiveStage), 12, HallAmber, TextAnchor.MiddleLeft);
            CourtHeading(new Rect(72, 28, 233, 44), "Knight Hall", 34, TextAnchor.MiddleLeft);
            var wallet = new Rect(width - (portrait ? 205 : 240), 13, portrait ? 190 : 222, 37);
            DrawKnightPanel(wallet);
            DrawWallet(new Rect(wallet.x + 9, wallet.y + 2, wallet.width - 14, 33));

            var hero = battle.Data.Heroes[battle.HeroAt(0)];
            ManuscriptFade(new Rect(0, dockTop - 150, width, 150),
                new Color32(12, 16, 27, 0), new Color32(12, 16, 27, 247));
            var nameplate = new Rect(21, dockTop - 86, portrait ? 288 : 337, 78);
            CourtRule(new Rect(nameplate.x, nameplate.y + 3, 63, 1), HallAmber);
            ArcadeText(new Rect(nameplate.x, nameplate.y + 10, nameplate.width, 19),
                hero.Role.ToString().ToUpperInvariant() + "    /    LV. " + battle.HeroLevel(battle.HeroAt(0)), 12, HallAmber, TextAnchor.MiddleLeft);
            CourtHeading(new Rect(nameplate.x - 1, nameplate.y + 28, nameplate.width, 47), hero.Name,
                portrait ? 33 : 39, TextAnchor.MiddleLeft);
            if (GUI.Button(nameplate, GUIContent.none, GUIStyle.none))
            {
                featureHero = battle.HeroAt(0);
                selectedSkill = -1;
                heroesFromMenu = true;
                heroView = HeroView.Info;
            }
            var resume = new Rect(width - 190, dockTop - 64, 169, 47);
            if (GUI.Button(new Rect(resume.x, resume.y - 48, resume.width, 38),
                battle.RepeatStage == 0 ? "Repeat stage" : "Repeat stage  /  ON", button)) OpenRepeatStages();
            if (GUI.Button(resume, "Back to battle   ›", knightSelectedButton))
                editing = false;
            DrawHallDock(width, height, -1);
        }

        private void DrawHallKnight(int slot, float center, float baseline, float height, bool behind)
        {
            var art = portraits[battle.HeroAt(slot)];
            float width = height * art.width / art.height;
            var tint = GUI.color;
            if (behind) GUI.color = new Color32(205, 214, 222, 255);
            GUI.DrawTexture(new Rect(center - width * 0.5f, baseline - height, width, height), art);
            GUI.color = tint;
        }

        private static int HallDockHeight(bool portrait) => portrait ? 84 : 66;

        private void DrawHallDock(int width, int height, int active)
        {
            bool portrait = height > width;
            float h = HallDockHeight(portrait);
            float top = height - h;
            ManuscriptFade(new Rect(0, top, width, h), new Color32(36, 38, 52, 255), new Color32(12, 18, 30, 255));
            CourtRule(new Rect(0, top, width, 1), HallLine);
            int links = FeatureTabs.Length + 1;
            float step = width / (float)links;
            for (int i = 0; i < links; i++)
            {
                var cell = new Rect(step * i, top, step, h);
                bool selected = active == i;
                bool pressed = GUI.Button(cell, GUIContent.none, GUIStyle.none);
                bool hover = GUI.enabled && cell.Contains(Event.current.mousePosition);
                if (selected || hover)
                    ManuscriptFade(cell, new Color32(139, 119, 77, 90), new Color32(76, 66, 57, 0));
                if (selected)
                {
                    CourtRule(new Rect(cell.center.x - 30, top + 1, 60, 1), HallAmber);
                }
                float size = portrait ? 33 : 29;
                DrawCourtEmblem(new Rect(cell.center.x - size / 2, top + (portrait ? 12 : 7), size, size),
                    i, selected || hover ? new Color32(255, 226, 174, 255) : new Color32(202, 187, 155, 255));
                string caption = i == 0 ? "Heroes" : FeatureTabs[i - 1];
                var captionRect = new Rect(cell.x, top + (portrait ? 48 : 37), step, 22);
                ArcadeText(captionRect, caption, 14, selected ? HallLight : new Color32(215, 213, 208, 255), TextAnchor.MiddleCenter);
                if (!pressed || selected) continue;
                pendingSale = null;
                if (i == 0) { OpenHeroes(); heroesFromMenu = true; }
                else
                {
                    heroView = HeroView.Menu;
                    OpenFeature(i - 1);
                    animationStart = float.NegativeInfinity;
                }
            }
        }
        private void DrawSynergyLine(float x, float y, string heading, TrialData.Role role)
        {
            string effect;
            string icon;
            switch (role)
            {
                case TrialData.Role.Attack: icon = "Onslaught"; effect = "ATK +10%"; break;
                case TrialData.Role.Defense: icon = "Guard"; effect = "Damage taken -10%"; break;
                case TrialData.Role.Support: icon = "Empowerment"; effect = "ATK and max HP +5%"; break;
                default:
                    GUI.Label(new Rect(x, y, 490, 34), heading + " synergy: None", label);
                    return;
            }
            Picture("SynergyUI/" + icon, x, y, 32, 32);
            GUI.Label(new Rect(x + 40, y, 460, 34), heading + ": " + icon + "  /  " + effect, small);
        }
        private void DrawFeatureScreen(int width, int height)
        {
            bool portrait = height > width;
            DrawKnightBackdrop(width, height);
            int contentBottom = height - HallDockHeight(portrait);
            const int headerHeight = 52;
            ManuscriptFade(new Rect(0, 0, width, headerHeight), new Color32(40, 40, 50, 255), HallInk);
            CourtRule(new Rect(0, headerHeight - 1, width, 1), HallLine);
            var back = new Rect(10, 4, 118, 43);
            ArcadeText(back, heroView == HeroView.Info ? "‹   Hero info" : "‹   Hall", 16, HallAmber, TextAnchor.MiddleCenter);
            if (GUI.Button(back, GUIContent.none, GUIStyle.none))
            {
                featurePage = -1;
                return;
            }
            CourtHeading(new Rect(130, 1, width - 260, 47), PageTitles[featurePage],
                portrait ? 26 : 29, TextAnchor.MiddleCenter);
            if (!portrait) DrawWallet(new Rect(width - 235, 5, 220, 42));
            if (featurePage == 0) DrawPartyPage(width, contentBottom);
            else
            {
                float panelHeight = contentBottom - headerHeight;
                GUI.BeginGroup(new Rect(0, headerHeight, width, panelHeight));
                switch (featurePage)
                {
                    case 1: DrawSummonPage(width, panelHeight, portrait); break;
                    case 2: DrawInventory(false, width, panelHeight); break;
                    case 3: DrawDungeonPage(width, panelHeight, portrait); break;
                    case 4: DrawIdlePage(width, panelHeight, portrait); break;
                }
                GUI.EndGroup();
            }
            DrawHallDock(width, height, featurePage + 1);
        }

        private void DrawHeroDossier(float width, float height)
        {
            bool tall = height > width;
            var hero = battle.Data.Heroes[featureHero];
            int level = battle.HeroLevel(featureHero);
            float growthTop = height - (tall ? 109 : 90);
            float listTop = tall ? growthTop - 213 : 0;
            float column = tall ? (width - 41) * 0.5f : width * 0.257f;
            float skillX = width - 14 - column;
            var backdrop = new Rect(0, 0, width, tall ? listTop : growthTop);
            GUI.DrawTexture(backdrop, FeatureTexture("KnightUI/ChapterHall"), ScaleMode.ScaleAndCrop);
            Fill(backdrop, new Color32(15, 21, 33, 91));
            var illustration = tall ? new Rect(0, 0, width, listTop) : new Rect(column + 16, 0, width - 2 * column - 32, growthTop);
            DrawKnightPortrait(illustration, featureHero, Color.white);
            ManuscriptFade(new Rect(0, backdrop.yMax - 180, width, 128),
                new Color32(15, 21, 33, 0), new Color32(15, 21, 33, 222));
            ManuscriptFade(new Rect(0, backdrop.yMax - 52, width, 52), new Color32(15, 21, 33, 222), HallInk);
            var info = new Rect(illustration.x + 8, backdrop.yMax - 84, illustration.width - 16, 80);
            Fill(new Rect(info.center.x - 61, info.y + 1, 122, 18), new Color32(13, 19, 32, 197));
            ArcadeText(new Rect(info.x, info.y, info.width, 20),
                hero.Role.ToString().ToUpperInvariant() + "    /    LV. " + level, 12, HallAmber, TextAnchor.MiddleCenter);
            CourtHeading(new Rect(info.x, info.y + 18, info.width, 36), hero.Name, tall ? 30 : 28, TextAnchor.MiddleCenter);
            var stats = battle.HeroStats(featureHero);
            ArcadeText(new Rect(info.x, info.y + 55, info.width, 18),
                $"HP {stats.hp:0.##}     ATK {stats.attack:0.##}     DEF {stats.armor}", 14, HallLight, TextAnchor.MiddleCenter);

            if (tall) Fill(new Rect(0, listTop, width, growthTop - listTop), HallInk);
            if (!tall)
            {
                var scrim = new Color32(13, 20, 32, 222);
                Fill(new Rect(0, 0, column + 20, growthTop), scrim);
                Fill(new Rect(skillX - 6, 0, column + 20, growthTop), scrim);
            }
            float rowHeight = tall ? 55 : 67;
            float rowTop = listTop + (tall ? 38 : 49);
            CourtHeading(new Rect(skillX + 3, listTop + 4, column, 33), "Skills", 21, TextAnchor.MiddleLeft);
            string prefix = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(hero.Id.Replace('_', ' ')).Replace(" ", "");
            for (int i = 0; i < 3; i++)
            {
                var row = new Rect(skillX, rowTop + i * rowHeight, column, rowHeight);
                if (DrawDossierRow(row, "SkillUI/" + prefix + "Skill" + (i + 1), "Lv. " + battle.SkillLevel(featureHero, i),
                    battle.Data.Skills[featureHero, i].Name, i == selectedSkill))
                {
                    selectedSkill = i;
                    skillScroll = Vector2.zero;
                }
            }
            if (selectedSkill >= 0)
            {
                float popupY = tall ? Mathf.Max(0, (backdrop.height - 330) / 2) : (height - 330) / 2;
                GUI.BeginGroup(new Rect(tall ? (width - 340) / 2 : (skillX - 340) / 2, popupY, 340, 330));
                DrawSkillDetails();
                GUI.EndGroup();
                return;
            }

            CourtHeading(new Rect(17, listTop + 4, column, 33), "Equipment", 21, TextAnchor.MiddleLeft);
            for (int i = 0; i < Equipment.Length; i++)
            {
                var slot = (TrialData.GearSlot)i;
                var item = battle.Equipped(featureHero, slot);
                var row = new Rect(14, rowTop + i * rowHeight, column, rowHeight);
                if (DrawDossierRow(row, item == null ? "EquipmentUI/" + Equipment[i] : GearIcon(item),
                    slot.ToString(), item == null ? "Empty" : $"{item.Gear.Name} +{item.Enhancement}", false))
                {
                    gearSlot = slot;
                    inventoryPage = -1;
                    selectedGear = item;
                    pendingSale = null;
                    heroView = HeroView.GearPicker;
                }
            }
            DrawDossierGrowth(width, growthTop, height - growthTop, level);
        }

        private bool DrawDossierRow(Rect row, string icon, string detail, string name, bool selected)
        {
            if (selected || row.Contains(Event.current.mousePosition))
                Fill(row, new Color32(121, 104, 73, 65));
            Fill(new Rect(row.x, row.y, row.width, 1), new Color32(112, 105, 90, 155));
            Picture(icon, row.x + 3, row.center.y - 17, 34, 34);
            ArcadeText(new Rect(row.x + 45, row.center.y - 21, row.width - 49, 17), detail, 12, HallAmber, TextAnchor.MiddleLeft);
            ArcadeText(new Rect(row.x + 45, row.center.y - 3, row.width - 49, 23), name, 15, HallLight, TextAnchor.MiddleLeft);
            return GUI.Button(row, GUIContent.none, GUIStyle.none);
        }

        private void DrawDossierGrowth(float width, float top, float height, int level)
        {
            var cost = battle.HeroUpgradeCost(featureHero);
            ManuscriptFade(new Rect(0, top, width, height), new Color32(39, 39, 49, 255), HallInk);
            CourtRule(new Rect(9, top, width - 18, 1), HallLine);
            float actionWidth = width > 600 ? 178 : 149;
            float actionX = width - 14 - actionWidth;
            float cell = (actionX - 28) * 0.5f;
            DrawLevelResource(new Rect(15, top + 8, cell, height - 16), goldCoin, "Gold", cost.gold, battle.Gold);
            DrawLevelResource(new Rect(15 + cell, top + 8, cell, height - 16), experienceCrystal, "Experience", cost.experience, battle.Experience);
            GUI.enabled = battle.CanUpgradeHero(featureHero);
            if (GUI.Button(new Rect(actionX, top + 13, actionWidth, 40), level == int.MaxValue ? "Maximum level" : $"Level up to {level + 1}", knightSelectedButton))
                TryPurchaseHeroLevel(featureHero);
            GUI.enabled = true;
            GUI.Label(new Rect(actionX - 5, top + 54, actionWidth + 10, height - 55),
                string.IsNullOrEmpty(idleSaveError) ? "Applies next battle" : idleSaveError, HudCentered);
        }

        private void DrawLevelResource(Rect area, Texture2D icon, string name, long cost, int owned)
        {
            GUI.DrawTexture(new Rect(area.x, area.y + 4, 24, 24), icon, ScaleMode.ScaleToFit);
            ArcadeText(new Rect(area.x + 28, area.y + 2, area.width - 29, 25), name, 15, HallLight, TextAnchor.MiddleLeft);
            ArcadeText(new Rect(area.x + 2, area.y + 29, area.width - 2, 20), $"Cost   {cost:N0}", 15,
                owned < cost ? new Color32(239, 169, 144, 255) : HallAmber, TextAnchor.MiddleLeft);
            ArcadeText(new Rect(area.x + 2, area.y + 49, area.width - 2, 19), $"Owned   {owned:N0}", 13, HallLight, TextAnchor.MiddleLeft);
        }
        private void DrawSkillDetails()
        {
            int slot = selectedSkill;
            var skill = battle.Data.Skills[featureHero, slot];
            int level = battle.SkillLevel(featureHero, slot);
            var panel = new Rect(2, 10, 335, 319);
            InventoryBox(panel, InventoryPanel, InventoryGold);
            GUI.Label(new Rect(12, 21, 274, 44), skill.Name, label);
            if (GUI.Button(new Rect(297, 18, 30, 30), "X")) selectedSkill = -1;
            string description = string.IsNullOrWhiteSpace(skill.Description) ? "Skill details pending." : skill.Description;
            description = description.Replace("n%", level + "%").Replace(" n ", " " + level + " ")
                .Replace(" n)", " " + level + ")");
            var content = new GUIContent(description);
            float textHeight = Mathf.Max(99, small.CalcHeight(content, 289));
            skillScroll = GUI.BeginScrollView(new Rect(12, 68, 312, 100), skillScroll, new Rect(0, 0, 289, textHeight));
            GUI.Label(new Rect(0, 0, 289, textHeight), content, small);
            GUI.EndScrollView();
            GUI.Label(new Rect(12, 175, 215, 30), $"Skill level {level} / 5", label);
            int cost = battle.SkillCost(featureHero, slot);
            string status = level >= 5 ? "Maximum level" : level >= battle.HeroLevel(featureHero)
                ? $"Requires hero Lv.{level + 1}" : $"Gold {battle.Gold} / {cost}";
            GUI.Label(new Rect(12, 210, 215, 32), status, small);
            GUI.enabled = battle.CanTrain(featureHero, slot);
            if (GUI.Button(new Rect(12, 254, 214, 48), $"Upgrade  /  {cost} G", button)) battle.Train(featureHero, slot);
            GUI.enabled = true;
        }
        private bool showSynergyEffects;
        private void OpenFeature(int page)
        {
            featurePage = page;
            showSynergyEffects = false;
            selectedDungeon = null;
            if (page != 0) return;
            for (int i = 0; i < draft.Length; i++) draft[i] = battle.HeroAt(i);
            selectedSlot = 0;
        }

        private void DrawPartyPage(int width, int height)
        {
            if (showSynergyEffects)
            {
                GUI.Label(new Rect(20, 65, 300, 42), "Synergy effects", title);
                if (GUI.Button(new Rect(width - 156, 65, 136, 42), "Back to slots", button)) showSynergyEffects = false;
                var preview = battle.FormationSynergy(draft);
                for (int i = 0; i < RoleIcons.Length; i++)
                {
                    var role = (TrialData.Role)(i + 1);
                    float y = 126 + i * 99;
                    ArcadePanel(new Rect(16, y - 6, width - 32, 89), HallInk, preview == role ? HallAmber : HallLine);
                    Picture("RoleUI/" + RoleIcons[i], 26, y + 11, 39, 39);
                    string state = battle.ActiveSynergy == role ? "Active now" : "Inactive";
                    GUI.Label(new Rect(72, y, width - 90, 34), $"2 {role} heroes  /  {state}", label);
                    DrawSynergyLine(72, y + 36, preview == role ? "Selected team" : "Effect", role);
                }
                return;
            }
            if (GUI.Button(new Rect(width - 168, 56, 148, 38), "Synergy effects")) showSynergyEffects = true;
            GUI.Label(new Rect(20, 59, width - 200, 32), "Choose a slot, then a hero.", small);
            float slotWidth = (width - 52) / 3f;
            bool changed = false;
            for (int i = 0; i < draft.Length; i++)
            {
                var rect = new Rect(20 + i * (slotWidth + 6), 101, slotWidth, 51);
                if (GUI.Button(rect, GUIContent.none, GUIStyle.none)) selectedSlot = i;
                bool current = i == selectedSlot;
                ManuscriptFade(rect, current ? new Color32(69, 63, 56, 239) : new Color32(43, 44, 58, 215), HallInk);
                CourtRule(new Rect(rect.x + 1, rect.yMax - 1, rect.width - 2, 1), current ? HallAmber : HallLine);
                float textX = rect.x + (width > 540 ? 73 : 31);
                if (width > 540)
                    DrawKnightPortrait(new Rect(rect.x + 1, rect.y + 2, 61, rect.height - 4), draft[i], Color.white);
                GUI.DrawTexture(new Rect(rect.x + 3, rect.y + (width > 540 ? 27 : 13), 23, 23), deploymentBadges[i], ScaleMode.ScaleToFit);
                ArcadeText(new Rect(textX, rect.y + 5, rect.xMax - textX - 3, 16),
                    battle.Data.Heroes[draft[i]].Role.ToString().ToUpperInvariant(), 11, HallAmber, TextAnchor.MiddleLeft);
                ArcadeText(new Rect(textX, rect.y + 20, rect.xMax - textX - 3, 26),
                    battle.Heroes[draft[i]], width > 540 ? 17 : 14, HallLight, TextAnchor.MiddleLeft);
                changed |= draft[i] != battle.HeroAt(i);
            }
            bool wide = width > 540;
            float footerHeight = wide ? 68 : 100;
            DrawHeroGrid(width, 160, height - 160 - footerHeight, true);
            ManuscriptFade(new Rect(0, height - footerHeight, width, footerHeight),
                new Color32(31, 35, 50, 240), HallInk);
            DrawSynergyLine(20, height - footerHeight + 2, "Active", battle.ActiveSynergy);
            if (changed || battle.FormationSynergy(draft) != battle.ActiveSynergy) DrawSynergyLine(20, height - footerHeight + 31, "Next battle", battle.FormationSynergy(draft));
            GUI.enabled = changed;
            if (GUI.Button(new Rect(width - 245, height - (wide ? 54 : 42), 225, 41), "Apply next battle", knightSelectedButton))
                ConfirmFormation();
            GUI.enabled = true;
        }
        private void DrawSummonPage(float width, float height, bool portraitLayout)
        {
            var art = new Rect(0, 0, width, height - (portraitLayout ? 98 : 68));
            var chamber = FeatureTexture("KnightUI/OathChamber");
            float cropWidth = Mathf.Min(1, art.width / art.height * chamber.height / chamber.width);
            float cropHeight = Mathf.Min(1, art.height / art.width * chamber.width / chamber.height);
            GUI.DrawTextureWithTexCoords(art, chamber, new Rect(1 - cropWidth, (1 - cropHeight) / 2, cropWidth, cropHeight));
            ManuscriptFade(new Rect(0, art.yMax, width, height - art.yMax), new Color32(35, 35, 49, 255), HallInk);
            CourtRule(new Rect(0, art.yMax, width, 1), HallLine);
            if (lastPulls == null)
            {
                var caption = new Rect(0, art.yMax - 102, portraitLayout ? width : width * 0.52f, 102);
                ManuscriptFade(new Rect(0, art.yMax - 148, width, 148), new Color32(14, 17, 29, 0), HallInk);
                CourtRule(new Rect(23, caption.y + 8, 69, 1), HallAmber);
                CourtHeading(new Rect(21, caption.y + 13, caption.width - 34, 43),
                    "Knight’s Oath", 34, TextAnchor.MiddleLeft);
                GUI.Label(new Rect(23, caption.y + 53, caption.width - 40, 45),
                    "One ticket calls one hero. A duplicate grants that hero one level.", small);
            }
            else
            {
                Fill(art, new Color(0.025f, 0.05f, 0.08f, 0.74f));
                bool single = lastPulls.Length == 1;
                int columns = portraitLayout ? 3 : 5;
                float columnWidth = (width - 30) / columns;
                float rowHeight = (art.height - 10) / (portraitLayout ? 4 : 2);
                float singleHeight = Mathf.Min(art.height * 0.87f, width * 0.79f);
                for (int i = 0; i < lastPulls.Length; i++)
                {
                    var pull = lastPulls[i];
                    int inRow = Mathf.Min(columns, lastPulls.Length - i / columns * columns);
                    float left = (width - inRow * columnWidth) / 2;
                    var card = single ? new Rect((width - singleHeight * 0.75f) / 2, (art.height - singleHeight) / 2, singleHeight * 0.75f, singleHeight)
                        : new Rect(left + i % columns * columnWidth, 7 + i / columns * rowHeight,
                            columnWidth - 4, rowHeight - 5);
                    GUI.DrawTexture(card, FeatureTexture("SummonUI/SummonResultCard"));
                    var footer = new Rect(card.x + 8, card.yMax - 56, card.width - 16, 45);
                    var portrait = new Rect(card.x + 6, card.y + 8, card.width - 12, footer.y - card.y - 9);
                    GUI.DrawTexture(portrait, portraits[pull.Hero], ScaleMode.ScaleToFit);
                    Sheet("SummonUI/AcquisitionSparkle", portrait);
                    Fill(footer, InventoryPanel);
                    Fill(new Rect(footer.x, footer.y, footer.width, 1), InventoryGold);
                    GUI.Label(new Rect(footer.x, footer.y + 3, footer.width, 21), battle.Heroes[pull.Hero], summonName);
                    GUI.Label(new Rect(footer.x, footer.y + 24, footer.width, 17),
                        pull.IsNew ? "New!" : "+1   Lv." + pull.Level, summonLevel);
                }
            }
            float ticketY = art.yMax + (portraitLayout ? 5 : 17);
            Picture("SummonUI/SummonTicket", 18, ticketY, 30, 30);
            GUI.Label(new Rect(54, ticketY - 2, 190, 34), "Tickets   " + battle.Tickets, label);
            float buttonWidth = portraitLayout ? (width - 48) / 2 : 174;
            float buttonX = width - 18 - 2 * buttonWidth - 12;
            float buttonY = art.yMax + (portraitLayout ? 40 : 11);
            DrawPullButton(new Rect(buttonX, buttonY, buttonWidth, 45), 1);
            DrawPullButton(new Rect(buttonX + 12 + buttonWidth, buttonY, buttonWidth, 45), 10);
        }
        private void DrawPullButton(Rect rect, int count)
        {
            GUI.enabled = battle.Tickets >= count;
            if (GUI.Button(rect, "Summon x" + count, count == 10 ? knightSelectedButton : button))
            {
                lastPulls = battle.Summon(count);
                animationStart = Time.unscaledTime;
            }
            GUI.enabled = true;
        }
        private void InventoryBox(Rect rect, Color inside, Color border)
        {
            Fill(rect, border);
            Fill(new Rect(rect.x + 2, rect.y + 2, rect.width - 4, rect.height - 4), inside);
        }

        private void DrawInventory(bool picking, float width, float height)
        {
            const int pageSize = 6;
            bool wide = width > height;
            float catalogWidth = wide ? width * 0.53f - 24 : width - 32;
            float catalogHeight = wide ? height - 26 : Mathf.Min(352, height * 0.47f);
            var catalog = new Rect(16, 12, catalogWidth, catalogHeight);
            var detail = wide
                ? new Rect(catalog.xMax + 16, 10, width - catalog.xMax - 31, height - 23)
                : new Rect(16, catalog.yMax + 12, width - 32, height - catalog.yMax - 25);

            CourtHeading(new Rect(catalog.x, catalog.y, catalog.width - 123, 36),
                picking ? "Choose " + gearSlot.ToString().ToLowerInvariant() : "Collection", 25, TextAnchor.MiddleLeft);
            if (picking || !wide)
                DrawAmount(new Rect(catalog.xMax - 114, catalog.y + 2, 110, 31), goldCoin, battle.Gold);
            else
                ArcadeText(new Rect(catalog.xMax - 126, catalog.y, 126, 36), battle.Inventory.Count + " owned",
                    14, HallAmber, TextAnchor.MiddleRight);

            int filter = picking ? (int)gearSlot : gearFilter;
            var tabs = new Rect(catalog.x, catalog.y + 42, catalog.width, 39);
            if (picking)
            {
                var equipped = battle.Equipped(featureHero, gearSlot);
                CourtRule(new Rect(tabs.x, tabs.y, tabs.width, 1), HallLine);
                ArcadeText(tabs, "Equipped   ·   " + (equipped == null ? "Empty" : equipped.Gear.Name + "  +" + equipped.Enhancement),
                    15, HallLight, TextAnchor.MiddleLeft);
            }
            else
            {
                float tabWidth = tabs.width / GearFilters.Length;
                for (int i = 0; i < GearFilters.Length; i++)
                {
                    var tab = new Rect(tabs.x + i * tabWidth, tabs.y, tabWidth, tabs.height);
                    bool active = gearFilter == i - 1;
                    if (active)
                    {
                        ManuscriptFade(tab, new Color32(61, 56, 50, 0), new Color32(113, 86, 48, 150));
                        CourtRule(new Rect(tab.x + 6, tab.yMax - 1, tab.width - 12, 1), HallAmber);
                    }
                    else Fill(new Rect(tab.x, tab.yMax - 1, tab.width, 1), HallLine);
                    ArcadeText(tab, GearFilters[i], 15, active ? Parchment : BodyInk, TextAnchor.MiddleCenter);
                    if (!active && GUI.Button(tab, GUIContent.none, GUIStyle.none))
                    {
                        gearFilter = filter = i - 1;
                        inventoryPage = 0;
                        selectedGear = pendingSale = null;
                    }
                }
            }

            visibleGear.Clear();
            foreach (var item in battle.Inventory)
                if (filter < 0 || (int)item.Gear.Slot == filter) visibleGear.Add(item);
            int pages = Mathf.Max(1, (visibleGear.Count + pageSize - 1) / pageSize);
            // Keep the currently equipped piece in view when the hero opens a long item list.
            if (picking && inventoryPage == -1)
                inventoryPage = Mathf.Max(0, visibleGear.IndexOf(selectedGear)) / pageSize;
            inventoryPage = Mathf.Clamp(inventoryPage, 0, pages - 1);
            if (pages > 1)
            {
                float y = catalog.yMax - 34;
                GUI.enabled = inventoryPage > 0;
                if (GUI.Button(new Rect(catalog.center.x - 89, y, 46, 34), "‹")) { inventoryPage--; pendingSale = null; }
                GUI.enabled = inventoryPage + 1 < pages;
                if (GUI.Button(new Rect(catalog.center.x + 43, y, 46, 34), "›")) { inventoryPage++; pendingSale = null; }
                GUI.enabled = true;
                ArcadeText(new Rect(catalog.center.x - 40, y, 80, 34), (inventoryPage + 1) + " / " + pages,
                    14, HallLight, TextAnchor.MiddleCenter);
            }
            int start = inventoryPage * pageSize;
            int selected = visibleGear.IndexOf(selectedGear);
            if (selected < start || selected >= start + pageSize)
                selectedGear = visibleGear.Count > start ? visibleGear[start] : null;

            const int gap = 8;
            float gridTop = tabs.yMax + 12;
            float cardWidth = (catalog.width - 2 * gap) / 3;
            float cardHeight = (catalog.yMax - gridTop - (pages > 1 ? 42 : 0) - gap) * 0.5f;
            for (int i = 0; i < pageSize && start + i < visibleGear.Count; i++)
            {
                var item = visibleGear[start + i];
                var card = new Rect(catalog.x + i % 3 * (cardWidth + gap), gridTop + i / 3 * (cardHeight + gap),
                    cardWidth, cardHeight);
                if (DrawGearCard(card, item, item == selectedGear))
                {
                    selectedGear = item;
                    pendingSale = null;
                }
            }
            if (selectedGear == null)
            {
                ArcadeText(new Rect(catalog.x, gridTop, catalog.width, 88), "No equipment in this category.",
                    16, HallLight, TextAnchor.MiddleCenter);
                return;
            }
            DrawGearProfile(detail, picking);
        }

        private bool DrawGearCard(Rect card, TrialBattle.Item item, bool selected)
        {
            bool hovered = card.Contains(Event.current.mousePosition);
            Color edge = selected ? HallAmber : hovered ? FrameGold : new Color32(73, 75, 78, 255);
            Fill(card, edge);
            var inside = new Rect(card.x + 1, card.y + 1, card.width - 2, card.height - 2);
            ManuscriptFade(inside, selected ? new Color32(60, 57, 55, 255) : new Color32(36, 44, 54, 255), HallInk);
            float size = Mathf.Min(card.width - 25, card.height - 30);
            Picture(GearIcon(item), card.center.x - size / 2, card.y + 4, size, size);
            if (item.Enhancement > 0)
                ArcadeText(new Rect(card.x + 7, card.y + 2, 60, 22), "+" + item.Enhancement, 12, HallAmber, TextAnchor.MiddleLeft);
            if (item.Owner >= 0)
                DrawCourtEmblem(new Rect(card.xMax - 26, card.y + 5, 19, 19), 0, HallAmber);
            CourtHeading(new Rect(card.x + 3, card.yMax - 29, card.width - 6, 26), item.Gear.Name, 16, TextAnchor.MiddleCenter);
            if (selected) CourtGem(new Vector2(card.center.x, card.y + 1), 5, HallAmber);
            return GUI.Button(card, new GUIContent("", item.Gear.Name), GUIStyle.none);
        }

        private void DrawGearProfile(Rect panel, bool picking)
        {
            var item = selectedGear;
            var gear = item.Gear;
            string stat = gear.Slot == TrialData.GearSlot.Weapon ? "ATK" : gear.Slot == TrialData.GearSlot.Armor ? "DEF" : "HP";
            bool capped = item.Enhancement >= battle.EnhancementLimit || item.Bonus == int.MaxValue;
            GUI.BeginGroup(panel);
            var area = new Rect(0, 0, panel.width, panel.height);
            float artHeight = area.height - 164;
            var art = new Rect(0, 0, area.width, artHeight);
            var backdrop = FeatureTexture("KnightUI/OathChamber");
            // Crop the quiet stone alcove on the left, leaving the selected item as the focal point.
            float crop = Mathf.Min(0.53f, art.width / art.height * backdrop.height / backdrop.width);
            GUI.DrawTextureWithTexCoords(art, backdrop, new Rect(0, 0.14f, crop, Mathf.Min(0.86f,
                crop * backdrop.width / backdrop.height * art.height / art.width)));
            ManuscriptFade(art, new Color32(12, 19, 32, 60), new Color32(14, 23, 35, 212));
            float iconSize = Mathf.Min(142, art.height - 19);
            GUI.DrawTexture(new Rect(art.center.x - iconSize * 0.55f, art.center.y - iconSize * 0.55f,
                iconSize * 1.1f, iconSize * 1.1f), coatMedallion, ScaleMode.ScaleToFit);
            Picture(GearIcon(item), art.center.x - iconSize * 0.42f, art.center.y - iconSize * 0.42f, iconSize * 0.84f, iconSize * 0.84f);
            ArcadeText(new Rect(15, 6, area.width - 30, 24), gear.Slot.ToString().ToUpperInvariant(),
                12, HallAmber, TextAnchor.MiddleLeft);
            ManuscriptFade(new Rect(0, artHeight, area.width, 164), new Color32(35, 40, 52, 255), HallInk);
            CourtRule(new Rect(0, artHeight, area.width, 1), FrameGold);
            float x = 16, y = artHeight + 8;
            CourtHeading(new Rect(x, y, area.width - 32, 35), gear.Name + "  +" + item.Enhancement,
                26, TextAnchor.MiddleLeft);
            string owner = item.Owner < 0 ? "In bag" : "Equipped by " + battle.Heroes[item.Owner];
            ArcadeText(new Rect(x, y + 35, area.width - 32, 21), owner, 14, BodyInk, TextAnchor.MiddleLeft);
            string increase = capped ? stat + "  +" + item.Bonus : stat + "  +" + item.Bonus + "  →  +" + item.BonusAt(item.Enhancement + 1);
            ArcadeText(new Rect(x, y + 64, area.width - 32, 25), increase, 18, Parchment, TextAnchor.MiddleLeft);
            if (picking)
            {
                var equipped = battle.Equipped(featureHero, gearSlot);
                int delta = item.Bonus - (equipped?.Bonus ?? 0);
                string compare = delta == 0 ? "Same as equipped" : (delta > 0 ? "+" : "") + delta + " vs. equipped";
                ArcadeText(new Rect(area.width * 0.48f, y + 64, area.width * 0.52f - 15, 25), compare,
                    13, delta >= 0 ? HallAmber : BodyInk, TextAnchor.MiddleRight);
            }
            float actionWidth = (area.width - 40) / 2;
            var enhance = new Rect(16, area.height - 53, actionWidth, 41);
            var secondary = new Rect(enhance.xMax + 8, enhance.y, actionWidth, enhance.height);
            GUI.enabled = battle.CanEnhance(item);
            if (GUI.Button(enhance, capped ? "Maximum level" : $"Enhance   {battle.EnhanceCost(item):N0} G", knightSelectedButton))
            {
                pendingSale = null;
                battle.Enhance(item, progress => TrySaveProgress(progress, pendingSeconds));
            }
            GUI.enabled = true;
            bool own = item.Owner == featureHero;
            GUI.enabled = item.Owner < 0 || (picking && own);
            string action = picking ? (own ? "Remove" : "Equip") :
                pendingSale == item ? "Confirm sale" : "Sell   " + gear.SellGold + " G";
            if (GUI.Button(secondary, action, button))
            {
                if (picking)
                {
                    if (own) battle.Unequip(featureHero, gearSlot); else battle.Equip(featureHero, item);
                    CheckpointProgress();
                    heroView = HeroView.Info;
                }
                else if (pendingSale == item)
                {
                    battle.Sell(item);
                    selectedGear = pendingSale = null;
                }
                else pendingSale = item;
            }
            GUI.enabled = true;
            GUI.EndGroup();
        }

        private GUIStyle inventoryText;
        private GUIStyle HudCentered => inventoryText ?? (inventoryText = new GUIStyle(small)
            { alignment = TextAnchor.MiddleCenter });

        private string GearIcon(TrialBattle.Item item)
        {
            string key = "EquipmentUI/Items/" + item.Gear.Name.Replace(" ", "");
            return FeatureTexture(key) ? key : "EquipmentUI/" + Equipment[(int)item.Gear.Slot];
        }
        private static string DungeonArt(TrialData.DungeonKind kind) =>
            kind == TrialData.DungeonKind.GoldVault ? "DungeonUI/GoldVaultEntrance" : "DungeonUI/ExperienceArchiveEntrance";

        private static Color GateMetal(TrialData.DungeonKind kind) =>
            kind == TrialData.DungeonKind.GoldVault ? HallAmber : new Color32(142, 213, 220, 255);

        private void DrawDungeonPage(float width, float height, bool portrait)
        {
            if (selectedDungeon.HasValue)
            {
                DrawDungeonDifficulty(selectedDungeon.Value, width, height, portrait);
                return;
            }

            const float margin = 17, gap = 12;
            int count = battle.Data.Dungeons.GetLength(0);
            float cardWidth = width - 2 * margin, cardHeight = height - 2 * margin;
            if (portrait) cardHeight = (cardHeight - gap * (count - 1)) / count;
            else cardWidth = (cardWidth - gap * (count - 1)) / count;
            for (int i = 0; i < count; i++)
            {
                var tier = battle.Data.Dungeons[i, 0];
                bool gold = tier.Kind == TrialData.DungeonKind.GoldVault;
                var ink = GateMetal(tier.Kind);
                var card = new Rect(margin + (portrait ? 0 : i * (cardWidth + gap)),
                    margin + (portrait ? i * (cardHeight + gap) : 0), cardWidth, cardHeight);
                bool hovered = card.Contains(Event.current.mousePosition);
                Fill(card, hovered ? ink : HallLine);
                var art = new Rect(card.x + 1, card.y + 1, card.width - 2, card.height - 2);
                GUI.DrawTexture(art, FeatureTexture(DungeonArt(tier.Kind)), ScaleMode.ScaleAndCrop);
                ManuscriptFade(new Rect(art.x, art.yMax - 168, art.width, 168), new Color32(10, 16, 25, 0), HallInk);
                CourtRule(new Rect(art.x + 19, art.yMax - 119, 62, 1), ink);
                CourtHeading(new Rect(art.x + 19, art.yMax - 111, art.width - 40, 43), tier.Name, 33, TextAnchor.MiddleLeft);
                GUI.DrawTexture(new Rect(art.x + 20, art.yMax - 67, 24, 24), gold ? goldCoin : experienceCrystal, ScaleMode.ScaleToFit);
                int cleared = 0;
                int difficulties = battle.Data.Dungeons.GetLength(1);
                for (int j = 0; j < difficulties; j++)
                    if (battle.IsDungeonCleared(battle.Data.Dungeons[i, j])) cleared++;
                ArcadeText(new Rect(art.x + 52, art.yMax - 68, art.width - 75, 26),
                    gold ? "Gold rewards" : "Experience rewards", 16, HallLight, TextAnchor.MiddleLeft);
                string status = battle.CanEnterDungeon(tier)
                    ? $"{cleared} / {difficulties} difficulties cleared"
                    : "Unlocks after the first main-stage boss";
                ArcadeText(new Rect(art.x + 21, art.yMax - 35, art.width - 70, 24),
                    status, 14, ink, TextAnchor.MiddleLeft);
                CourtHeading(new Rect(art.xMax - 47, art.yMax - 51, 35, 41), "›", 31, TextAnchor.MiddleCenter);
                if (GUI.Button(card, GUIContent.none, GUIStyle.none))
                {
                    selectedDungeon = tier.Kind;
                    dungeonDifficulty = 0;
                }
            }
        }

        private void DrawDungeonDifficulty(TrialData.DungeonKind kind, float width, float height, bool portrait)
        {
            var entryTier = battle.Data.Dungeons[(int)kind, 0];
            bool gold = kind == TrialData.DungeonKind.GoldVault;
            var ink = GateMetal(kind);
            var art = portrait ? new Rect(0, 0, width, height * 0.45f) : new Rect(0, 0, width * 0.47f, height);
            GUI.DrawTexture(art, FeatureTexture(DungeonArt(kind)), ScaleMode.ScaleAndCrop);
            ManuscriptFade(new Rect(0, art.yMax - 147, art.width, 147), new Color32(10, 18, 30, 0), HallInk);
            CourtRule(new Rect(23, art.yMax - 76, 74, 1), ink);
            CourtHeading(new Rect(22, art.yMax - 66, art.width - 43, 52), entryTier.Name, 35, TextAnchor.MiddleLeft);
            if (GUI.Button(new Rect(17, 16, 129, 42), "‹   All gates", GUI.skin.button)) selectedDungeon = null;

            var book = portrait ? new Rect(0, art.yMax, width, height - art.height) :
                new Rect(art.xMax, 0, width - art.width, height);
            ManuscriptFade(book, new Color32(37, 42, 52, 255), HallInk);
            float left = book.x + 26, inner = book.width - 52;
            float top = book.y + (portrait ? 29 : 19);
            CourtHeading(new Rect(left, top, inner, 38), "Choose difficulty", 27, TextAnchor.MiddleLeft);
            int count = battle.Data.Dungeons.GetLength(1);
            float step = (inner + 8) / count;
            for (int i = 0; i < count; i++)
            {
                var option = battle.Data.Dungeons[(int)kind, i];
                var tab = new Rect(left + i * step, top + 55, step - 8, 64);
                bool selected = dungeonDifficulty == i;
                ManuscriptFade(tab, selected ? new Color32(65, 63, 60, 255) : ArcadeNavy, HallInk);
                Fill(new Rect(tab.x, tab.yMax - (selected ? 3 : 1), tab.width, selected ? 3 : 1), selected ? ink : HallLine);
                CourtHeading(new Rect(tab.x, tab.y + 4, tab.width, 29), option.Difficulty, 20, TextAnchor.MiddleCenter);
                string state = battle.IsDungeonCleared(option) ? "Cleared" : battle.CanEnterDungeon(option) ? "Open" : "Locked";
                ArcadeText(new Rect(tab.x, tab.y + 32, tab.width, 23), state, 13, selected ? ink : BodyInk, TextAnchor.MiddleCenter);
                if (GUI.Button(tab, GUIContent.none, GUIStyle.none)) dungeonDifficulty = i;
            }

            var tier = battle.Data.Dungeons[(int)kind, dungeonDifficulty];
            float metricsY = top + 141;
            CourtRule(new Rect(left, metricsY, inner, 1), HallLine);
            DrawLedgerAmount(new Rect(left, metricsY + 11, inner * 0.56f, 65), gold ? goldCoin : experienceCrystal,
                "Victory reward", (gold ? tier.Gold : tier.Experience).ToString("N0", CultureInfo.InvariantCulture));
            float timerX = left + inner * 0.59f;
            DrawCourtEmblem(new Rect(timerX, metricsY + 25, 27, 29), 5, ink);
            ArcadeText(new Rect(timerX + 37, metricsY + 14, inner * 0.4f - 37, 22), "Time limit", 13, BodyInk, TextAnchor.MiddleLeft);
            CourtHeading(new Rect(timerX + 37, metricsY + 34, inner * 0.4f - 37, 34), tier.Seconds + "s", 25, TextAnchor.MiddleLeft);
            CourtRule(new Rect(left, metricsY + 85, inner, 1), HallLine);

            bool canEnter = battle.CanEnterDungeon(tier);
            if (!canEnter)
            {
                string requirement = !battle.CanEnterDungeon(entryTier)
                    ? "Defeat the first main-stage boss to enter."
                    : "Clear " + battle.Data.Dungeons[(int)kind, dungeonDifficulty - 1].Difficulty + " in this dungeon to enter.";
                GUI.Label(new Rect(left, metricsY + 98, inner, 40), requirement, small);
            }
            GUI.enabled = canEnter;
            if (GUI.Button(new Rect(left, book.yMax - 73, inner, 50), "Enter dungeon", knightSelectedButton))
                EnterDungeon(tier);
            GUI.enabled = true;
        }

        private void DrawLedgerAmount(Rect area, Texture2D icon, string caption, string amount)
        {
            float size = Mathf.Min(45, area.width * 0.28f);
            GUI.DrawTexture(new Rect(area.x, area.center.y - size / 2, size, size), icon, ScaleMode.ScaleToFit);
            float left = area.x + size + 9, width = area.xMax - left;
            ArcadeText(new Rect(left, area.center.y - 27, width, 22), caption, 14, BodyInk, TextAnchor.MiddleLeft);
            CourtHeading(new Rect(left, area.center.y - 6, width, 37), amount,
                amount.Length > 10 ? 20 : 26, TextAnchor.MiddleLeft);
        }

        private void DrawIdlePage(float width, float height, bool portrait)
        {
            var hourly = EstimateIdleReward(3600);
            var art = portrait ? new Rect(0, 0, width, height * 0.42f) : new Rect(0, 0, width * 0.44f, height);
            // Frame the gate and its heraldry above the ledger caption in both orientations.
            float viewHeight = 0.76f;
            float viewWidth = viewHeight * art.width / art.height * courtyard.height / courtyard.width;
            GUI.DrawTextureWithTexCoords(art, courtyard,
                new Rect(Mathf.Clamp(0.22f - viewWidth * 0.5f, 0, 1 - viewWidth), 1 - viewHeight, viewWidth, viewHeight));
            ManuscriptFade(new Rect(0, art.yMax - 173, art.width, 173), new Color32(7, 16, 25, 0), HallInk);
            DrawCourtEmblem(new Rect(23, art.yMax - 96, 29, 33), 5, HallAmber);
            ArcadeText(new Rect(62, art.yMax - 94, art.width - 85, 32), "Reward stage", 15, HallLight, TextAnchor.MiddleLeft);
            CourtHeading(new Rect(22, art.yMax - 58, art.width - 40, 42), battle.StageName(hourly.Stage),
                portrait ? 31 : 27, TextAnchor.MiddleLeft);

            var book = portrait ? new Rect(0, art.yMax, width, height - art.height) :
                new Rect(art.xMax, 0, width - art.width, height);
            ManuscriptFade(book, new Color32(36, 43, 55, 255), HallInk);
            float x = book.x + 29, inner = book.width - 58, y = book.y + (portrait ? 42 : 27);
            CourtHeading(new Rect(x, y, inner, 43), "Earnings per hour", 30, TextAnchor.MiddleLeft);
            CourtRule(new Rect(x, y + 55, inner, 1), FrameGold);
            DrawLedgerAmount(new Rect(x, y + 66, inner / 2, 86), goldCoin, "Gold",
                "+" + hourly.Gold.ToString("N0", CultureInfo.InvariantCulture));
            DrawLedgerAmount(new Rect(x + inner / 2, y + 66, inner / 2, 86), experienceCrystal, "Experience",
                "+" + hourly.Experience.ToString("N0", CultureInfo.InvariantCulture));
            CourtRule(new Rect(x, y + 157, inner, 1), HallLine);
            DrawAccrualRow(x, y + 170, inner, "Accrual limit", battle.Data.IdleCapHours + " hours");
            DrawAccrualRow(x, y + 218, inner, "Farming rate", battle.Data.IdleEfficiencyPercent + "%");
            CourtRule(new Rect(x, y + 271, inner, 1), HallLine);
            GUI.Label(new Rect(x, y + 287, inner, 63),
                battle.Highest > 0 ? "Equipment may drop during idle farming." : "Clear the first stage to earn equipment while away.", small);
        }

        private void DrawAccrualRow(float x, float y, float width, string caption, string value)
        {
            ArcadeText(new Rect(x, y, width * 0.58f, 40), caption, 16, BodyInk, TextAnchor.MiddleLeft);
            CourtHeading(new Rect(x + width * 0.58f, y, width * 0.42f, 40), value, 23, TextAnchor.MiddleRight);
        }

        private GUIStyle idleAmountText;

        private void DrawRewardAmount(float centerX, float top, Texture2D icon, string name, int amount)
        {
            if (idleAmountText == null)
                idleAmountText = new GUIStyle(label) { alignment = TextAnchor.MiddleCenter, fontSize = 24, fontStyle = FontStyle.Bold };
            GUI.DrawTexture(new Rect(centerX - 24, top, 48, 48), icon, ScaleMode.ScaleToFit);
            GUI.Label(new Rect(centerX - 102, top + 49, 204, 22), name, HudCentered);
            GUI.Label(new Rect(centerX - 105, top + 71, 210, 33),
                "+" + amount.ToString("N0", CultureInfo.InvariantCulture), idleAmountText);
        }

        private static string IdleDuration(long seconds)
        {
            if (seconds >= 86400) return $"{seconds / 86400}d {seconds / 3600 % 24}h {seconds / 60 % 60}m";
            if (seconds >= 3600) return $"{seconds / 3600}h {seconds / 60 % 60}m";
            if (seconds >= 60) return $"{seconds / 60}m {seconds % 60}s";
            return seconds + "s";
        }

        private Vector2 idleGearScroll;

        private static string DescribeIdleEquipment(TrialBattle.IdleEquipment equipment)
        {
            var counts = new Dictionary<string, int>();
            foreach (var item in equipment.Items)
            {
                counts.TryGetValue(item.Gear.Name, out int count);
                counts[item.Gear.Name] = count + 1;
            }
            var lines = new List<string>();
            foreach (var pair in counts) lines.Add($"{pair.Key}  x{pair.Value}");
            if (equipment.Converted > 0) lines.Add($"Bag full: {equipment.Converted} converted to gold");
            return lines.Count == 0 ? "No equipment found" : string.Join("\n", lines);
        }

        private void DrawIdleReturnPopup(int width, int height)
        {
            var reward = PendingIdleReward;
            bool portrait = height > width;
            bool saveFailed = !string.IsNullOrEmpty(idleSaveError);
            DrawKnightBackdrop(width, height);
            Fill(new Rect(0, 0, width, height), new Color32(6, 12, 22, 133));
            float pageWidth = portrait ? width - 36 : 816;
            float pageHeight = portrait ? 720 : 434;
            GUI.BeginGroup(new Rect((width - pageWidth) / 2, (height - pageHeight) / 2, pageWidth, pageHeight));
            Fill(new Rect(0, 0, pageWidth, pageHeight), FrameGold);
            var art = portrait ? new Rect(1, 1, pageWidth - 2, 256) : new Rect(1, 1, 292, pageHeight - 2);
            GUI.DrawTexture(art, FeatureTexture("KnightUI/ChapterHall"), ScaleMode.ScaleAndCrop);
            ManuscriptFade(new Rect(art.x, art.yMax - 214, art.width, 214), new Color32(9, 19, 31, 0), HallInk);
            CourtRule(new Rect(art.x + 20, art.yMax - 159, 71, 1), HallAmber);
            ArcadeText(new Rect(art.x + 20, art.yMax - 151, art.width - 34, 22),
                battle.StageName(reward.Stage), 13, FrameLight, TextAnchor.MiddleLeft);
            CourtHeading(new Rect(art.x + 19, art.yMax - 130, art.width - 33, 47), "Welcome back", 34, TextAnchor.MiddleLeft);
            ArcadeText(new Rect(art.x + 21, art.yMax - 72, art.width - 35, 22),
                "Time away    " + IdleDuration(reward.AwaySeconds), 15, BodyInk, TextAnchor.MiddleLeft);
            if (reward.AwaySeconds > reward.CreditedSeconds)
                ArcadeText(new Rect(art.x + 21, art.yMax - 46, art.width - 35, 22),
                    "Reward time    " + IdleDuration(reward.CreditedSeconds), 15, FrameLight, TextAnchor.MiddleLeft);

            var ledger = portrait ? new Rect(1, art.yMax, pageWidth - 2, pageHeight - art.yMax - 1)
                : new Rect(art.xMax, 1, pageWidth - art.xMax - 1, pageHeight - 2);
            ManuscriptFade(ledger, new Color32(36, 43, 55, 255), HallInk);
            float x = ledger.x + 24, inner = ledger.width - 48, y = ledger.y + 21;
            CourtHeading(new Rect(x, y, inner, 39), "Idle rewards", 28, TextAnchor.MiddleLeft);
            CourtRule(new Rect(x, y + 47, inner, 1), HallLine);
            DrawLedgerAmount(new Rect(x, y + 58, inner / 2, 69), goldCoin, "Gold",
                "+" + reward.Gold.ToString("N0", CultureInfo.InvariantCulture));
            DrawLedgerAmount(new Rect(x + inner / 2, y + 58, inner / 2, 69), experienceCrystal, "Experience",
                "+" + reward.Experience.ToString("N0", CultureInfo.InvariantCulture));
            CourtRule(new Rect(x, y + 140, inner, 1), HallLine);
            CourtHeading(new Rect(x, y + 149, inner, 35), "Equipment", 23, TextAnchor.MiddleLeft);
            float actionY = ledger.yMax - 68;
            var scroll = new Rect(x, y + 190, inner, actionY - (y + 190) - (saveFailed ? 57 : 13));
            var gear = new GUIContent(reward.EquipmentSummary);
            float listWidth = scroll.width - 22;
            float listHeight = Mathf.Max(scroll.height, small.CalcHeight(gear, listWidth) + 6);
            idleGearScroll = GUI.BeginScrollView(scroll, idleGearScroll, new Rect(0, 0, listWidth, listHeight));
            GUI.Label(new Rect(0, 0, listWidth, listHeight), gear, small);
            GUI.EndScrollView();
            if (saveFailed) GUI.Label(new Rect(x, actionY - 52, inner, 47), idleSaveError, small);
            bool confirm = GUI.Button(new Rect(x, actionY, inner, 47), "Confirm & collect", knightSelectedButton);
            GUI.EndGroup();
            if (confirm) ConfirmIdleReward();
        }
    }
}
