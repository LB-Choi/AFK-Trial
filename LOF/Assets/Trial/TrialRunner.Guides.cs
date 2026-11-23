using UnityEngine;

namespace AFKTrial
{
    public sealed partial class TrialRunner
    {
        private int activeGuide = -1;
        private bool guideReplay;
        private double guideNotBefore;
        private bool GuideOpen => activeGuide >= 0;
        private static readonly string[] GuideTitles = {
            "자동 전투와 기사 성장", "첫 레벨업을 할 수 있어요", "획득한 장비를 장착하세요",
            "보스에게 다시 도전하세요", "재화 던전이 열렸어요", "방치 보상을 확인하세요"
        };
        private static readonly string[] GuideTexts = {
            "전투는 자동으로 진행됩니다. 상단에서 스테이지와 남은 시간을 확인하세요.\n\n영웅 메뉴에서 기사를 성장시키고 편성 메뉴에서 출전 기사를 바꿀 수 있습니다.",
            "출전 기사 중 레벨업할 수 있는 기사가 있습니다. 영웅 메뉴에서 골드·경험치 비용과 성장 전후 수치를 확인하세요.\n\n성장 효과는 다음 전투부터 적용됩니다.",
            "새 장비를 획득했습니다. 장비는 획득만으로 적용되지 않습니다. 영웅 정보에서 장비 슬롯을 선택해 장착하세요.\n\n홀의 장비 메뉴에서 가방을 확인할 수 있으며 장착·강화 효과는 다음 전투부터 적용됩니다.",
            "직전 보스 도전이 패배로 끝났습니다. 결과창의 성장·편성으로 기사를 준비한 뒤 재도전할 수 있습니다.\n\n결과를 닫았다면 전장의 재도전을 이용하세요. 홀의 보스 도전 기록에서 시간·잔여 체력·편성의 변화를 다시 비교할 수 있습니다.",
            "첫 보스를 클리어해 골드 금고와 경험치 기록보관소가 열렸습니다. 홀 → 던전에서 필요한 재화를 선택하세요.\n\n쉬움부터 순서대로 해금하며 각 던전·난이도의 최초 승리에는 소환권 1장을 추가로 받습니다.",
            "부재 시간에 따른 방치 보상을 수령했습니다. 복귀할 때 보상창에서 부재 시간과 지급 내역을 확인하고 수령합니다.\n\n홀 → 방치 보상에서 적립 기준을 다시 확인할 수 있습니다. 보상창을 보고 있는 시간에는 추가 적립하지 않습니다."
        };
        private void ResetGuideUI()
        {
            activeGuide = -1; guideReplay = false; guideNotBefore = 0;
        }
        private void UpdateGuides()
        {
            if (atTitle || GuideOpen) return;
            // Existing results always complete first. A short gap also avoids back-to-back guides.
            if (SystemMenuOpen || showRecruit || HasPendingIdleReward || HasDungeonResult ||
                HasBossReview || battle.PendingSummons != null || battle.ActiveDungeon != null ||
                choosingRepeat || pendingSale != null ||
                (editing && featurePage < 0 && heroView != HeroView.Menu && heroView != HeroView.List) ||
                !string.IsNullOrEmpty(idleSaveError))
            { guideNotBefore = System.Math.Max(guideNotBefore, Time.realtimeSinceStartupAsDouble + 2); return; }
            battle.ObserveGuideConditions();
            if (Time.realtimeSinceStartupAsDouble < guideNotBefore) return;
            for (int i = 0; i < GuideTitles.Length; i++)
                if (battle.GuidePending((TrialBattle.Guide)i))
                { activeGuide = i; guideReplay = false; break; }
        }
        private void DismissGuide(bool skip)
        {
            if (!GuideOpen) return;
            if (!guideReplay)
            {
                battle.CompleteGuide((TrialBattle.Guide)activeGuide, skip);
                // Keep the in-memory acknowledgement on failure; normal checkpoints retry it.
                if (progressLock != null) CheckpointProgress();
            }
            activeGuide = -1;
            guideNotBefore = Time.realtimeSinceStartupAsDouble + 8;
        }
        private Rect GuideFocus(int width, int height)
        {
            if (activeGuide == 0 && (!editing || guideReplay)) return new Rect(6, 4, width - 12, 106 + StageHeaderOffset);
            if (editing && !guideReplay)
            {
                int tab = activeGuide == 2 ? 3 : activeGuide == 4 ? 4 : activeGuide == 5 ? 5 : 0;
                float h = HallDockHeight(height > width);
                return new Rect(tab * width / 6f, height - h, width / 6f, h);
            }
            // On the battlefield, highlight the route to the relevant menu without forced navigation.
            if (activeGuide == 3 && battle.Farming && !battle.ActiveRepeat && battle.RepeatStage == 0)
                return new Rect(width - 148, (sideBattle ? 420 : 696) - 43, 134, 40);
            Rect dock = sideBattle ? new Rect(513, 429, width - 524, 102) : new Rect(5, 876, width - 10, 78);
            int entry = activeGuide == 4 || activeGuide == 5 ? 3 : 0;
            return new Rect(dock.x + entry * dock.width / 4, dock.y, dock.width / 4, dock.height);
        }
        private void DrawGuide(int width, int height)
        {
            Rect focus = GuideFocus(width, height);
            foreach (var area in SpotlightDimAreas(width, height, focus)) Fill(area, new Color(.01f, .015f, .025f, .86f));
            CourtRule(new Rect(focus.x, focus.y, focus.width, 2), HallAmber);
            CourtRule(new Rect(focus.x, focus.yMax - 2, focus.width, 2), HallAmber);
            Fill(new Rect(focus.x, focus.y, 2, focus.height), HallAmber);
            Fill(new Rect(focus.xMax - 2, focus.y, 2, focus.height), HallAmber);
            float w = Mathf.Min(464, width - 32), h = 286;
            float y = focus.y < 120 ? 150 : (sideBattle ? Mathf.Min(105, focus.y - h - 20) : 280);
            GUI.BeginGroup(new Rect((width - w) / 2, y, w, h));
            DrawKnightPanel(new Rect(0, 0, w, h));
            CourtHeading(new Rect(20, 12, w - 40, 34), GuideTitles[activeGuide], 23, TextAnchor.MiddleLeft);
            GUI.Label(new Rect(20, 53, w - 40, 165), GuideTexts[activeGuide],
                new GUIStyle(small) { fontSize = 17, wordWrap = true });
            GUI.Label(new Rect(20, 213, w - 40, 22), "전투 일시정지 · 도움말에서 다시 볼 수 있습니다.", small);
            if (GUI.Button(new Rect(20, h - 43, (w - 48) / 2, 31), guideReplay ? "도움말로" : "건너뛰기", button))
                DismissGuide(true);
            if (GUI.Button(new Rect(28 + (w - 48) / 2, h - 43, (w - 48) / 2, 31), "확인", button))
                DismissGuide(false);
            GUI.EndGroup();
        }
        private void DrawGuideList()
        {
            for (int i = 0; i < GuideTitles.Length; i++)
            {
                string state = battle.GuideSkipped((TrialBattle.Guide)i) ? "건너뜀" :
                    battle.GuideSeen((TrialBattle.Guide)i) ? "확인함" : "미확인";
                if (GUI.Button(new Rect(26, 102 + i * 45, 412, 38), GuideTitles[i] + " · " + state, button))
                { activeGuide = i; guideReplay = true; }
            }
        }
    }
}
