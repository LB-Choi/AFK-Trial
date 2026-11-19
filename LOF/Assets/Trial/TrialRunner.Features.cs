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
        private int featurePage = -1, featureHero, dungeonChoice;
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
            pendingSale = null;
            editing = true;
            featurePage = -1;
            heroView = HeroView.Menu;
            animationStart = float.NegativeInfinity;
        }

        private static readonly string[] NavigationArt =
        {
            "KnightUI/NavHeroes", "KnightUI/NavFormation", "KnightUI/NavSummon",
            "KnightUI/NavGear", "KnightUI/NavDungeons", "KnightUI/NavIdle"
        };
        private static readonly string[] PageTitles =
            { "Formation", "Oath chamber", "Equipment inventory", "Dungeon gates", "Idle rewards" };
        private GUIStyle destinationTitle, destinationDetail, hallTitle, hallLocation;
        private GUIStyle tabCaption, summonName, summonLevel;

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

        private void PrepareHallStyles()
        {
            if (hallTitle != null) return;
            hallTitle = new GUIStyle(title) { fontSize = 41, alignment = TextAnchor.MiddleLeft };
            destinationTitle = new GUIStyle(title) { fontSize = 23, alignment = TextAnchor.MiddleLeft };
            destinationDetail = new GUIStyle(small) { fontSize = 14, alignment = TextAnchor.MiddleLeft };
            destinationDetail.normal.textColor = new Color32(186, 209, 210, 255);
            hallLocation = new GUIStyle(small) { fontSize = 15, fontStyle = FontStyle.Bold };
            hallLocation.normal.textColor = new Color32(248, 213, 153, 255);
        }

        private string DestinationStatus(int index)
        {
            switch (index)
            {
                case 0: return OwnedCount() + " / " + portraits.Length + " owned";
                case 1: return draft.Length + " deployed";
                case 2: return battle.Tickets + " tickets";
                case 3: return battle.Inventory.Count + " items";
                case 4: return "Entrance preview";
                default: return "Offline earnings";
            }
        }

        private void DrawMenu(int width, int height)
        {
            PrepareHallStyles();
            bool wide = width > height;
            float margin = wide ? 25 : 18;
            float bannerHeight = wide ? 172 : 274;
            var banner = new Rect(margin, 16, width - margin * 2, bannerHeight);
            GUI.DrawTexture(banner, FeatureTexture("KnightUI/ChapterHall"), ScaleMode.ScaleAndCrop);
            Fill(new Rect(banner.x, banner.yMax - 3, banner.width, 3), InventoryGold);
            float textX = banner.x + 25;
            GUI.Label(new Rect(textX, banner.center.y - 38, 305, 26), battle.StageName(battle.ActiveStage), hallLocation);
            GUI.Label(new Rect(textX - 2, banner.center.y - 18, 315, 55), "Knight Hall", hallTitle);

            int columns = wide ? 3 : 2;
            int rows = NavigationArt.Length / columns;
            float top = banner.yMax + 16;
            float cardWidth = (banner.width - (columns - 1) * 10) / columns;
            float cardHeight = (height - 90 - top - (rows - 1) * 10) / rows;
            for (int i = 0; i < NavigationArt.Length; i++)
            {
                var rect = new Rect(margin + (i % columns) * (cardWidth + 10),
                    top + (i / columns) * (cardHeight + 10), cardWidth, cardHeight);
                bool open = GUI.Button(rect, GUIContent.none, button);
                float emblem = wide ? 70 : 74;
                float iconX = wide ? rect.x + 14 : rect.center.x - emblem / 2;
                float iconY = wide ? rect.center.y - emblem / 2 : rect.y + 12;
                Picture(NavigationArt[i], iconX, iconY, emblem, emblem);
                float copyX = wide ? iconX + emblem + 12 : rect.x + 19;
                float copyY = wide ? rect.center.y - 31 : iconY + emblem + 3;
                GUI.Label(new Rect(copyX, copyY, rect.xMax - copyX - 8, 35),
                    i == 0 ? "Heroes" : FeatureTabs[i - 1], destinationTitle);
                GUI.Label(new Rect(copyX + 1, copyY + 36, rect.xMax - copyX - 9, 28), DestinationStatus(i), destinationDetail);
                if (!open) continue;
                if (i == 0) { OpenHeroes(); heroesFromMenu = true; }
                else OpenFeature(i - 1);
            }
            DrawWallet(new Rect(margin + 5, height - 70, wide ? 335 : 285, 45));
            if (GUI.Button(new Rect(width - margin - 168, height - 71, 168, 47), "Resume battle", knightSelectedButton))
                editing = false;
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
            if (tabCaption == null) tabCaption = new GUIStyle(HudCentered)
                { fontSize = 14, wordWrap = false, padding = new RectOffset() };
            DrawKnightBackdrop(width, height);
            GUI.Label(new Rect(20, 12, width - 178, 42), PageTitles[featurePage], title);
            if (GUI.Button(new Rect(width - 145, 13, 125, 38), heroView == HeroView.Info ? "Hero info" : "Menu", button)) featurePage = -1;
            float tabWidth = (width - 40f) / FeatureTabs.Length;
            for (int i = 0; i < FeatureTabs.Length; i++)
            {
                var tab = new Rect(20 + i * tabWidth, 66, tabWidth - 3, 42);
                if (GUI.Button(tab, GUIContent.none, featurePage == i ? knightSelectedButton : GUI.skin.button) && featurePage != i)
                {
                    pendingSale = null;
                    OpenFeature(i);
                    animationStart = float.NegativeInfinity;
                }
                float icon = portrait ? 20 : 31;
                Picture(NavigationArt[i + 1], portrait ? tab.center.x - icon / 2 : tab.x + 18,
                    portrait ? tab.y + 3 : tab.center.y - icon / 2, icon, icon);
                var caption = portrait ? new Rect(tab.x, tab.y + 21, tab.width, 18)
                    : new Rect(tab.x + 50, tab.y, tab.width - 59, tab.height);
                GUI.Label(caption, FeatureTabs[i], tabCaption);
            }
            if (featurePage < 0) return;
            if (featurePage == 0) { DrawPartyPage(width, height); return; }
            float panelWidth = (featurePage == 1 || featurePage == 2) ? Mathf.Min(width - 40, 770) : 500;
            bool tallGallery = portrait && featurePage == 1 && lastPulls != null && lastPulls.Length > 1;
            float panelHeight = tallGallery ? 754 : 410;
            float panelTop = 118 + (height - 130 - panelHeight) / 2f;
            GUI.BeginGroup(new Rect((width - panelWidth) / 2f, panelTop, panelWidth, panelHeight));
            switch (featurePage)
            {
                case 1: DrawSummonPage(panelWidth, tallGallery); break;
                case 2: DrawInventory(false, panelWidth); break;
                case 3: DrawDungeonPage(); break;
                case 4: DrawIdlePage(); break;
            }
            GUI.EndGroup();
        }

        private void DrawSkillPage()
        {
            var hero = battle.Data.Heroes[featureHero];
            string id = hero.Id.Replace('_', ' ');
            string prefix = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(id).Replace(" ", "");
            GUI.Label(new Rect(380, 18, 120, 32), "Skills", label);
            for (int i = 0; i < 3; i++)
            {
                int y = 58 + i * 91;
                if (GUI.Button(new Rect(360, y, 136, 82), GUIContent.none, button))
                {
                    selectedSkill = i;
                    skillScroll = Vector2.zero;
                }
                Picture("SkillUI/" + prefix + "Skill" + (i + 1), 366, y + 5, 48, 48);
                GUI.Label(new Rect(417, y + 15, 78, 30), "Lv." + battle.SkillLevel(featureHero, i), small);
                GUI.Label(new Rect(364, y + 49, 129, 30), battle.Data.Skills[featureHero, i].Name, formationName);
            }
            if (selectedSkill >= 0)
            {
                DrawSkillDetails();
                return;
            }
            GUI.DrawTexture(new Rect(144, 38, 211, 225), portraits[featureHero], ScaleMode.ScaleToFit);
            GUI.Label(new Rect(144, 265, 211, 36), hero.Name + "  Lv." + battle.HeroLevel(featureHero, battle.Level), formationName);
            GUI.Label(new Rect(144, 295, 211, 28), hero.Role.ToString(), formationName);
            if (hero.Role != TrialData.Role.None)
                Picture("RoleUI/" + RoleIcons[(int)hero.Role - 1], 180, 296, 24, 24);
            var stats = battle.HeroStats(featureHero, battle.Level);
            GUI.Label(new Rect(0, 346, 500, 38),
                $"HP {stats.hp:0.##}    ATK {stats.attack:0.##}    DEF {stats.armor}", formationName);
            GUI.Label(new Rect(10, 18, 135, 32), "Equipment", label);
            for (int i = 0; i < Equipment.Length; i++)
            {
                var slot = (TrialData.GearSlot)i;
                var item = battle.Equipped(featureHero, slot);
                float y = 58 + i * 91;
                if (GUI.Button(new Rect(4, y, 136, 82), GUIContent.none, button))
                {
                    gearSlot = slot;
                    inventoryPage = 0;
                    selectedGear = battle.Equipped(featureHero, slot);
                    pendingSale = null;
                    heroView = HeroView.GearPicker;
                }
                Picture("EquipmentUI/EquipmentSlotFrame", 12, y + 5, 34, 34);
                if (item != null) Picture(GearIcon(item), 14, y + 7, 30, 30);
                GUI.Label(new Rect(49, y + 8, 90, 30), slot.ToString(), small);
                GUI.Label(new Rect(9, y + 41, 126, 35), item?.Gear.Name ?? "Empty", formationName);
            }
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
            var content = new GUIContent(description);
            float textHeight = Mathf.Max(99, small.CalcHeight(content, 289));
            skillScroll = GUI.BeginScrollView(new Rect(12, 68, 312, 100), skillScroll, new Rect(0, 0, 289, textHeight));
            GUI.Label(new Rect(0, 0, 289, textHeight), content, small);
            GUI.EndScrollView();
            GUI.Label(new Rect(12, 175, 215, 30), $"Skill level {level} / 5", label);
            int cost = battle.SkillCost(featureHero, slot);
            string status = level >= 5 ? "Maximum level" : level >= battle.HeroLevel(featureHero, battle.Level)
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
            if (page != 0) return;
            for (int i = 0; i < draft.Length; i++) draft[i] = battle.HeroAt(i);
            selectedSlot = 0;
        }

        private void DrawPartyPage(int width, int height)
        {
            if (showSynergyEffects)
            {
                GUI.Label(new Rect(20, 120, 300, 42), "Synergy effects", title);
                if (GUI.Button(new Rect(width - 148, 122, 128, 36), "Back to slots", button)) showSynergyEffects = false;
                var preview = battle.FormationSynergy(draft);
                for (int i = 0; i < RoleIcons.Length; i++)
                {
                    var role = (TrialData.Role)(i + 1);
                    float y = 190 + i * 100;
                    Picture("RoleUI/" + RoleIcons[i], 20, y, 40, 40);
                    string state = battle.ActiveSynergy == role ? "Active now" : "Inactive";
                    GUI.Label(new Rect(72, y, width - 90, 34), $"2 {role} heroes  /  {state}", label);
                    DrawSynergyLine(72, y + 36, preview == role ? "Selected team" : "Effect", role);
                }
                return;
            }
            if (GUI.Button(new Rect(width - 165, 113, 145, 26), "Synergy effects")) showSynergyEffects = true;
            GUI.Label(new Rect(20, 114, width - 200, 26), "Choose a slot, then a hero.", small);
            float slotWidth = (width - 52) / 3f;
            bool changed = false;
            for (int i = 0; i < draft.Length; i++)
            {
                var rect = new Rect(20 + i * (slotWidth + 6), 142, slotWidth, 52);
                if (GUI.Button(rect, GUIContent.none, GUIStyle.none)) selectedSlot = i;
                GUI.DrawTexture(rect, slotFrame);
                GUI.DrawTexture(new Rect(rect.x + 6, rect.y + 8, 26, 32), deploymentBadges[i]);
                GUI.Label(new Rect(rect.x + 33, rect.y + 4, rect.width - 38, 44), battle.Heroes[draft[i]], HudCentered);
                if (i == selectedSlot) Fill(new Rect(rect.x + 6, rect.yMax - 4, rect.width - 12, 2), selectedSlotColor);
                changed |= draft[i] != battle.HeroAt(i);
            }
            DrawHeroGrid(width, 204, height - 308, true);
            DrawSynergyLine(20, height - 101, "Active", battle.ActiveSynergy);
            if (changed) DrawSynergyLine(20, height - 69, "After applying", battle.FormationSynergy(draft));
            GUI.enabled = changed;
            if (GUI.Button(new Rect(width - 245, height - 43, 225, 38), "Apply & restart battle", button))
            {
                battle.ApplyFormation(draft);
                accumulator = restartDelay = 0;
                editing = false;
            }
            GUI.enabled = true;
        }
        private void DrawSummonPage(float width, bool tallGallery)
        {
            var art = new Rect(5, 2, width - 10, tallGallery ? 627 : 283);
            GUI.DrawTexture(art, FeatureTexture("KnightUI/OathChamber"), ScaleMode.ScaleAndCrop);
            Fill(new Rect(art.x, art.yMax - 2, art.width, 2), InventoryGold);
            if (lastPulls == null)
            {
                GUI.Label(new Rect(25, 75, width * 0.53f, 49), "Knight's oath", title);
                GUI.Label(new Rect(28, 125, width * 0.42f, 96),
                    "One ticket calls one hero. A duplicate grants that hero one level.", small);
            }
            else
            {
                Fill(art, new Color(0.025f, 0.05f, 0.08f, 0.74f));
                bool single = lastPulls.Length == 1;
                int columns = tallGallery ? 3 : 5;
                float columnWidth = (width - 30) / columns;
                float rowHeight = (art.height - 10) / (tallGallery ? 4 : 2);
                for (int i = 0; i < lastPulls.Length; i++)
                {
                    var pull = lastPulls[i];
                    int inRow = Mathf.Min(columns, lastPulls.Length - i / columns * columns);
                    float left = (width - inRow * columnWidth) / 2;
                    var card = single ? new Rect(width / 2 - 99, 12, 198, 263)
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
            Picture("SummonUI/SummonTicket", 14, art.yMax + 7, 33, 33);
            GUI.Label(new Rect(51, art.yMax + 9, width - 62, 32), "Tickets   " + battle.Tickets, label);
            float buttonWidth = (width - 40) / 2;
            DrawPullButton(new Rect(14, art.yMax + 52, buttonWidth, 50), 1);
            DrawPullButton(new Rect(26 + buttonWidth, art.yMax + 52, buttonWidth, 50), 10);
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

        private void DrawInventory(bool picking, float width = 500)
        {
            const int columns = 6, pageSize = 18;
            float sidebar = width >= 700 ? 148 : 112;
            float cell = Mathf.Min(81, (width - sidebar - 26) / columns);
            DrawKnightPanel(new Rect(0, 0, width, 400));
            GUI.Label(new Rect(16, 10, width - 165, 32), picking ? "Choose " + gearSlot : "Owned equipment", label);
            DrawAmount(new Rect(width - 112, 8, 100, 32), goldCoin, battle.Gold);

            for (int i = 0; i < GearFilters.Length; i++)
            {
                var rect = new Rect(10, 54 + 48 * i, sidebar - 20, 40);
                bool active = picking ? i == (int)gearSlot + 1 : i == gearFilter + 1;
                InventoryBox(rect, active ? new Color(0.50f, 0.28f, 0.09f) : InventoryPanel, InventoryGold);
                GUI.Label(rect, GearFilters[i], HudCentered);
                if (!picking && GUI.Button(rect, GUIContent.none, GUIStyle.none) && !active)
                {
                    gearFilter = i - 1;
                    inventoryPage = 0;
                    selectedGear = pendingSale = null;
                }
            }
            visibleGear.Clear();
            int filter = picking ? (int)gearSlot : gearFilter;
            foreach (var item in battle.Inventory)
                if (filter < 0 || (int)item.Gear.Slot == filter) visibleGear.Add(item);
            int pages = Mathf.Max(1, (visibleGear.Count + pageSize - 1) / pageSize);
            inventoryPage = Mathf.Clamp(inventoryPage, 0, pages - 1);
            int start = inventoryPage * pageSize;
            if (selectedGear == null || visibleGear.IndexOf(selectedGear) < start ||
                visibleGear.IndexOf(selectedGear) >= start + pageSize)
                selectedGear = visibleGear.Count > start ? visibleGear[start] : null;

            for (int i = 0; i < pageSize; i++)
            {
                var rect = new Rect(sidebar + 5 + i % columns * cell, 48 + i / columns * cell, cell - 4, cell - 4);
                var item = start + i < visibleGear.Count ? visibleGear[start + i] : null;
                bool selected = item != null && item == selectedGear;
                InventoryBox(rect, selected ? new Color(0.29f, 0.21f, 0.12f) : new Color(0.075f, 0.08f, 0.085f),
                    selected ? selectedSlotColor : InventoryGold);
                if (item == null) continue;
                Picture(GearIcon(item), rect.x + 4, rect.y + 3, rect.width - 8, rect.height - 7);
                if (item.Owner >= 0)
                    Fill(new Rect(rect.x + 5, rect.yMax - 6, rect.width - 10, 2), selectedSlotColor);
                if (GUI.Button(rect, new GUIContent("", item.Gear.Name), GUIStyle.none))
                {
                    selectedGear = item;
                    pendingSale = null;
                }
            }
            var prev = new Rect(12, 257, 28, 32);
            var next = new Rect(sidebar - 39, 257, 28, 32);
            GUI.enabled = inventoryPage > 0;
            if (GUI.Button(prev, "<")) { inventoryPage--; pendingSale = null; }
            GUI.enabled = inventoryPage + 1 < pages;
            if (GUI.Button(next, ">")) { inventoryPage++; pendingSale = null; }
            GUI.enabled = true;
            GUI.Label(new Rect(prev.xMax, 257, next.x - prev.xMax, 32), (inventoryPage + 1) + "/" + pages, HudCentered);
            Fill(new Rect(12, 299, width - 24, 1), InventoryGold);
            if (selectedGear == null)
            {
                GUI.Label(new Rect(16, 324, width - 32, 36), "No equipment in this category.", label);
                return;
            }
            var gear = selectedGear.Gear;
            string stat = gear.Slot == TrialData.GearSlot.Weapon ? "ATK" : gear.Slot == TrialData.GearSlot.Armor ? "DEF" : "HP";
            GUI.Label(new Rect(16, 308, width - 160, 32), gear.Name + "   +" + gear.Bonus + " " + stat, label);
            GUI.Label(new Rect(16, 346, width - 162, 35), selectedGear.Owner < 0 ? "In bag" : "Equipped: " + battle.Heroes[selectedGear.Owner], small);
            bool own = selectedGear.Owner == featureHero;
            GUI.enabled = selectedGear.Owner < 0 || (picking && own);
            string action = picking ? (own ? "Remove" : "Equip") :
                pendingSale == selectedGear ? "Confirm sale" : "Sell " + gear.SellGold + " G";
            if (GUI.Button(new Rect(width - 153, 328, 139, 47), action, button))
            {
                if (picking)
                {
                    if (own) battle.Unequip(featureHero, gearSlot); else battle.Equip(featureHero, selectedGear);
                    heroView = HeroView.Info;
                }
                else if (pendingSale == selectedGear)
                {
                    battle.Sell(selectedGear);
                    selectedGear = pendingSale = null;
                }
                else pendingSale = selectedGear;
            }
            GUI.enabled = true;
        }

        private GUIStyle inventoryText;
        private GUIStyle HudCentered => inventoryText ?? (inventoryText = new GUIStyle(small)
            { alignment = TextAnchor.MiddleCenter });

        private string GearIcon(TrialBattle.Item item)
        {
            string key = "EquipmentUI/Items/" + item.Gear.Name.Replace(" ", "");
            return FeatureTexture(key) ? key : "EquipmentUI/" + Equipment[(int)item.Gear.Slot];
        }
        private void DrawDungeonPage()
        {
            bool gold = dungeonChoice == 0;
            Picture(gold ? "DungeonUI/GoldVaultEntrance" : "DungeonUI/ExperienceArchiveEntrance", 4, 4, 492, 277);
            if (GUI.Button(new Rect(8, 299, 234, 48), "Gold Vault", button)) dungeonChoice = 0;
            if (GUI.Button(new Rect(258, 299, 234, 48), "Experience Archive", button)) dungeonChoice = 1;
        }

        private void DrawIdlePage()
        {
            var hourly = EstimateIdleReward(3600);
            DrawKnightPanel(new Rect(8, 30, 484, 326));
            Picture("KnightUI/NavIdle", 26, 48, 52, 52);
            GUI.Label(new Rect(90, 48, 373, 38), "Earnings per hour", title);
            GUI.Label(new Rect(92, 86, 370, 25), battle.StageName(hourly.Stage), small);
            Fill(new Rect(30, 126, 440, 1), InventoryGold);
            DrawIdleAmount(132, 144, goldCoin, "Gold", hourly.Gold);
            DrawIdleAmount(368, 144, experienceCrystal, "Experience", hourly.Experience);
            Fill(new Rect(30, 262, 440, 1), InventoryGold);
            GUI.Label(new Rect(35, 275, 430, 58),
                $"Earn up to {battle.Data.IdleCapHours} hours at {battle.Data.IdleEfficiencyPercent}% of the farming rate for your highest cleared stage. Collect when you return.", HudCentered);
        }

        private GUIStyle idleAmountText;

        private void DrawIdleAmount(float centerX, float top, Texture2D icon, string name, int amount)
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

        private void DrawIdleReturnPopup(int width, int height)
        {
            var reward = PendingIdleReward;
            DrawKnightBackdrop(width, height);
            Fill(new Rect(0, 0, width, height), new Color(0.01f, 0.02f, 0.04f, 0.35f));
            var panel = new Rect((width - 500) / 2f, (height - 300) / 2f, 500, 300);
            GUI.BeginGroup(panel);
            Picture("IdleRewardUI/IdleRewardPanel", 0, 0, 500, 300);
            GUI.Label(new Rect(66, 18, 224, 42), "Welcome back", title);
            GUI.Label(new Rect(295, 18, 178, 40), battle.StageName(reward.Stage), HudCentered);
            string duration = "Time away   " + IdleDuration(reward.AwaySeconds);
            if (reward.AwaySeconds > reward.CreditedSeconds)
                duration += "     /     Reward time   " + IdleDuration(reward.CreditedSeconds);
            GUI.Label(new Rect(20, 64, 460, 25), duration, HudCentered);
            DrawIdleAmount(136, 100, goldCoin, "Gold", reward.Gold);
            DrawIdleAmount(364, 100, experienceCrystal, "Experience", reward.Experience);
            bool confirm = GUI.Button(new Rect(169, 238, 162, 44), "Confirm & collect", knightSelectedButton);
            GUI.EndGroup();
            if (!string.IsNullOrEmpty(idleSaveError))
                GUI.Label(new Rect(panel.x, panel.yMax + 6, panel.width, 44), idleSaveError, HudCentered);
            if (confirm) ConfirmIdleReward();
        }
    }
}
