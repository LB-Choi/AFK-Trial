using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace AFKTrial.Editor
{
    internal static class TrialGoalChecks
    {
        private static void Require(bool ok, string message)
        { if (!ok) throw new InvalidOperationException("Goals: " + message); }
        public static void Check(TrialData data)
        {
            var battle = new TrialBattle(data);
            Require(battle.CompletedGoalCount == 0 && battle.CurrentGoal == 0, "fresh progress");
            battle.CreditIdle(10000, 10000);
            int hero = battle.FirstDeployedHero;
            Require(!battle.UpgradeHero(hero, p => false) && !battle.GoalComplete(0), "failed upgrade cannot complete goal");
            TrialBattle.Progress paid = null;
            Require(battle.UpgradeHero(hero, p => { paid = p; return true; }), "upgrade");
            var restored = new TrialBattle(data);
            Require(restored.RestoreProgress(paid) && restored.GoalComplete(0), "atomic upgrade save infers completion on restart");
            Require(!battle.Train(hero, 0, p => false) && !battle.GoalComplete(3), "failed training not recorded");
            Require(battle.Train(hero, 0) && battle.GoalComplete(3) && battle.CurrentGoal == 1, "later goal completes independently");
            var cleared = battle.CaptureProgress(); cleared.highest = 1; cleared.stage = 2;
            Require(battle.RestoreProgress(cleared), "idle equipment requires cleared stage");
            var equipment = battle.EstimateIdleEquipment(72000, 123);
            Require(equipment.Items.Count > 0 && battle.CollectIdle(0, 0, equipment, p => true), "equipment fixture");
            var item = battle.Inventory[0];
            Require(battle.Equip(hero, item) && battle.GoalComplete(1), "equip completes");
            var legacy = battle.CaptureProgress(); legacy.completedGoals = 0;
            Require(restored.RestoreProgress(legacy) && restored.Unequip(hero, item.Gear.Slot) && restored.GoalComplete(1), "old equipped save migrates and retains completion after unequip");
            Require(battle.Unequip(hero, item.Gear.Slot) && battle.Sell(item) && battle.GoalComplete(1), "selling does not undo completion");
            string folder = Path.Combine(Application.temporaryCachePath, "goals-" + Guid.NewGuid());
            Directory.CreateDirectory(folder);
            var host = new GameObject("Isolated goal checks"); host.SetActive(false);
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var runner = host.AddComponent<TrialRunner>();
            void Set(string key, object value) => typeof(TrialRunner).GetField(key, flags).SetValue(runner, value);
            object Get(string key) => typeof(TrialRunner).GetField(key, flags).GetValue(runner);
            object Call(string key, params object[] args) => typeof(TrialRunner).GetMethod(key, flags).Invoke(runner, args);
            try
            {
                string path = Path.Combine(folder, "goals.json");
                File.WriteAllText(path, JsonUtility.ToJson(battle.CaptureProgress()));
                Require(restored.RestoreProgress(JsonUtility.FromJson<TrialBattle.Progress>(File.ReadAllText(path))) && restored.GoalComplete(1), "file roundtrip retains sold equipment milestone");
                var state = restored.CaptureProgress(); state.highest = data.RoundLength; state.stage = state.highest + 1;
                state.goldVaultClears = 1;
                Require(restored.RestoreProgress(state) && restored.CompletedGoalCount == 6 && restored.CurrentGoal == -1, "all goals completed");
                int gold = restored.Gold, xp = restored.Experience, tickets = restored.Tickets;
                for (int i = 0; i < 3; i++) Require(restored.RestoreProgress(restored.CaptureProgress()), "repeat restore");
                Require(restored.Gold == gold && restored.Experience == xp && restored.Tickets == tickets, "goal completion never grants currency");
                state.completedGoals = 64; Require(!restored.RestoreProgress(state), "invalid bits rejected");
                Set("battle", restored); Call("OpenProgressSessionAt", folder);
                Set("atTitle", false);
                var menu = typeof(TrialRunner).GetNestedType("SystemMenu", BindingFlags.NonPublic);
                Set("systemMenu", Enum.Parse(menu, "Goals"));
                float elapsed = restored.Elapsed, hp = restored.Units[0].Hp;
                for (int i = 0; i < 3; i++) Call("Update");
                Require(restored.Elapsed == elapsed && restored.Units[0].Hp == hp, "goal list pauses combat");
                Call("BackFromSystemMenu"); Require(Get("systemMenu").ToString() == "Closed", "close returns to original screen");
                Set("systemMenu", Enum.Parse(menu, "Reset")); Set("resetConfirmation", "초기화");
                Require((bool)Call("TryResetProgress") && ((TrialBattle)Get("battle")).CompletedGoalCount == 0, "confirmed reset clears goals");
            }
            finally
            {
                (Get("progressLock") as IDisposable)?.Dispose(); Set("progressLock", null);
                UnityEngine.Object.DestroyImmediate(host);
                Directory.Delete(folder, true);
            }
        }
    }
}
