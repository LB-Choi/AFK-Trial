using UnityEngine;

namespace AFKTrial
{
    public sealed partial class TrialRunner
    {
        private static readonly string[] GoalTitles = {
            "영웅 첫 레벨업", "장비 첫 장착", "첫 보스 처치",
            "스킬 첫 강화", "재화 던전 첫 클리어", "첫 회차 돌파"
        };
        private string goalRewardNotice;
        private string GoalHint(int goal)
        {
            switch (goal)
            {
                case 0: return "영웅 → 영웅 선택 → 레벨업";
                case 1: return "영웅 → 영웅 선택 → 장비 슬롯 → 장착";
                case 2: return "메인 " + battle.Data.BossEvery + "스테이지를 클리어하세요.";
                case 3: return "영웅을 레벨업한 뒤 스킬을 선택해 강화하세요.";
                case 4: return "첫 보스 처치 후 홀 → 던전 → 쉬움에 도전하세요.";
                default: return "메인 " + battle.Data.RoundLength + "스테이지를 클리어하세요.";
            }
        }
        private void DrawGoalTracker(Rect area)
        {
            int current = battle.CurrentGoal;
            string caption;
            if (battle.IntroGoalRewardClaimed)
                caption = battle.PendingBossGoals + battle.PendingRoundGoals > 0 ? "성장 목표 · 소환권 보상 받기  ›" : "반복 목표 · 보스 처치 / 회차 돌파  ›";
            else
                caption = (current < 0 ? "성장 목표 · 소환권 10장 받기" : "목표 · " + GoalTitles[current]) +
                    "  (" + battle.CompletedGoalCount + "/6)  ›";
            if (GUI.Button(area, caption, new GUIStyle(button) { fontSize = 15 }) && CanOpenSystemMenu)
            { goalRewardNotice = null; systemMenu = SystemMenu.Goals; }
        }
        private void ClaimGoal(int kind)
        {
            goalRewardNotice = battle.ClaimGoalReward(kind, SaveMenuPurchase)
                ? "소환권 " + (kind == 0 ? 10 : 1) + "장을 받았습니다."
                : "보상을 받지 못했습니다. 저장 상태와 달성 조건을 확인해 주세요.";
        }
        private void DrawGoals(int width, int height)
        {
            Fill(new Rect(0, 0, width, height), new Color(.015f, .025f, .04f, .86f));
            const float w = 496, h = 500;
            GUI.BeginGroup(new Rect((width - w) / 2, (height - h) / 2, w, h));
            DrawKnightPanel(new Rect(0, 0, w, h));
            CourtHeading(new Rect(20, 12, w - 40, 36), battle.IntroGoalRewardClaimed ? "반복 성장 목표" : "성장 목표 · " + battle.CompletedGoalCount + " / 6", 27, TextAnchor.MiddleLeft);
            if (!battle.IntroGoalRewardClaimed)
            {
                GUI.Label(new Rect(20, 50, w - 40, 46), "목표 6개를 모두 달성하면 소환권 10장!\n보상 수령 후 반복 성장 목표가 열립니다.", small);
                for (int i = 0; i < TrialBattle.GoalCount; i++)
                {
                    float y = 100 + i * 49;
                    bool done = battle.GoalComplete(i);
                    ArcadeText(new Rect(20, y, w - 40, 24), (done ? "달성" : "미달성") + " · " + GoalTitles[i] + "   " +
                        battle.GoalProgress(i) + "/" + battle.GoalTarget(i), 18, done ? HallAmber : HallLight, TextAnchor.MiddleLeft);
                    GUI.Label(new Rect(20, y + 24, w - 40, 23), GoalHint(i), small);
                }
                bool enabled = GUI.enabled;
                GUI.enabled = enabled && battle.CanClaimIntroGoalReward;
                if (GUI.Button(new Rect(20, 399, w - 40, 34), "최초 달성 보상 · 소환권 10장 받기", button)) ClaimGoal(0);
                GUI.enabled = enabled;
            }
            else
            {
                GUI.Label(new Rect(20, 50, w - 40, 46), "최초 달성 보상 수령 완료 · 소환권 10장\n새 보스 처치와 회차 돌파마다 각각 소환권 1장", small);
                DrawRepeatGoal(110, "보스 처치 · 메인 " + battle.NextGoalBossStage + "스테이지", battle.PendingBossGoals, 1);
                DrawRepeatGoal(245, "회차 돌파 · " + battle.NextGoalRound + "회차 완료", battle.PendingRoundGoals, 2);
                GUI.Label(new Rect(20, 375, w - 40, 48), "보상 수령 시 다음 목표로 갱신됩니다.\n이미 클리어한 스테이지 반복은 목표에 포함되지 않습니다.", small);
            }
            if (!string.IsNullOrEmpty(goalRewardNotice)) GUI.Label(new Rect(20, 434, w - 40, 26), goalRewardNotice, small);
            if (GUI.Button(new Rect(148, 462, 200, 30), "닫기 / 전투 재개", button)) BackFromSystemMenu();
            GUI.EndGroup();
        }
        private void DrawRepeatGoal(float y, string title, int pending, int kind)
        {
            ArcadeText(new Rect(20, y, 456, 28), title, 20, HallLight, TextAnchor.MiddleLeft);
            GUI.Label(new Rect(20, y + 32, 456, 24), pending > 0 ? "달성 · 수령 가능한 보상 " + pending + "회" : "진행 중 · 현재 메인 " + battle.Highest + "스테이지 클리어", small);
            bool enabled = GUI.enabled;
            GUI.enabled = enabled && pending > 0;
            if (GUI.Button(new Rect(20, y + 65, 456, 34), "소환권 1장 받기", button)) ClaimGoal(kind);
            GUI.enabled = enabled;
        }
    }
}
