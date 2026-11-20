using UnityEngine;

namespace AFKTrial
{
    public sealed partial class TrialRunner
    {
        private Texture2D courtyard, hpTrack, allyHp, enemyHp;
        private Texture2D goldVault, experienceArchive;
        private Texture2D bossMark, victoryMark, defeatMark;
        private Texture2D goldCoin, experienceCrystal;
        private GUIStyle battleHudText;
        private bool battleOptionsOpen;
        private static readonly Color bronzeRule = new Color(0.51f, 0.40f, 0.25f);
        private static readonly Color enemyAccent = new Color(0.86f, 0.48f, 0.43f);

        private void LoadBattleArt()
        {
            courtyard = LoadTexture("Battlefield/AlliedCastleGate");
            goldVault = LoadTexture("Battlefield/GoldVault");
            experienceArchive = LoadTexture("Battlefield/ExperienceArchive");
            hpTrack = LoadTexture("BattleUI/HpTrack");
            allyHp = LoadTexture("BattleUI/HpFillAlly");
            enemyHp = LoadTexture("BattleUI/HpFillEnemy");
            bossMark = LoadTexture("BattleUI/BossMark");
            victoryMark = LoadTexture("BattleUI/VictoryMark");
            defeatMark = LoadTexture("BattleUI/DefeatMark");
            goldCoin = LoadTexture("RewardUI/GoldCoin");
            experienceCrystal = LoadTexture("RewardUI/ExperienceCrystal");
        }

        private GUIStyle HudText => battleHudText ?? (battleHudText = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            alignment = TextAnchor.MiddleLeft,
            wordWrap = false,
            clipping = TextClipping.Clip,
            padding = new RectOffset()
        });

        private float StageHeaderOffset => !sideBattle && battle.BossStage ? 50f : 0f;

        private void DrawStageHeader(int width)
        {
            var dungeon = battle.ActiveDungeon;
            float offset = StageHeaderOffset;
            ManuscriptFade(new Rect(0, 0, width, 108 + offset), new Color32(7, 18, 28, 246), new Color32(7, 18, 28, 0));
            var boss = battle.ActiveBoss;
            if (boss != null)
            {
                var plate = new Rect((width - 232) * 0.5f, 3, 232, 45);
                ManuscriptFade(plate, new Color32(14, 22, 33, 244), new Color32(26, 35, 45, 220));
                GUI.DrawTexture(new Rect(plate.x + 9, plate.y + 4, 28, 28), bossMark, ScaleMode.ScaleToFit);
                CourtHeading(new Rect(plate.x + 40, plate.y, plate.width - 48, 31), boss.Name, 22, TextAnchor.MiddleCenter);
                DrawHp(new Rect(plate.x + 40, plate.y + 30, plate.width - 48, 10), boss);
                CourtRule(new Rect(plate.x + 5, plate.yMax - 1, plate.width - 10, 1), HudAmber);
            }
            GUI.BeginGroup(new Rect(0, offset, width, 108));
            GUI.DrawTexture(new Rect(13, 11, 48, 48), coatMedallion);
            GUI.DrawTexture(new Rect(17, 15, 40, 40), knightSeal, ScaleMode.ScaleToFit);
            string stageLine = dungeon != null ? dungeon.Difficulty : battle.StageName(battle.ActiveStage);
            if (dungeon == null && (battle.ActiveRepeat || battle.ActiveFarming || battle.BossStage))
                stageLine += battle.ActiveRepeat ? "   ·   REPEAT" : battle.ActiveFarming ? "   ·   FARM" : "   ·   BOSS";
            ArcadeText(new Rect(71, 9, sideBattle ? 350 : 222, 21), stageLine, 12, HudAmber, TextAnchor.MiddleLeft);
            CourtHeading(new Rect(69, 27, sideBattle ? 360 : 225, 44), dungeon?.Name ?? "Castle Gate", sideBattle ? 34 : 29, TextAnchor.MiddleLeft);

            float actionWidth = dungeon == null ? 42 : 84;
            var action = new Rect(width - actionWidth - 12, 12, actionWidth, 38);
            float walletWidth = sideBattle ? 190 : 135;
            var wallet = new Rect(action.x - walletWidth - 6, 13, walletWidth, 34);
            ManuscriptFade(wallet, new Color32(9, 22, 33, 112), new Color32(9, 22, 33, 194));
            DrawWallet(new Rect(wallet.x + 6, wallet.y + 2, wallet.width - 12, wallet.height - 4));
            if (dungeon != null)
            {
                if (GUI.Button(action, "Retreat", button)) battle.RetreatFromDungeon();
            }
            else if (GUI.Button(action, "•••", button))
                battleOptionsOpen = !battleOptionsOpen;
            DrawTimer(sideBattle ? new Rect(wallet.x - 98, 13, 92, 34) : new Rect(width - 112, 55, 100, 33));
            GUI.EndGroup();
        }

        private void DrawBattleOptions(int width)
        {
            if (!battleOptionsOpen) return;
            GUI.enabled = !battle.Finished;
            if (GUI.Button(new Rect(width - 198, 57 + StageHeaderOffset, 186, 44), "Test: force defeat", button))
            {
                battle.ForceTimeout();
                battleOptionsOpen = false;
            }
            GUI.enabled = true;
        }

        private void DrawUnitHealth(TrialBattle.Unit unit, float x, float y)
        {
            if (unit == battle.ActiveBoss) return;
            float top = unit.Ally && !sideBattle ? y + 50 : y - (sideBattle ? 67 : 64);
            DrawHp(new Rect(x - 25, top, 50, 5), unit);
        }
        private void DrawBattlefield(Rect area)
        {
            if (battle.ActiveDungeon != null)
            {
                var scenery = battle.ActiveDungeon.Kind == TrialData.DungeonKind.GoldVault ? goldVault : experienceArchive;
                // Center the alcove and keep every lane below the wall in both camera orientations.
                float aspect = area.width / area.height * scenery.height / scenery.width;
                float height = Mathf.Min(sideBattle ? 1f : 0.94f, 1f / aspect);
                float width = height * aspect;
                float bottom = sideBattle ? (1f - height) / 2f : 0f;
                GUI.DrawTextureWithTexCoords(area, scenery, new Rect((1f - width) / 2f, bottom, width, height));
                return;
            }
            // Show the full gate in landscape and keep the threshold above the rear portrait lane.
            float aspectRatio = area.width / area.height * courtyard.height / courtyard.width;
            float visibleHeight = sideBattle ? Mathf.Min(1, 1f / aspectRatio) : 0.93f;
            float visibleWidth = visibleHeight * aspectRatio;
            GUI.DrawTextureWithTexCoords(area, courtyard,
                new Rect(Mathf.Clamp(0.22f - visibleWidth / 2, 0, 1f - visibleWidth), 0f, visibleWidth, visibleHeight));
        }

        private void DrawHp(Rect area, TrialBattle.Unit unit)
        {
            GUI.DrawTexture(area, hpTrack);
            float hp = Mathf.Clamp01(unit.Hp / unit.MaxHp);
            if (hp <= 0) return;

            // The 200 x 16 track leaves a 4 x 3 pixel inset around its fill.
            var fill = new Rect(area.x + area.width * 0.02f, area.y + area.height * 3 / 16,
                area.width * 0.96f * hp, area.height * 10 / 16);
            GUI.DrawTextureWithTexCoords(fill, unit.Ally ? allyHp : enemyHp, new Rect(0, 0, hp, 1));
        }

        private void DrawPartyStrip(Rect area)
        {
            float width = (area.width - 12) / 3;
            for (int slot = 0; slot < 3; slot++)
            {
                var card = new Rect(area.x + (width + 6) * slot, area.y, width, area.height);
                var hero = battle.Units[slot];
                int index = hero.HeroIndex;
                if (index < 0) continue;
                var role = hero.Stats.Role;
                Color accent = RoleAccent(role);
                Fill(card, new Color32(115, 108, 88, 255));
                var art = new Rect(card.x + 1, card.y + 1, card.width - 2, card.height - 2);
                ManuscriptFade(art, Color.Lerp(HudInk, accent, 0.27f), HudInk);
                DrawKnightPortrait(art, index, hero.Alive ? Color.white : Color.gray);
                if (hero.LastSkill != null && battle.Elapsed - hero.LastSkillTime < 1f)
                {
                    var skill = new Rect(art.x, art.y, art.width, 23);
                    Fill(skill, new Color32(12, 28, 41, 235));
                    CourtGem(new Vector2(skill.x + 10, skill.center.y), 5, HudAmber);
                    ArcadeText(new Rect(skill.x + 21, skill.y, skill.width - 24, skill.height),
                        hero.LastSkill, 13, Parchment, TextAnchor.MiddleLeft);
                }
                else
                {
                    var level = new Rect(card.x + 4, card.y + 4, 32, 19);
                    Fill(level, new Color32(11, 23, 31, 204));
                    ArcadeText(level, "Lv." + hero.Level, 11, Parchment, TextAnchor.MiddleCenter);
                    if (role != TrialData.Role.None)
                        Picture("RoleUI/" + RoleIcons[(int)role - 1], card.xMax - 26, card.y + 4, 21, 21);
                }
                var caption = new Rect(art.x, art.yMax - 48, art.width, 48);
                ManuscriptFade(caption, new Color32(10, 19, 28, 0), new Color32(8, 16, 25, 255));
                CourtHeading(new Rect(card.x + 7, card.yMax - 32, card.width - 14, 23), hero.Name, sideBattle ? 16 : 18, TextAnchor.MiddleLeft);
                var health = new Rect(card.x + 8, card.yMax - 8, card.width - 16, 3);
                Fill(health, new Color32(46, 62, 65, 255));
                Fill(new Rect(health.x, health.y, health.width * Mathf.Clamp01(hero.Hp / hero.MaxHp), 3), new Color32(142, 204, 173, 255));
                if (battle.ActiveDungeon == null && GUI.Button(card, GUIContent.none, GUIStyle.none))
                {
                    battleOptionsOpen = false;
                    OpenHeroes();
                    featureHero = index;
                    selectedSkill = -1;
                    heroView = HeroView.Info;
                }
            }
        }

        private static readonly string[] BattleDockTitles = { "Heroes", "Formation", "Summon", "Hall" };

        private void DrawBattleDock(Rect area)
        {
            float width = area.width / BattleDockTitles.Length;
            for (int i = 0; i < BattleDockTitles.Length; i++)
            {
                var item = new Rect(area.x + i * width, area.y, width, area.height);
                float size = sideBattle ? 50 : 48;
                var seal = new Rect(item.center.x - size / 2, item.center.y - size / 2 - 11, size, size);
                GUI.DrawTexture(seal, coatMedallion);
                DrawCourtEmblem(new Rect(seal.x + 10, seal.y + 10, size - 20, size - 20), i == 3 ? 6 : i, HudAmber);
                CourtHeading(new Rect(item.x, seal.yMax + 3, width, 24), BattleDockTitles[i], 16, TextAnchor.MiddleCenter);
                if (!GUI.Button(item, GUIContent.none, GUIStyle.none)) continue;
                battleOptionsOpen = false;
                if (i == 0) OpenHeroes();
                else
                {
                    OpenMenu();
                    if (i != 3) OpenFeature(i - 1);
                }
            }
        }

        private void DrawTimer(Rect area)
        {
            ManuscriptFade(area, new Color32(9, 22, 33, 112), new Color32(9, 22, 33, 194));
            float remaining = Mathf.Max(0, battle.Limit - battle.Elapsed);
            var accent = remaining > 5 ? HudAmber : enemyAccent;
            DrawCourtEmblem(new Rect(area.x + 6, area.y + 6, 20, 20), 5, accent);
            ArcadeText(new Rect(area.x + 30, area.y, area.width - 35, area.height),
                remaining.ToString("F1") + "s", 16, Parchment, TextAnchor.MiddleCenter);
            Fill(new Rect(area.x, area.yMax - 1, area.width * remaining / battle.Limit, 1), accent);
        }
        private void DrawWallet(Rect area)
        {
            float cell = area.width / 2;
            DrawAmount(new Rect(area.x, area.y, cell, area.height), goldCoin, battle.Gold);
            DrawAmount(new Rect(area.x + cell, area.y, cell, area.height), experienceCrystal, battle.Experience);
        }

        private void DrawAmount(Rect area, Texture2D icon, int value)
        {
            float size = Mathf.Min(26, area.height);
            GUI.DrawTexture(new Rect(area.x, area.center.y - size / 2, size, size), icon);
            var labelArea = new Rect(area.x + size + 3, area.y, area.width - size - 3, area.height);
            string amount = value.ToString("N0");
            if (value >= 1000 && HudText.CalcSize(new GUIContent(amount)).x > labelArea.width)
                amount = value >= 1000000000 ? (value / 1e9f).ToString("0.#") + "B"
                    : value >= 1000000 ? (value / 1e6f).ToString("0.#") + "M" : (value / 1000f).ToString("0.#") + "K";
            GUI.Label(labelArea, amount, HudText);
        }

        private void DrawBattleRetry(Rect area)
        {
            GUI.enabled = battle.Farming;
            int target = battle.Next - 1;
            string targetName = target < 0 ? "Practice" : (target / battle.Data.RoundLength + 1) + "-" + (target % battle.Data.RoundLength + 1);
            if (GUI.Button(area, "Retry " + targetName, knightSelectedButton))
            {
                battle.Retry();
                accumulator = restartDelay = 0;
            }
            GUI.enabled = true;
        }

        private void DrawDungeonSpoils(Rect area)
        {
            DrawKnightPanel(area);
            var dungeon = battle.ActiveDungeon;
            bool gold = dungeon.Kind == TrialData.DungeonKind.GoldVault;
            GUI.Label(new Rect(area.x + 15, area.y, 155, area.height), "Victory reward", HudText);
            DrawAmount(new Rect(area.x + 169, area.y, area.width - 184, area.height),
                gold ? goldCoin : experienceCrystal, gold ? dungeon.Gold : dungeon.Experience);
        }

        private void DrawBattleStatus(Rect area)
        {
            var content = new Rect(area.x + 2, area.y, area.width - 4, area.height);
            if (battle.Finished)
            {
                DrawResult(content);
                return;
            }
            if (battle.ActiveDungeon != null)
            {
                int enemies = 0;
                foreach (var unit in battle.Units)
                    if (!unit.Ally && unit.Alive) enemies++;
                GUI.Label(content, $"Defeat all enemies   /   {enemies} remaining", HudText);
                return;
            }
            int interval = battle.Data.BossEvery;
            int inCycle = battle.ActiveStage > 0 ? (battle.ActiveStage - 1) % interval + 1 : 0;
            int remaining = interval - inCycle;
            string objective = battle.BossStage ? "Defeat the boss" : inCycle == 0 ? "Practice battle" :
                remaining == 1 ? "Boss in 1 stage" : $"Boss in {remaining} stages";
            GUI.DrawTexture(new Rect(content.x, content.center.y - 13, 26, 26), bossMark, ScaleMode.ScaleToFit);
            CourtHeading(new Rect(content.x + 32, content.y, 156, content.height), objective, 17, TextAnchor.MiddleLeft);
            float trackX = content.x + 186;
            float trackEnd = content.xMax - 5;
            float trackWidth = trackEnd - trackX;
            Fill(new Rect(trackX, content.center.y, trackWidth, 1), bronzeRule);
            if (inCycle > 0)
                Fill(new Rect(trackX, content.center.y, trackWidth * inCycle / interval, 1), HudAmber);
            if (interval <= 12)
                for (int i = 1; i <= interval; i++)
                    CourtGem(new Vector2(trackX + trackWidth * i / interval, content.center.y), 4.5f,
                        i <= inCycle ? HudAmber : new Color32(142, 142, 130, 255));
        }
        private void DrawResult(Rect area)
        {
            if (battle.Finished)
            {
                float size = Mathf.Min(44, area.height);
                GUI.DrawTexture(new Rect(area.x, area.center.y - size / 2, size, size),
                    battle.Won ? victoryMark : defeatMark);
                area.xMin += size + 6;
            }
            if (battle.Won)
            {
                GUI.Label(new Rect(area.x, area.y, 82, area.height), "VICTORY", HudText);
                float cell = (area.width - 82) / (battle.TicketReward > 0 ? 3 : 2);
                DrawAmount(new Rect(area.x + 82, area.y, cell, area.height), goldCoin, battle.GoldReward);
                DrawAmount(new Rect(area.x + 82 + cell, area.y, cell, area.height), experienceCrystal, battle.ExperienceReward);
                if (battle.TicketReward > 0)
                    DrawAmount(new Rect(area.x + 82 + 2 * cell, area.y, cell, area.height),
                        FeatureTexture("SummonUI/SummonTicket"), battle.TicketReward);
                return;
            }
            GUI.Label(area, battle.Result, HudText);
        }
    }
}
