using System;
using System.Linq;
using UnityEngine;

namespace AFKTrial
{
    public sealed partial class TrialRunner
    {
        private TrialBattle.BossAttempt bossReview, bossPrevious;
        private bool bossResultPending;
        private int bossHistoryIndex;
        private Vector2 bossComparisonScroll;
        private bool HasBossReview => bossReview != null;

        private void ShowCompletedBoss()
        {
            // Automatic farming/repeat remains unattended; its results are available from Hall.
            if (battle.CompletedBossAttempt == null || battle.ActiveFarming) return;
            bossReview = battle.CompletedBossAttempt.Copy();
            bossPrevious = battle.PreviousBossAttempt?.Copy();
            bossResultPending = true;
            bossComparisonScroll = Vector2.zero;
        }

        private void OpenBossHistory(int index)
        {
            var history = battle.BossHistory;
            if (history.Length == 0) return;
            bossHistoryIndex = Mathf.Clamp(index, 0, history.Length - 1);
            bossReview = history[bossHistoryIndex];
            bossPrevious = history.Take(bossHistoryIndex).LastOrDefault(item => item.stage == bossReview.stage && item.bossId == bossReview.bossId);
            bossResultPending = false;
            bossComparisonScroll = Vector2.zero;
        }

        private void CloseBossReview(bool growth, bool retry)
        {
            if (bossResultPending && !string.IsNullOrEmpty(idleSaveError)) return;
            if (bossResultPending)
            {
                if (retry && !bossReview.won && bossReview.stage == battle.Next) battle.Retry();
                else battle.StartBattle();
                accumulator = restartDelay = 0;
                CheckpointProgress();
            }
            bossReview = bossPrevious = null;
            bossResultPending = false;
            if (growth) OpenMenu();
        }

        private string BossSetupText(string text)
        {
            foreach (var gear in battle.Data.StarterGear) text = text.Replace(gear.Name, GearNameKorean(gear.Name));
            return text;
        }

        private static string BossHpText(TrialBattle.BossAttempt item) =>
            $"{item.remainingHp:0.##} / {item.maxHp:0.##} ({item.remainingHp / item.maxHp * 100:0.0}%)";

        private string BossComparisonSummary()
        {
            if (bossPrevious == null) return "같은 회차·스테이지 보스의 직전 기록이 없습니다. 이번 결과부터 비교합니다.";
            string outcome = (bossPrevious.won ? "승리" : "패배") + " → " + (bossReview.won ? "승리" : "패배");
            if (!Mathf.Approximately(bossPrevious.maxHp, bossReview.maxHp)) return outcome + " / 보스 최대 체력이 달라 직접적인 난이도 비교에 주의하세요.";
            float time = bossReview.seconds - bossPrevious.seconds;
            string timing = bossPrevious.won && bossReview.won
                ? $"처치 시간 {Mathf.Abs(time):0.0}초 " + (time < 0 ? "단축" : time > 0 ? "증가" : "변화 없음")
                : !bossPrevious.won && !bossReview.won
                    ? $"전투 지속 {Mathf.Abs(time):0.0}초 " + (time < 0 ? "감소" : time > 0 ? "증가" : "변화 없음")
                    : "승패가 바뀌어 시간 우열은 판정하지 않습니다";
            float hp = (bossReview.remainingHp / bossReview.maxHp - bossPrevious.remainingHp / bossPrevious.maxHp) * 100;
            return outcome + " / " + timing + $"\n보스 잔여 체력 {Mathf.Abs(hp):0.0}%p " + (hp < 0 ? "감소" : hp > 0 ? "증가" : "변화 없음");
        }

        private void DrawBossReview(int width, int height)
        {
            float w = Mathf.Min(900, width - 24), h = Mathf.Min(506, height - 24);
            Fill(new Rect(0, 0, width, height), new Color(0.01f, .02f, .03f, .85f));
            GUI.BeginGroup(new Rect((width - w) / 2, (height - h) / 2, w, h));
            DrawKnightPanel(new Rect(0, 0, w, h));
            CourtHeading(new Rect(20, 12, w - (bossResultPending ? 40 : 190), 34), "보스 전투 결과 · 재도전 비교", width > 540 ? 26 : 18, TextAnchor.MiddleLeft);
            ArcadeText(new Rect(20, 49, w - 40, 23), StageLabel(bossReview.stage) + " / " + bossReview.bossName, 15, HallAmber, TextAnchor.MiddleLeft);
            if (!bossResultPending)
            {
                GUI.enabled = bossHistoryIndex > 0;
                if (GUI.Button(new Rect(w - 166, 12, 66, 31), "이전", button)) OpenBossHistory(bossHistoryIndex - 1);
                GUI.enabled = bossHistoryIndex < battle.BossHistory.Length - 1;
                if (GUI.Button(new Rect(w - 92, 12, 66, 31), "다음", button)) OpenBossHistory(bossHistoryIndex + 1);
                GUI.enabled = true;
            }
            float nameW = w * .23f, cellW = (w - nameW - 40) / 2;
            ArcadeText(new Rect(nameW, 79, cellW, 23), "직전 도전", 15, BodyInk, TextAnchor.MiddleLeft);
            ArcadeText(new Rect(nameW + cellW, 79, cellW, 23), "이번 도전", 15, HallAmber, TextAnchor.MiddleLeft);
            void Row(float y, string name, string before, string now)
            {
                CourtRule(new Rect(20, y - 2, w - 40, 1), HallLine);
                ArcadeText(new Rect(20, y, nameW - 25, 27), name, 14, BodyInk, TextAnchor.MiddleLeft);
                ArcadeText(new Rect(nameW, y, cellW - 8, 27), before, 14, BodyInk, TextAnchor.MiddleLeft);
                ArcadeText(new Rect(nameW + cellW, y, cellW - 8, 27), now, 14, HallAmber, TextAnchor.MiddleLeft);
            }
            Row(109, "승패", bossPrevious == null ? "기록 없음" : bossPrevious.won ? "승리" : "패배", bossReview.won ? "승리" : "패배");
            Row(141, "전투 시간", bossPrevious == null ? "—" : $"{bossPrevious.seconds:0.0}초", $"{bossReview.seconds:0.0}초");
            Row(173, "보스 잔여 체력", bossPrevious == null ? "—" : BossHpText(bossPrevious), BossHpText(bossReview));
            GUI.Label(new Rect(20, 208, w - 40, 43), BossComparisonSummary(), small);
            ArcadeText(new Rect(20, 251, w - 40, 23), "전투 시작 시 편성·성장 기록 (진행 중 변경은 다음 도전에 반영)", 14, HallAmber, TextAnchor.MiddleLeft);
            float inner = w - 56, column = inner / 2;
            var textStyle = new GUIStyle(small) { wordWrap = true, fontSize = 13 };
            var rows = new string[6];
            string[] slots = { "아래 후열", "중앙 전열", "위 후열" };
            float total = 0;
            var heights = new float[3];
            for (int i = 0; i < 3; i++)
            {
                rows[i * 2] = slots[i] + " / " + (bossPrevious == null ? "기록 없음" : bossPrevious.party[i] + "\n" + BossSetupText(bossPrevious.setup[i]));
                rows[i * 2 + 1] = slots[i] + " / " + bossReview.party[i] + "\n" + BossSetupText(bossReview.setup[i]);
                heights[i] = Mathf.Max(textStyle.CalcHeight(new GUIContent(rows[i * 2]), column - 12), textStyle.CalcHeight(new GUIContent(rows[i * 2 + 1]), column - 12)) + 16;
                total += heights[i];
            }
            bossComparisonScroll = GUI.BeginScrollView(new Rect(20, 280, w - 40, h - 365), bossComparisonScroll, new Rect(0, 0, inner, total));
            float y = 0;
            for (int i = 0; i < 3; i++)
            {
                GUI.Label(new Rect(0, y, column - 12, heights[i]), rows[i * 2], textStyle);
                GUI.Label(new Rect(column, y, column - 12, heights[i]), rows[i * 2 + 1], textStyle);
                y += heights[i];
            }
            GUI.EndScrollView();
            bool failed = bossResultPending && !string.IsNullOrEmpty(idleSaveError);
            GUI.Label(new Rect(20, h - 78, w - 40, 23), failed ? "저장 실패: 재저장 후 진행할 수 있습니다." : "같은 조건의 기록을 비교하세요. 승패는 성장 외 전투 상황에도 영향을 받습니다.", small);
            float bw = (w - 56) / 3;
            GUI.enabled = !failed;
            if (GUI.Button(new Rect(20, h - 46, bw, 32), bossResultPending ? "전투 계속" : "닫기", button)) CloseBossReview(false, false);
            if (GUI.Button(new Rect(28 + bw, h - 46, bw, 32), "성장·편성", button)) CloseBossReview(true, false);
            GUI.enabled = true;
            if (failed)
            {
                if (GUI.Button(new Rect(36 + bw * 2, h - 46, bw, 32), "저장 재시도", button)) CheckpointProgress(battle.Won);
            }
            else
            {
                GUI.enabled = bossReview != null && bossResultPending && !bossReview.won && bossReview.stage == battle.Next;
                if (GUI.Button(new Rect(36 + bw * 2, h - 46, bw, 32), "같은 보스 재도전", button)) CloseBossReview(false, true);
                GUI.enabled = true;
            }
            GUI.EndGroup();
        }
    }
}
