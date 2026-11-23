using System;
using System.IO;
using System.Reflection;
using UnityEngine;
namespace AFKTrial.Editor
{
    internal static class TrialLifecycleChecks
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        static void Require(bool ok, string text) { if (!ok) throw new Exception("Lifecycle: " + text); }
        public static void Check(TrialData data)
        {
            string folder = Path.Combine(Application.temporaryCachePath, "afk-lifecycle-" + Guid.NewGuid());
            var host = new GameObject("Isolated lifecycle checks"); host.SetActive(false);
            var runner = host.AddComponent<TrialRunner>();
            void Set(string key, object value) => typeof(TrialRunner).GetField(key, Flags).SetValue(runner, value);
            object Get(string key) => typeof(TrialRunner).GetField(key, Flags).GetValue(runner);
            object Call(string key, params object[] args) => typeof(TrialRunner).GetMethod(key, Flags).Invoke(runner, args);
            try
            {
                var battle = new TrialBattle(data);
                foreach (TrialBattle.Guide guide in Enum.GetValues(typeof(TrialBattle.Guide))) battle.CompleteGuide(guide, true);
                Set("battle", battle); Call("OpenProgressSessionAt", folder);
                long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                battle.Tick(.2f); float elapsed = battle.Elapsed;
                Set("lastSeenUtc", now - 600);
                Call("OnApplicationFocus", false);
                Require(!(bool)Get("suspended") && (long)Get("pendingSeconds") == 0, "focus alone is not absence");
                Require((bool)Call("SetSuspension", true, now), "enter absence");
                Require(!(bool)Call("SetSuspension", true, now + 20), "duplicate pause ignored");
                Set("applicationPaused", true); Call("Update");
                Require(battle.Elapsed == elapsed, "combat stops while away");
                Set("applicationPaused", false); Call("SetSuspension", false, now + 60);
                Require((long)Get("pendingSeconds") == 60, "only actual absence counted; preceding menu time excluded");
                Require(!(bool)Call("SetSuspension", false, now + 80) && (long)Get("pendingSeconds") == 60, "duplicate resume ignored");
                int gold = battle.Gold;
                string path = Path.Combine(folder, "progress.json");
                using (var locked = new FileStream(path + ".tmp", FileMode.Create, FileAccess.Write, FileShare.None))
                    Call("ConfirmIdleReward");
                Require(battle.Gold == gold && (long)Get("pendingSeconds") == 60, "failed claim preserves pending time and wallet");
                Call("ConfirmIdleReward");
                Require(battle.Gold > gold && (long)Get("pendingSeconds") == 0, "claim pays and clears atomically");
                gold = battle.Gold; Call("ConfirmIdleReward");
                Require(battle.Gold == gold, "claim twice cannot pay twice");
                var unit = battle.Units[0]; Call("ResumeBattleAfterAway");
                Require(battle.Elapsed == 0 && battle.Units[0] != unit, "resume starts a fresh encounter");
                // Closing while minimized retains the original boundary for the next session.
                long boundary = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 90;
                Set("lastSeenUtc", boundary); Set("suspended", true);
                Call("CloseProgressSession");
                Call("OpenProgressSessionAt", folder);
                long pending = (long)Get("pendingSeconds");
                Require(pending >= 90 && pending < 95, "reconnect accounts for minimized interval");
                Call("CloseProgressSession"); Call("OpenProgressSessionAt", folder);
                Require((long)Get("pendingSeconds") >= pending && (long)Get("pendingSeconds") < pending + 5, "unclaimed reconnect does not double count");
                Call("ConfirmIdleReward"); gold = battle.Gold;
                Call("CloseProgressSession"); Call("OpenProgressSessionAt", folder);
                Require(battle.Gold == gold, "claimed balance persists without repayment");
                // No absence transition while settings, guides or result screens are visible.
                Set("lastSeenUtc", DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 300);
                Call("CheckpointProgress", false);
                Call("CountTimeAway", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                Require((long)Get("pendingSeconds") < 5, "visible paused-screen time is not idle time");
                var p = battle.CaptureProgress(); p.highest = data.BossEvery; p.stage = p.highest + 1; p.farming = false;
                Require(battle.RestoreProgress(p) && battle.TryStartDungeon(TrialData.DungeonKind.GoldVault, 0), "dungeon fixture");
                var dungeon = battle.ActiveDungeon; battle.Tick(.2f); Set("restartAfterAway", true); Call("ResumeBattleAfterAway");
                Require(battle.ActiveDungeon == dungeon && battle.Elapsed == 0, "dungeon restarts same difficulty");
                battle.ForceTimeout(); Set("restartAfterAway", true); Call("ResumeBattleAfterAway");
                Require(battle.Finished && battle.ActiveDungeon == dungeon, "completed result preserved");
            }
            finally
            {
                Call("CloseProgressSession"); UnityEngine.Object.DestroyImmediate(host);
                if (Directory.Exists(folder)) Directory.Delete(folder, true);
            }
        }
    }
}
