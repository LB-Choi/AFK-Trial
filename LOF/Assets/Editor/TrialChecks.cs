using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AFKTrial.Editor
{
    internal static class TrialChecks
    {
        [InitializeOnLoadMethod]
        private static void OnReload() { EditorApplication.delayCall += Run; }

        [MenuItem("AFK Trial/Run Model Checks")]
        public static void Run()
        {
            try
            {
                var battle = new TrialBattle();
                Require(battle.Stage == 1 && battle.Units.Count == 6, "initial state");
                UntilFinished(battle);
                Require(battle.Highest == 1 && battle.Stage == 2 && battle.Gold == 45, "first win rewards / advance");
                int gold = battle.Gold;
                battle.Tick(1);
                Require(battle.Gold == gold, "no duplicate result reward");
                Require(battle.Upgrade() && battle.Level == 2 && battle.BattleLevel == 1, "deferred growth");
                battle.StartBattle();
                Require(battle.BattleLevel == 2 && battle.Units[0].Hp == 135 && battle.Units[0].Attacks == 0,
                    "new battle resets state and applies growth");
                battle.ForceTimeout();
                Require(battle.Farming && battle.Stage == 1 && battle.Next == 2, "defeat enters farming");
                gold = battle.Gold;
                battle.StartBattle();
                UntilFinished(battle);
                Require(battle.Highest == 1 && battle.Stage == 1 && battle.Gold == gold + 15, "farming stays / normal reward");
                battle.StartBattle();
                battle.ForceTimeout();
                Require(battle.Stage == 0 && battle.Next == 2, "farm retreats without losing challenge target");
                gold = battle.Gold;
                battle.StartBattle();
                battle.ForceTimeout();
                Require(battle.Stage == 0 && battle.Gold == gold, "practice floor / no loss reward");
                battle.Retry();
                Require(battle.Stage == 2 && !battle.Farming && !battle.Finished, "manual retry");
                var wipe = new TrialBattle();
                foreach (var unit in wipe.Units) unit.Hp = 0;
                wipe.Tick(0.05f);
                Require(wipe.Farming && wipe.Highest == 0 && wipe.Gold == 0, "simultaneous wipe loses");
                Require(!new TrialBattle().Upgrade(), "insufficient resources rejected");
                Require(TrialBattle.StageName(11) == "ROUND 2 / STAGE 1", "round display");
                Directory.CreateDirectory("Logs");
                File.WriteAllText("Logs/TrialChecks.txt", "PASS: 13 model checks\nUnity " + Application.unityVersion +
                    "\n" + DateTime.UtcNow.ToString("O") + "\nNot a Windows build or full game QA result.");
                Debug.Log("[AFK Trial] PASS: 13 model checks. See Logs/TrialChecks.txt");
            }
            catch (Exception error)
            {
                Directory.CreateDirectory("Logs");
                File.WriteAllText("Logs/TrialChecks.txt", "FAIL: " + error);
                Debug.LogException(error);
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }

        private static void UntilFinished(TrialBattle battle)
        {
            for (int i = 0; i < 1300 && !battle.Finished; i++) battle.Tick(0.05f);
            Require(battle.Finished, "battle terminates within time limit");
        }

        private static void Require(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException("Trial check failed: " + name);
        }
    }
}
