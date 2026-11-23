using UnityEngine;

namespace AFKTrial
{
    public sealed partial class TrialRunner
    {
        private static readonly string[] GoalTitles = {
            "영웅 첫 레벨업", "장비 첫 장착", "첫 보스 처치",
            "스킬 첫 강화", "재화 던전 첫 클리어", "첫 회차 돌파"
        };
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
            string caption = current < 0 ? "초반 목표 모두 달성!" : "목표 · " + GoalTitles[current];
            caption += "  (" + battle.CompletedGoalCount + "/" + TrialBattle.GoalCount + ")  ›";
            if (GUI.Button(area, caption, new GUIStyle(button) { fontSize = 15 }) && CanOpenSystemMenu)
                systemMenu = SystemMenu.Goals;
        }
        private void DrawGoals(int width, int height)
        {
            Fill(new Rect(0, 0, width, height), new Color(.015f, .025f, .04f, .86f));
            const float w = 496, h = 500;
            GUI.BeginGroup(new Rect((width - w) / 2, (height - h) / 2, w, h));
            DrawKnightPanel(new Rect(0, 0, w, h));
            CourtHeading(new Rect(20, 12, w - 40, 36), "성장 목표 · " + battle.CompletedGoalCount + " / 6", 27, TextAnchor.MiddleLeft);
            GUI.Label(new Rect(20, 50, w - 40, 46), "순서와 관계없이 달성합니다. 별도 목표 보상은 없습니다.\n달성 기록 자동 저장 · 목록을 보는 동안 전투 일시정지", small);
            for (int i = 0; i < TrialBattle.GoalCount; i++)
            {
                float y = 100 + i * 53;
                bool done = battle.GoalComplete(i);
                string status = done ? "달성" : i == battle.CurrentGoal ? "현재 목표" : "미달성";
                ArcadeText(new Rect(20, y, w - 40, 24), status + " · " + GoalTitles[i] + "   " +
                    battle.GoalProgress(i) + "/" + battle.GoalTarget(i), 18, done ? HallAmber : HallLight, TextAnchor.MiddleLeft);
                GUI.Label(new Rect(20, y + 25, w - 40, 23), GoalHint(i), small);
            }
            GUI.Label(new Rect(20, 423, w - 40, 28), battle.CurrentGoal < 0 ?
                "모두 달성했습니다! 다음 회차와 던전 상위 난이도에 도전하세요." : "상단의 목표 표시를 눌러 언제든 달성 목록을 확인하세요.", small);
            if (GUI.Button(new Rect(148, 462, 200, 30), "닫기 / 전투 재개", button)) BackFromSystemMenu();
            GUI.EndGroup();
        }
    }
}
