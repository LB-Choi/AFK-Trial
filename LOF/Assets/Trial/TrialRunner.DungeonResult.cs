using UnityEngine;

namespace AFKTrial
{
    public sealed partial class TrialRunner
    {
        private TrialData.DungeonTier resultDungeon;
        private bool dungeonWon;
        private int dungeonReward, dungeonTicketReward;
        private bool HasDungeonResult => resultDungeon != null;

        private void ReturnFromDungeon()
        {
            resultDungeon = battle.ActiveDungeon;
            dungeonWon = battle.Won;
            dungeonTicketReward = battle.TicketReward;
            dungeonReward = resultDungeon.Kind == TrialData.DungeonKind.GoldVault
                ? battle.GoldReward : battle.ExperienceReward;

            battle.StartBattle();
            accumulator = restartDelay = 0;
            CheckpointProgress();
        }

        private void DrawDungeonResult(int width, int height)
        {
            bool gold = resultDungeon.Kind == TrialData.DungeonKind.GoldVault;
            bool saveFailed = !string.IsNullOrEmpty(idleSaveError);
            int panelHeight = saveFailed ? 372 : 328;
            Fill(new Rect(0, 0, width, height), new Color(0.015f, 0.025f, 0.04f, 0.66f));
            GUI.BeginGroup(new Rect((width - 420) / 2f, (height - panelHeight) / 2f, 420, panelHeight));
            DrawKnightPanel(new Rect(0, 0, 420, panelHeight));
            var banner = new Rect(4, 4, 412, 94);
            GUI.DrawTexture(banner, gold ? goldVault : experienceArchive, ScaleMode.ScaleAndCrop);
            ManuscriptFade(banner, new Color32(12, 23, 38, 176), new Color32(12, 23, 38, 235));
            GUI.DrawTexture(new Rect(23, 21, 59, 59), dungeonWon ? victoryMark : defeatMark, ScaleMode.ScaleToFit);
            CourtHeading(new Rect(98, 35, 298, 47), dungeonWon ? "던전 승리" : "던전 패배", 31, TextAnchor.MiddleLeft);
            ArcadeText(new Rect(99, 15, 293, 24), resultDungeon.Name + "   /   " + resultDungeon.Difficulty,
                14, FrameLight, TextAnchor.MiddleLeft);
            CourtRule(new Rect(21, 97, 378, 1), FrameGold);
            DrawRewardAmount(210, 109, gold ? goldCoin : experienceCrystal, gold ? "획득 골드" : "획득 경험치", dungeonReward);
            ArcadeText(new Rect(26, 218, 368, 28), dungeonTicketReward > 0
                ? "최초 클리어: 소환권 +" + dungeonTicketReward + "장"
                : "획득 소환권: 0장", 16, FrameLight, TextAnchor.MiddleCenter);
            if (saveFailed)
            {
                GUI.Label(new Rect(26, 247, 368, 44), idleSaveError, HudCentered);
                if (GUI.Button(new Rect(30, panelHeight - 63, 146, 44), "저장 재시도", button)) CheckpointProgress();
            }
            GUI.enabled = !saveFailed;
            bool resume = GUI.Button(new Rect(saveFailed ? 190 : 110, panelHeight - 63, 200, 44),
                "전투 재개", knightSelectedButton);
            GUI.enabled = true;
            GUI.EndGroup();

            if (resume) resultDungeon = null;
        }
    }
}
