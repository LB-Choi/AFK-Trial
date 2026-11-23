using UnityEngine;

namespace AFKTrial
{
    public sealed partial class TrialRunner
    {
        private Vector2 summonInfoScroll;
        private void DrawSummonInformation(Rect area)
        {
            Fill(area, new Color(.025f, .05f, .08f, .86f));
            CourtHeading(new Rect(22, 12, area.width - 44, 36), "소환 확률 · 신규 영웅 보장", 24, TextAnchor.MiddleLeft);
            string progress = battle.MissingSummonHeroes == 0
                ? "모든 영웅 보유: 신규 보장 종료 / 일반 확률로 소환"
                : $"연속 중복 {battle.SummonDuplicateStreak}/9회 · 신규 보장까지 최대 {10 - battle.SummonDuplicateStreak}회";
            GUI.Label(new Rect(24, 55, area.width - 48, 30), progress, label);
            var style = new GUIStyle(small) { wordWrap = true, fontSize = 14 };
            string rules = "소환권 1장당 1회 · 10회 소환은 1회씩 순서대로 처리합니다.\n" +
                "신규 영웅을 얻으면 연속 중복 횟수가 초기화됩니다. 9회 연속 중복이면 다음 1회는 미보유 영웅 중 균등 확률로 보장합니다.\n" +
                "중복 영웅은 레벨이 1 오릅니다(최대 레벨 제외). 아래 기본 확률은 보장 소환에는 적용되지 않습니다.";
            float ruleHeight = style.CalcHeight(new GUIContent(rules), area.width - 48);
            GUI.Label(new Rect(24, 91, area.width - 48, ruleHeight), rules, style);
            float top = 103 + ruleHeight;
            float rowWidth = (area.width - 62) / 2;
            int rows = (battle.Heroes.Count + 1) / 2;
            summonInfoScroll = GUI.BeginScrollView(new Rect(24, top, area.width - 48, area.height - top - 10),
                summonInfoScroll, new Rect(0, 0, area.width - 66, rows * 47));
            for (int i = 0; i < battle.Heroes.Count; i++)
            {
                string guarantee = battle.MissingSummonHeroes == 0 ? "" :
                    battle.Owned(i) ? " / 보장 제외" : $" / 보장 {100d / battle.MissingSummonHeroes:0.##}%";
                GUI.Label(new Rect(i % 2 * rowWidth, i / 2 * 47, rowWidth - 8, 45),
                    $"{battle.Heroes[i]}  {battle.Data.SummonRates[i]:0.####}%\n" +
                    (battle.Owned(i) ? "보유" : "미보유") + guarantee, style);
            }
            GUI.EndScrollView();
        }
    }
}
