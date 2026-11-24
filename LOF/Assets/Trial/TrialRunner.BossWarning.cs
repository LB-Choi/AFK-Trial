using UnityEngine;

namespace AFKTrial
{
    public sealed partial class TrialRunner
    {
        private static string BossTechnique(TrialBattle.Unit boss) => boss.Stats.Id == "boss" ? "충격 강타" : "출혈 베기";

        private void DrawBossWarning(TrialBattle.Unit unit, float x, float y)
        {
            if (unit != battle.ActiveBoss || !unit.Alive || battle.Finished || !unit.SpecialAttackPending) return;
            float pulse = .5f + .5f * Mathf.Sin((battle.Elapsed - unit.SpecialWarningStarted) * 9f);
            Color glow = Color.Lerp(new Color(1f, .4f, .16f, .65f), HallAmber, pulse);
            float radius = 44 + pulse * 8;
            // Battle time drives the pulse, so menus and suspension also freeze the warning.
            CourtGem(new Vector2(x, y - 72), 10 + pulse * 4, glow);
            CourtRule(new Rect(x - radius, y + 47, radius * 2, 3), glow);
            var banner = new Rect(x - 104, y - 112, 208, 30);
            Fill(banner, new Color(.08f, .025f, .015f, .94f));
            CourtRule(new Rect(banner.x, banner.y, banner.width, 2), glow);
            ArcadeText(banner, "다음 공격 · " + BossTechnique(unit), 16, HallLight, TextAnchor.MiddleCenter);
        }

        private void DrawBossInformation(int width, int height)
        {
            Fill(new Rect(0, 0, width, height), new Color(.01f, .02f, .03f, .86f));
            const float w = 580, h = 370;
            GUI.BeginGroup(new Rect((width - w) / 2, (height - h) / 2, w, h));
            DrawKnightPanel(new Rect(0, 0, w, h));
            var boss = battle.ActiveBoss;
            CourtHeading(new Rect(22, 14, w - 44, 36), "보스 기술 안내", 26, TextAnchor.MiddleLeft);
            if (boss != null)
            {
                int round = (battle.ActiveStage - 1) / battle.Data.RoundLength + 1;
                string effect = boss.Stats.Id == "boss"
                    ? "기본 공격 피해의 " + round + "배 + 기절 0.3초"
                    : "기본 공격 피해 + 출혈 " + round + "초\n출혈은 1초마다 대상의 현재 체력 0.1% 피해를 줍니다.";
                string text = boss.Name + " · " + BossTechnique(boss) + "\n\n" +
                    "기본 공격 " + (boss.Stats.SkillEvery - 1) + "회 → 공격을 쉬며 예고 → 다음 공격에 특수 공격\n\n" +
                    effect + "\n\n예고 중에는 새 공격 피해나 상태 이상을 주지 않습니다.\n" +
                    "기절·이동 등으로 실제 공격이 늦어질 수 있습니다. 피해에는 전투 보정이 적용됩니다.";
                GUI.Label(new Rect(22, 62, w - 44, 235), text,
                    new GUIStyle(small) { fontSize = 17, wordWrap = true });
            }
            if (GUI.Button(new Rect(170, h - 52, 240, 36), "닫기 / 전투 재개", button)) BackFromSystemMenu();
            GUI.EndGroup();
        }
    }
}
