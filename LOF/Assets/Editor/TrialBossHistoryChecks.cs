using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace AFKTrial.Editor
{
    internal static class TrialBossHistoryChecks
    {
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Boss history: " + message);
        }
        public static void Check(TrialData data)
        {
            var battle = new TrialBattle(data);
            var initial = battle.CaptureProgress();
            initial.highest = data.BossEvery - 1; initial.stage = data.BossEvery;
            initial.gold = initial.experience = 10000;
            initial.bossHistory = null;
            Require(battle.RestoreProgress(initial), "legacy save without records loads");
            var end = typeof(TrialBattle).GetMethod("EndBattle", BindingFlags.NonPublic | BindingFlags.Instance);
            void Finish(bool won, float fraction)
            {
                battle.ActiveBoss.Hp = battle.ActiveBoss.MaxHp * fraction;
                end.Invoke(battle, new object[] { won });
            }
            int hero = battle.HeroAt(0), level = battle.HeroLevel(hero);
            Require(battle.UpgradeHero(hero), "upgrade during boss combat");
            Finish(false, .6f);
            var first = battle.CompletedBossAttempt;
            Require(!first.won && first.party[0].EndsWith("레벨 " + level) && battle.PreviousBossAttempt == null &&
                Mathf.Approximately(first.remainingHp / first.maxHp, .6f), "first result snapshots starting party and boss HP");
            end.Invoke(battle, new object[] { true });
            Require(battle.BossHistory.Length == 1 && !battle.CompletedBossAttempt.won, "finished encounter records once");
            var restored = new TrialBattle(data);
            Require(restored.RestoreProgress(JsonUtility.FromJson<TrialBattle.Progress>(JsonUtility.ToJson(battle.CaptureProgress()))), "history reloads through save format");
            battle = restored;
            battle.Retry();
            Finish(false, .25f);
            Require(battle.PreviousBossAttempt.remainingHp == first.remainingHp &&
                battle.CompletedBossAttempt.party[0].EndsWith("레벨 " + (level + 1)), "retry compares prior attempt and new growth");
            battle.Retry();
            Finish(true, 0);
            Require(battle.BossHistory.Length == 2 && !battle.PreviousBossAttempt.won && battle.CompletedBossAttempt.won &&
                Mathf.Approximately(battle.PreviousBossAttempt.remainingHp / battle.PreviousBossAttempt.maxHp, .25f), "third result compares immediate previous, not oldest");
            var history = battle.BossHistory;
            history[0].party[0] = "modified";
            Require(battle.BossHistory[0].party[0] != "modified", "history export is detached from model");
            var nextRound = battle.CaptureProgress();
            nextRound.highest = data.RoundLength + data.BossEvery - 1;
            nextRound.stage = nextRound.highest + 1;
            nextRound.farming = false;
            Require(battle.RestoreProgress(nextRound), "next round fixture");
            Finish(false, .5f);
            Require(battle.PreviousBossAttempt == null, "same boss at a different round is not compared");
            var saved = battle.CaptureProgress();
            string before = JsonUtility.ToJson(saved);
            saved.bossHistory[0].remainingHp = float.NaN;
            Require(!battle.RestoreProgress(saved) && JsonUtility.ToJson(battle.CaptureProgress()) == before,
                "malformed history is rejected without changing live state");
            saved = battle.CaptureProgress();
            saved.highest = data.BossEvery; saved.stage = data.BossEvery + 1; saved.farming = false;
            Require(battle.RestoreProgress(saved) && battle.TryStartDungeon(TrialData.DungeonKind.GoldVault, 0), "dungeon fixture");
            int count = battle.BossHistory.Length;
            battle.ForceTimeout();
            Require(battle.BossHistory.Length == count && battle.CompletedBossAttempt == null, "dungeon does not overwrite boss records");
            battle.StartBattle(); battle.ForceTimeout();
            Require(battle.BossHistory.Length == count, "normal stage does not overwrite records");
            battle.StartBattle(); // Farming returns to the previously cleared boss.
            Require(battle.ActiveBoss != null, "farming boss fixture");
            Finish(true, 0);
            Require(battle.CompletedBossAttempt != null, "farming boss still records its result");

            var host = new GameObject("Boss result fixture"); host.SetActive(false);
            string folder = Path.Combine(Application.temporaryCachePath, "afk-boss-" + Guid.NewGuid());
            Directory.CreateDirectory(folder);
            try
            {
                var runner = host.AddComponent<TrialRunner>();
                const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
                typeof(TrialRunner).GetField("battle", flags).SetValue(runner, battle);
                typeof(TrialRunner).GetMethod("ShowCompletedBoss", flags).Invoke(runner, null);
                Require(typeof(TrialRunner).GetField("bossReview", flags).GetValue(runner) == null, "farming does not interrupt automatic progression with popup");
                typeof(TrialRunner).GetMethod("OpenBossHistory", flags).Invoke(runner, new object[] { battle.BossHistory.Length - 1 });
                Require(typeof(TrialRunner).GetField("bossReview", flags).GetValue(runner) != null, "saved results accessible from history");
                Require(battle.BossHistory.All(item => item.party.Length == 3 && item.setup.Length == 3), "every result has three starting formation slots");
                void Set(string name, object value) => typeof(TrialRunner).GetField(name, flags).SetValue(runner, value);
                object Call(string name, params object[] args) => typeof(TrialRunner).GetMethod(name, flags).Invoke(runner, args);
                string path = Path.Combine(folder, "progress.json");
                Set("progressPath", path); Set("victoryBackupPath", Path.Combine(folder, "victory.json"));
                Call("CheckpointProgress", true);
                string persisted = JsonUtility.ToJson(battle.CaptureProgress());
                Call("ReadProgress", path);
                Require(JsonUtility.ToJson(battle.CaptureProgress()) == persisted, "production disk save restores boss history");
                var challenge = battle.CaptureProgress();
                challenge.highest = data.BossEvery * 2 - 1; challenge.stage = challenge.highest + 1; challenge.farming = false;
                Require(battle.RestoreProgress(challenge), "new boss challenge fixture");
                var formation = new[] { battle.HeroAt(1), battle.HeroAt(0), battle.HeroAt(2) };
                battle.ApplyFormation(formation);
                battle.Retry();
                typeof(TrialBattle).GetField("<Elapsed>k__BackingField", flags).SetValue(battle, 23.5f);
                Finish(false, .4f);
                Call("ShowCompletedBoss");
                Require(battle.CompletedBossAttempt.seconds == 23.5f && battle.CompletedBossAttempt.party[0].StartsWith(battle.Units[0].Name), "duration and changed formation captured");
                Set("idleSaveError", "test save failure");
                Call("CloseBossReview", false, true);
                Require(battle.Finished, "failed save blocks retry dismissal");
                Call("CheckpointProgress", false);
                Call("CloseBossReview", false, true);
                Require(!battle.Finished && !battle.ActiveFarming && battle.ActiveStage == battle.Next, "successful save allows same-boss retry");
            }
            finally { UnityEngine.Object.DestroyImmediate(host); Directory.Delete(folder, true); }
        }
    }
}
