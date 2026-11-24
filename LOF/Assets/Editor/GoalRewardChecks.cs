using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AFKTrial.Editor
{
    [InitializeOnLoad]
    public static class GoalRewardChecks
    {
        static GoalRewardChecks() { EditorApplication.update += Poll; }
        static void Poll()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlaying) return;
            if (!File.Exists("Logs/GoalRewardChecks.request")) return;
            File.Delete("Logs/GoalRewardChecks.request"); Run();
        }
        [MenuItem("AFK Trial/Check Goal Rewards")]
        public static void Run()
        {
            try
            {
                var data = TrialData.Load();
                var b = new TrialBattle(data);
                Require(!b.ClaimGoalReward(0, p => true), "incomplete goals cannot claim");
                var old = b.CaptureProgress();
                old.completedGoals = TrialBattle.GoalMask;
                old.highest = data.RoundLength;
                old.stage = old.highest + 1; old.farming = false;
                // Old saves have no reward fields and can collect their first reward once.
                Require(b.RestoreProgress(old), "legacy completed save loads");
                int tickets = b.Tickets;
                Require(!b.ClaimGoalReward(0, p => false) && b.Tickets == tickets && !b.IntroGoalRewardClaimed, "save failure leaves reward unclaimed");
                TrialBattle.Progress saved = null;
                Require(b.ClaimGoalReward(0, p => { saved = p; return true; }), "initial claim");
                Require(b.Tickets == tickets + 10 && saved.tickets == b.Tickets, "exactly ten tickets persisted");
                Require(!b.ClaimGoalReward(0, p => true), "initial reward cannot repeat");
                Require(b.PendingBossGoals == 0 && b.PendingRoundGoals == 0, "earlier milestones are not repeat rewards");
                var restored = new TrialBattle(data);
                Require(restored.RestoreProgress(JsonUtility.FromJson<TrialBattle.Progress>(JsonUtility.ToJson(saved))), "reward state survives serialization");
                Require(!restored.ClaimGoalReward(0, p => true), "reload does not duplicate intro");
                saved.highest += data.RoundLength; saved.stage = saved.highest + 1;
                Require(restored.RestoreProgress(saved), "later progression loads");
                int bosses = restored.PendingBossGoals;
                Require(bosses > 0 && restored.PendingRoundGoals == 1, "boss and round missions advance independently");
                tickets = restored.Tickets;
                Require(!restored.ClaimGoalReward(1, p => false) && restored.PendingBossGoals == bosses, "repeat save failure retries safely");
                for (int i = 0; i < bosses; i++) Require(restored.ClaimGoalReward(1, p => { saved = p; return true; }), "one ticket per boss milestone");
                Require(!restored.ClaimGoalReward(1, p => true), "boss reward cannot duplicate");
                Require(restored.ClaimGoalReward(2, p => { saved = p; return true; }), "one ticket for round milestone");
                Require(restored.Tickets == tickets + bosses + 1, "exact repeat payout");
                Require(restored.RestoreProgress(saved) && restored.PendingBossGoals == 0 && restored.PendingRoundGoals == 0, "reload preserves claimed milestones");
                saved.goalBossClaimed = int.MaxValue;
                Require(!restored.RestoreProgress(saved), "invalid reward counters rejected");
                File.WriteAllText("Logs/GoalRewardChecks.txt", "PASS\nInitial ten tickets; repeat boss/round one ticket each; legacy saves; serialization; duplicate prevention; failed-save rollback; invalid counters.\n" + DateTime.UtcNow.ToString("O"));
                Debug.Log("[AFK Trial] Goal reward checks PASS");
            }
            catch (Exception e) { File.WriteAllText("Logs/GoalRewardChecks.txt", "FAIL\n" + e); Debug.LogException(e); }
        }
        static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
