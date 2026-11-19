using UnityEngine;

namespace AFKTrial
{
    public sealed partial class TrialRunner
    {
        private Texture2D courtyard, hpTrack, allyHp, enemyHp;
        private Texture2D stagePanel, timerPanel, bossMark, victoryMark, defeatMark;
        private Texture2D goldCoin, experienceCrystal;
        private GUIStyle battleHudText, flankNameText, flankMetaText, flankTitleText;
        private static readonly Color bronzeRule = new Color(0.51f, 0.40f, 0.25f);
        private static readonly Color allyAccent = new Color(0.35f, 0.78f, 0.76f);
        private static readonly Color enemyAccent = new Color(0.86f, 0.48f, 0.43f);
        private static readonly Color progressGold = new Color(0.88f, 0.70f, 0.38f);

        private void LoadBattleArt()
        {
            courtyard = LoadTexture("Battlefield/AlliedCastleGate");
            hpTrack = LoadTexture("BattleUI/HpTrack");
            allyHp = LoadTexture("BattleUI/HpFillAlly");
            enemyHp = LoadTexture("BattleUI/HpFillEnemy");
            stagePanel = LoadTexture("BattleUI/StagePanel");
            timerPanel = LoadTexture("BattleUI/TimerPanel");
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

        private void DrawBattleGround(int width, int height)
        {
            DrawKnightBackdrop(width, height);
            if (flankNameText != null) return;
            flankNameText = new GUIStyle(HudText) { fontSize = 13, fontStyle = FontStyle.Normal };
            flankTitleText = new GUIStyle(HudText) { fontSize = 11, fontStyle = FontStyle.Bold };
            flankMetaText = new GUIStyle(HudText) { fontSize = 12 };
            flankMetaText.normal.textColor = new Color(0.79f, 0.83f, 0.86f);
        }

        private void DrawFlankHeader(float x, bool ally)
        {
            Color accent = ally ? allyAccent : enemyAccent;
            flankTitleText.normal.textColor = accent;
            GUI.Label(new Rect(x + 4, 61, 103, 23), ally ? "ALLIES" : "ENEMIES", flankTitleText);
            Fill(new Rect(x + 4, 86, 99, 1), bronzeRule);
        }

        private void DrawFlankUnit(TrialBattle.Unit unit, int lane)
        {
            float x = unit.Ally ? 8 : 844;
            float y = 92 + (2 - lane) * 108;
            var panel = new Rect(x, y, 108, 100);
            DrawKnightPanel(panel);
            var accent = unit.Ally ? allyAccent : enemyAccent;
            Fill(new Rect(x + 4, y + 9, 2, 18), unit.Alive ? accent : Color.gray);
            Texture2D emblem = deploymentBadges[lane];
            if (unit.Ally && unit.Stats.Role != TrialData.Role.None)
                emblem = FeatureTexture("RoleUI/" + RoleIcons[(int)unit.Stats.Role - 1]);
            bool boss = !unit.Ally && battle.BossStage && lane == battle.Data.BossSlot;
            GUI.DrawTexture(new Rect(x + 10, y + 7, 26, 26), boss ? bossMark : emblem, ScaleMode.ScaleToFit);
            string rank = unit.Ally ? "Lv." + battle.HeroLevel(battle.HeroAt(lane), battle.BattleLevel) :
                boss ? "BOSS" : "FOE";
            flankTitleText.normal.textColor = accent;
            GUI.Label(new Rect(x + 40, y + 8, 61, 24), rank, flankTitleText);
            GUI.Label(new Rect(x + 7, y + 33, 99, 23), unit.Name, flankNameText);
            DrawHp(new Rect(x + 7, y + 59, 94, 10), unit);
            GUI.Label(new Rect(x + 8, y + 74, 92, 18), Mathf.CeilToInt(unit.Hp) + " HP", flankMetaText);
        }

        private void DrawFieldName(Rect area, TrialBattle.Unit unit)
        {
            Fill(area, new Color(0.04f, 0.07f, 0.11f, 0.88f));
            Fill(new Rect(area.x, area.y + 4, 2, area.height - 8), unit.Ally ? allyAccent : enemyAccent);
            GUI.Label(new Rect(area.x + 5, area.y, area.width - 7, 18), unit.Name, flankNameText);
            GUI.Label(new Rect(area.x + 5, area.y + 16, area.width - 7, 17),
                Mathf.CeilToInt(unit.Hp) + " HP", flankMetaText);
        }
        private void DrawBattlefield(Rect area)
        {
            Fill(new Rect(area.x - 2, area.y - 2, area.width + 4, area.height + 4), bronzeRule);
            // Show the full gate in landscape and keep the threshold above the rear portrait lane.
            float visibleHeight = sideBattle ? 1f : 0.8f;
            float visibleWidth = Mathf.Min(1f, visibleHeight * area.width / area.height * courtyard.height / courtyard.width);
            GUI.DrawTextureWithTexCoords(area, courtyard,
                new Rect(0f, 0f, visibleWidth, visibleHeight));
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

        private void DrawStage(Rect area)
        {
            GUI.DrawTexture(area, stagePanel);
            float right = area.xMax - 10;
            if (battle.BossStage)
            {
                float size = area.height - 6;
                GUI.DrawTexture(new Rect(right - size, area.y + 3, size, size), bossMark);
                right -= size + 3;
            }
            float left = area.x + area.width * 0.115f;
            string mode = battle.ActiveFarming ? "FARM" : battle.BossStage ? "BOSS" : "CHALLENGE";
            GUI.Label(new Rect(left, area.y, right - left, area.height),
                battle.StageName(battle.ActiveStage) + " / " + mode, HudText);
        }

        private void DrawTimer(Rect area)
        {
            GUI.DrawTexture(area, timerPanel);
            float left = area.width * 0.115f;
            float remaining = Mathf.Max(0, battle.Limit - battle.Elapsed);
            float trackWidth = area.width - left - 17;
            Fill(new Rect(area.x + left, area.yMax - 8, trackWidth, 2), bronzeRule);
            Fill(new Rect(area.x + left, area.yMax - 8, trackWidth * remaining / battle.Limit, 2),
                remaining > 5 ? allyAccent : enemyAccent);
            GUI.Label(new Rect(area.x + left, area.y, area.width - left - 8, area.height - 5),
                $"{remaining:F1}s    Battle Lv.{battle.BattleLevel}", HudText);
        }
        private void DrawWallet(Rect area)
        {
            float cell = area.width / 3;
            DrawAmount(new Rect(area.x, area.y, cell, area.height), goldCoin, battle.Gold);
            DrawAmount(new Rect(area.x + cell, area.y, cell, area.height), experienceCrystal, battle.Experience);
            GUI.Label(new Rect(area.x + 2 * cell, area.y, cell, area.height), $"Team Lv.{battle.Level}", HudText);
        }

        private void DrawAmount(Rect area, Texture2D icon, int value)
        {
            float size = Mathf.Min(26, area.height);
            GUI.DrawTexture(new Rect(area.x, area.center.y - size / 2, size, size), icon);
            GUI.Label(new Rect(area.x + size + 3, area.y, area.width - size - 3, area.height), value.ToString(), HudText);
        }

        private bool DrawUpgradeButton(Rect area)
        {
            bool pressed = GUI.Button(area, GUIContent.none, button);
            float inset = 12;
            float labelWidth = Mathf.Min(120, area.width * 0.38f);
            float priceWidth = (area.width - 2 * inset - labelWidth) / 2;
            var textArea = new Rect(area.x + inset, area.y, labelWidth, area.height);
            GUI.Label(textArea, "Team level +1", HudText);
            DrawAmount(new Rect(textArea.xMax, area.y, priceWidth, area.height), goldCoin, battle.Cost);
            DrawAmount(new Rect(textArea.xMax + priceWidth, area.y, priceWidth, area.height), experienceCrystal, battle.XpCost);
            return pressed;
        }

        private void DrawTeamUpgrade(Rect area)
        {
            GUI.enabled = battle.Gold >= battle.Cost && battle.Experience >= battle.XpCost;
            if (DrawUpgradeButton(area)) battle.Upgrade();
            GUI.enabled = true;
        }

        private void DrawBattleRetry(Rect area)
        {
            GUI.enabled = battle.Farming;
            if (GUI.Button(area, "Retry " + battle.StageName(battle.Next), button))
            {
                battle.Retry();
                accumulator = restartDelay = 0;
            }
            GUI.enabled = true;
        }

        private void DrawDefeatTest(Rect area)
        {
            GUI.enabled = !battle.Finished;
            bool pressed = GUI.Button(area, GUIContent.none, GUIStyle.none);
            GUI.Label(area, "Test: force defeat", HudCentered);
            Fill(new Rect(area.x + area.width * 0.15f, area.yMax - 2, area.width * 0.7f, 1), bronzeRule);
            if (pressed) battle.ForceTimeout();
            GUI.enabled = true;
        }

        private void DrawBattleStatus(Rect area)
        {
            DrawKnightPanel(area);
            var content = new Rect(area.x + 11, area.y + 2, area.width - 22, area.height - 4);
            if (battle.Finished)
            {
                DrawResult(content);
                return;
            }
            int interval = battle.Data.BossEvery;
            int inCycle = battle.ActiveStage > 0 ? (battle.ActiveStage - 1) % interval + 1 : 0;
            int remaining = interval - inCycle;
            string objective = battle.BossStage ? "Defeat the boss" : inCycle == 0 ? "Practice battle" :
                remaining == 1 ? "Boss in 1 stage" : $"Boss in {remaining} stages";
            GUI.Label(new Rect(content.x, content.y, 185, content.height), objective, HudText);
            float trackX = content.x + 192;
            float trackEnd = content.xMax - 37;
            float trackWidth = trackEnd - trackX;
            Fill(new Rect(trackX, content.center.y - 1, trackWidth, 3), bronzeRule);
            if (inCycle > 0)
                Fill(new Rect(trackX, content.center.y - 1, trackWidth * inCycle / interval, 3), progressGold);
            if (interval <= 12)
                for (int i = 1; i <= interval; i++)
                    Fill(new Rect(trackX + trackWidth * i / interval - 2, content.center.y - 3, 4, 7),
                        i <= inCycle ? progressGold : bronzeRule);
            GUI.DrawTexture(new Rect(content.xMax - 32, content.center.y - 15, 30, 30), bossMark, ScaleMode.ScaleToFit);
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
