using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AFKTrial.Editor
{
    internal static class TrialChecks
    {
        [InitializeOnLoadMethod]
        private static void OnReload() { EditorApplication.delayCall += ValidateLiveTables; }

        private static void ValidateLiveTables()
        {
            TrialData.Load();
        }

        [MenuItem("AFK Trial/Run Model Checks")]
        public static void Run()
        {
            try
            {
                ValidateLiveTables();
                var fixture = new TrialData(Path.Combine(Application.dataPath, "Editor/TrialChecksData~"));
                TrialBattle NewBattle() => new TrialBattle(fixture);
                var roster = NewBattle();
                for (int hero = 0; hero < roster.Heroes.Count; hero++)
                {
                    int count = roster.Heroes.Count;
                    int[] party = { hero, (hero + 1) % count, (hero + 2) % count };
                    roster.ApplyFormation(party);
                    for (int frame = 0; frame < 60; frame++) roster.Tick(0.05f);
                    for (int slot = 0; slot < party.Length; slot++)
                    {
                        var unit = roster.Units[slot];
                        Require(unit.Name == roster.Heroes[party[slot]] && unit.Position.x > -2 &&
                            unit.Attacks > 0 && unit.Target.Hp < unit.Target.MaxHp, "roster member fights in selected slot");
                    }
                    roster.StartBattle();
                    Require(roster.Units[0].Name == roster.Heroes[hero], "formation survives restart");
                }
                var battle = NewBattle();
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
                var lanes = NewBattle();
                var attacker = lanes.Units[1];
                var target = lanes.Units[5];
                attacker.Target = target;
                float laneY = attacker.Position.y;
                lanes.Tick(0.05f);
                Require(attacker.Position.x > -2 && attacker.Position.y == laneY && attacker.Attacks == 0,
                    "approach on X only while outside attack range");
                attacker.Position.x = target.Position.x - 0.5f;
                lanes.Tick(0.05f);
                Require(attacker.Attacks == 1 && attacker.Position.y == laneY && target.Hp < target.MaxHp,
                    "attack across lanes using horizontal range");
                Require(target.LastHitTime == lanes.Elapsed && Mathf.Approximately(target.LastDamage, target.MaxHp - target.Hp),
                    "floating damage matches HP loss");
                var bossBattle = NewBattle();
                for (int stage = 1; stage < 5; stage++)
                {
                    foreach (var unit in bossBattle.Units)
                        if (!unit.Ally) unit.Hp = 0;
                    bossBattle.Tick(0.05f);
                    bossBattle.StartBattle();
                }
                Require(bossBattle.Units.Count == 6 && bossBattle.Units[4].Name == "BOSS" &&
                    bossBattle.Units[3].MaxHp < bossBattle.Units[4].MaxHp &&
                    bossBattle.Units[5].MaxHp == bossBattle.Units[3].MaxHp, "boss flanked by two regular enemies");
                var wipe = NewBattle();
                foreach (var unit in wipe.Units) unit.Hp = 0;
                wipe.Tick(0.05f);
                Require(wipe.Farming && wipe.Highest == 0 && wipe.Gold == 0, "simultaneous wipe loses");
                Require(!NewBattle().Upgrade(), "insufficient resources rejected");
                Require(battle.StageName(11) == "ROUND 2 / STAGE 1", "round display");
                Directory.CreateDirectory("Logs");
                File.WriteAllText("Logs/TrialChecks.txt", "PASS: battle and 10-character formation checks\nUnity " + Application.unityVersion +
                    "\n" + DateTime.UtcNow.ToString("O") + "\nNot a Windows build or full game QA result.");
                Debug.Log("[AFK Trial] PASS: battle and 10-character formation checks. See Logs/TrialChecks.txt");
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
