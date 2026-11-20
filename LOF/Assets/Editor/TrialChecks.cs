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
                CheckSkillCasts(TrialData.Load());
                var fixture = new TrialData(Path.Combine(Application.dataPath, "Editor/TrialChecksData~"));
                TrialBattle NewBattle() => new TrialBattle(fixture);
                CheckEquipment(NewBattle());
                CheckIdleEquipment(NewBattle());
                CheckHeroGrowth(NewBattle());
                CheckSummonsAndSkills(NewBattle());
                CheckDungeons(NewBattle());
                CheckRepeatStage(NewBattle());
                var roster = NewBattle();
                for (int hero = 0; hero < roster.Heroes.Count; hero++)
                {
                    int count = roster.Heroes.Count;
                    int[] party = { hero, (hero + 1) % count, (hero + 2) % count };
                    var previous = roster.Units[0];
                    roster.ApplyFormation(party);
                    Require(roster.Units[0] == previous, "applying formation preserves current encounter");
                    roster.StartBattle();
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
                Require(battle.Won && battle.ActiveStage == 1 && battle.GoldReward == battle.Gold &&
                    battle.ExperienceReward == battle.Experience, "victory HUD shows completed stage and awarded amounts");
                int gold = battle.Gold;
                battle.Tick(1);
                Require(battle.Gold == gold, "no duplicate result reward");
                int upgraded = battle.HeroAt(0);
                Require(battle.UpgradeHero(upgraded) && battle.HeroLevel(upgraded) == 2 && battle.Units[0].Level == 1,
                    "upgrade one hero with stage rewards");
                battle.StartBattle();
                Require(battle.Units[0].Level == 2 && battle.Units[0].Hp == 135 && battle.Units[0].Attacks == 0,
                    "new battle resets state and applies growth");
                battle.ForceTimeout();
                Require(battle.Farming && battle.Stage == 1 && battle.Next == 2, "defeat enters farming");
                Require(!battle.Won && !battle.ActiveFarming && battle.ActiveStage == 2 &&
                    battle.GoldReward == 0 && battle.ExperienceReward == 0, "defeat HUD has no previous reward");
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
                bossBattle.ForceTimeout();
                Require(bossBattle.BossStage && bossBattle.Limit == fixture.BossSeconds, "boss HUD stays until restart");
                bossBattle.StartBattle();
                Require(!bossBattle.BossStage && bossBattle.ActiveFarming && !bossBattle.Finished, "farming HUD after boss defeat");
                var wipe = NewBattle();
                foreach (var unit in wipe.Units) unit.Hp = 0;
                wipe.Tick(0.05f);
                Require(wipe.Farming && wipe.Highest == 0 && wipe.Gold == 0, "simultaneous wipe loses");
                Require(!NewBattle().UpgradeHero(upgraded), "insufficient resources rejected");
                Require(battle.StageName(11) == "ROUND 2 / STAGE 1", "round display");
                Directory.CreateDirectory("Logs");
                File.WriteAllText("Logs/TrialChecks.txt", "PASS: battle, hero growth, formation and dungeon checks\nUnity " + Application.unityVersion +
                    "\n" + DateTime.UtcNow.ToString("O") + "\nNot a Windows build or full game QA result.");
                Debug.Log("[AFK Trial] PASS: battle, hero growth, formation and dungeon checks. See Logs/TrialChecks.txt");
            }
            catch (Exception error)
            {
                Directory.CreateDirectory("Logs");
                File.WriteAllText("Logs/TrialChecks.txt", "FAIL: " + error);
                Debug.LogException(error);
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }

        private static void CheckHeroGrowth(TrialBattle battle)
        {
            var old = battle.CaptureProgress();
            int hero = battle.HeroAt(0), other = battle.HeroAt(1);
            string heroId = battle.Data.Heroes[hero].Id;
            old.level = 3; // A v1 save stores both the historic team level and absolute hero levels.
            Array.Find(old.heroes, entry => entry.id == heroId).level = 4;
            Array.Find(old.heroes, entry => entry.id == battle.Data.Heroes[other].Id).level = 2;
            Array.Resize(ref old.heroes, old.heroes.Length - 1);
            int locked = battle.Heroes.Count - 1;
            old.duplicateStreak = 9;
            old.highest = battle.Data.BossEvery;
            old.stage = old.highest + 1;
            old.gold = 4 * battle.Data.UpgradeGold;
            old.experience = 4 * battle.Data.UpgradeXp - 1;
            Require(battle.RestoreProgress(old) && battle.HeroLevel(hero) == 4 && battle.HeroLevel(other) == 2 &&
                battle.HeroLevel(locked) == 0, "read each owned hero's absolute level from a team-level save");
            var cost = battle.HeroUpgradeCost(hero);
            Require(cost.gold == old.gold && cost.experience == old.experience + 1 &&
                battle.HeroUpgradeCost(other).gold == 2 * battle.Data.UpgradeGold, "price uses selected hero level");
            Require(!battle.UpgradeHero(locked) && !battle.CanUpgradeHero(hero) && !battle.UpgradeHero(hero) &&
                battle.Gold == old.gold && battle.Experience == old.experience, "locked hero or missing experience cannot spend");
            battle.CreditIdle(0, 1);
            Require(!battle.UpgradeHero(hero, _ => false) && battle.HeroLevel(hero) == 4 &&
                battle.Gold == cost.gold && battle.Experience == cost.experience, "failed save leaves level and wallet intact");
            var unit = battle.Units[0];
            var necklace = battle.Inventory[4];
            float maxHp = unit.MaxHp;
            unit.Hp = maxHp / 2;
            TrialBattle.Progress accepted = null;
            Require(battle.UpgradeHero(hero, next => { accepted = next; return true; }) && accepted != null &&
                accepted.gold == 0 && accepted.experience == 0 && Array.Find(accepted.heroes, entry => entry.id == heroId).level == 5 &&
                battle.HeroLevel(hero) == 5 && battle.HeroLevel(other) == 2 && battle.Gold == 0 && battle.Experience == 0,
                "accepted save contains exactly one purchased level and both currency deductions");
            Require(unit.Level == 4 && unit.MaxHp == maxHp && battle.Equip(hero, necklace) && unit.Level == 4 &&
                unit.MaxHp == maxHp && unit.Hp == maxHp / 2,
                "pending equipment and level preserve live HP");
            Require(battle.TryStartDungeon(TrialData.DungeonKind.GoldVault, 0) && battle.Units[0].Level == 5 &&
                battle.Units[0].MaxHp == unit.MaxHp + necklace.Gear.Bonus + battle.Data.Heroes[hero].HpPerLevel && battle.Units[1].Level == 2 &&
                battle.Units[0].Armor == unit.Armor + battle.Data.Heroes[hero].ArmorPerLevel,
                "next dungeon applies only the purchased hero level");
            var restored = new TrialBattle(battle.Data);
            var save = JsonUtility.FromJson<TrialBattle.Progress>(JsonUtility.ToJson(battle.CaptureProgress()));
            Require(restored.RestoreProgress(save) && restored.HeroLevel(hero) == 5 && restored.HeroLevel(other) == 2 &&
                restored.ActiveDungeon == null && restored.Gold == 0 && restored.Experience == 0, "individual growth survives reload");
            var pull = restored.Summon(1)[0];
            Require(pull.IsNew && pull.Hero == locked && pull.Level == 1 && restored.HeroLevel(hero) == 5,
                "newly summoned hero starts at level one");
            restored.CreditIdle(battle.Data.UpgradeGold, battle.Data.UpgradeXp);
            Require(restored.UpgradeHero(pull.Hero) && restored.HeroLevel(pull.Hero) == 2 &&
                restored.HeroLevel(hero) == 5 && restored.Units[0].Level == 5, "reserve hero can level independently");
            save.gold = 5 * battle.Data.UpgradeGold - 1;
            save.experience = 5 * battle.Data.UpgradeXp;
            Require(restored.RestoreProgress(save) && !restored.UpgradeHero(hero) &&
                restored.Gold == save.gold && restored.Experience == save.experience, "missing gold preserves both currencies");
            Array.Find(save.heroes, entry => entry.id == heroId).level = int.MaxValue - 1;
            save.gold = save.experience = int.MaxValue;
            Require(restored.RestoreProgress(save) && restored.HeroUpgradeCost(hero).gold > int.MaxValue &&
                !restored.UpgradeHero(hero) && restored.Gold == int.MaxValue && restored.Experience == int.MaxValue,
                "level price cannot wrap to a free upgrade");
        }

        private static void CheckDungeons(TrialBattle battle)
        {
            var vault = TrialData.DungeonKind.GoldVault;
            var archive = TrialData.DungeonKind.ExperienceArchive;
            var easy = battle.Data.Dungeons[(int)vault, 0];
            var normal = battle.Data.Dungeons[(int)vault, 1];
            var archiveEasy = battle.Data.Dungeons[(int)archive, 0];
            Require(!battle.TryStartDungeon(vault, 0), "dungeon requires first main boss");

            var progress = battle.CaptureProgress();
            progress.highest = battle.Data.BossEvery;
            progress.stage = progress.highest;
            progress.farming = true;
            Require(battle.RestoreProgress(progress) && !battle.TryStartDungeon(vault, 1), "newly opened dungeon starts at Easy");
            Require(battle.TryStartDungeon(vault, 0) && battle.ActiveDungeon == easy && !battle.BossStage &&
                !battle.ActiveFarming && battle.Limit == easy.Seconds && !battle.TryStartDungeon(archive, 0),
                "dungeon encounter replaces the main boss and rejects nested entry");
            float easyHp = battle.Units[3].MaxHp;
            battle.ForceTimeout();
            Require(!battle.Won && !battle.CanEnterDungeon(normal) && battle.Gold == progress.gold &&
                battle.Experience == progress.experience && battle.Stage == progress.stage, "dungeon defeat grants nothing and preserves farming");
            battle.StartBattle();
            Require(battle.ActiveDungeon == null && battle.ActiveFarming && battle.BossStage &&
                battle.ActiveStage == progress.stage && battle.Elapsed == 0, "return restarts the original farming stage");

            Require(battle.TryStartDungeon(vault, 0), "retry dungeon after defeat");
            WinEncounter(battle);
            Require(battle.GoldReward == easy.Gold && battle.Gold == progress.gold + easy.Gold &&
                battle.Experience == progress.experience && battle.Tickets == progress.tickets &&
                battle.Highest == progress.highest && battle.CanEnterDungeon(normal) &&
                !battle.CanEnterDungeon(battle.Data.Dungeons[(int)archive, 1]), "vault win credits gold and unlocks only its next difficulty");
            battle.Tick(1);
            battle.ForceTimeout();
            Require(battle.Won && battle.Gold == progress.gold + easy.Gold, "finished dungeon settles once");

            var saved = battle.CaptureProgress();
            var restored = new TrialBattle(battle.Data);
            Require(restored.RestoreProgress(saved) && restored.ActiveDungeon == null && restored.IsDungeonCleared(easy) &&
                restored.Gold == battle.Gold && restored.TryStartDungeon(vault, 1) && restored.Units[3].MaxHp > easyHp,
                "new session restores wallet, dungeon unlock and main stage");
            WinEncounter(restored);
            restored.StartBattle();
            Require(restored.TryStartDungeon(vault, 2), "Normal victory unlocks Hard");
            restored.StartBattle();
            Require(restored.TryStartDungeon(vault, 0), "cleared difficulty remains repeatable");
            int gold = restored.Gold;
            WinEncounter(restored);
            Require(restored.Gold == gold + easy.Gold, "repeat victory awards currency");

            // A later main round must neither scale dungeon enemies nor change the challenge target.
            saved.farming = false;
            saved.highest += battle.Data.RoundLength;
            saved.stage = saved.highest + 1;
            saved.experience = int.MaxValue - 1;
            Require(restored.RestoreProgress(saved) && restored.TryStartDungeon(archive, 0), "enter archive from challenge");
            Require(restored.Units[3].MaxHp == restored.Data.Enemies["normal"].Hp * archiveEasy.Power, "fixed dungeon enemy strength");
            WinEncounter(restored);
            Require(restored.Experience == int.MaxValue && restored.ExperienceReward == 1 && restored.GoldReward == 0 &&
                restored.Gold == saved.gold && restored.Tickets == saved.tickets, "archive reports only experience actually credited");
            restored.StartBattle();
            Require(!restored.Farming && restored.ActiveStage == saved.stage && restored.Highest == saved.highest,
                "dungeon victory returns to the original challenge target");
        }

        private static void CheckRepeatStage(TrialBattle battle)
        {
            Require(!battle.SetRepeatStage(1), "repeat requires a cleared stage");
            for (int i = 0; i < battle.Data.BossEvery; i++) { WinEncounter(battle); battle.StartBattle(); }
            int boss = battle.Highest;
            var unit = battle.Units[0];
            Require(!battle.SetRepeatStage(-1) && !battle.SetRepeatStage(boss + 1) &&
                !battle.SetRepeatStage(boss, _ => false) && battle.RepeatStage == 0, "invalid or unsaved repeat stays off");
            Require(battle.SetRepeatStage(boss) && battle.Units[0] == unit && !battle.ActiveRepeat, "repeat waits for next battle");
            WinEncounter(battle);
            Require(battle.Highest == boss + 1 && battle.TicketReward > 0, "pending repeat preserves current first clear");
            battle.StartBattle();
            Require(battle.ActiveRepeat && battle.BossStage && battle.ActiveStage == boss, "repeat starts selected boss");
            int tickets = battle.Tickets;
            Require(battle.SetRepeatStage(1), "retarget repeat");
            WinEncounter(battle);
            Require(battle.Highest == boss + 1 && battle.Tickets == tickets &&
                battle.GoldReward == battle.Data.BaseGold + boss * battle.Data.GoldPerStage &&
                battle.ExperienceReward == battle.Data.BaseXp + boss * battle.Data.XpPerStage, "repeat pays completed stage at normal rate");
            var restored = new TrialBattle(battle.Data);
            Require(restored.RestoreProgress(battle.CaptureProgress()) && restored.ActiveStage == 1 && restored.ActiveRepeat,
                "saved repeat starts at new target");
            restored.ForceTimeout();
            restored.StartBattle();
            Require(restored.ActiveStage == 1 && restored.ActiveRepeat && !restored.Farming, "repeat defeat keeps target and progression");
            Require(restored.TryStartDungeon(TrialData.DungeonKind.GoldVault, 0) && !restored.ActiveRepeat, "dungeon keeps independent rules");
            WinEncounter(restored);
            restored.StartBattle();
            Require(restored.ActiveStage == 1 && restored.SetRepeatStage(0) && restored.ActiveRepeat, "stop after active repeat");
            WinEncounter(restored);
            restored.StartBattle();
            Require(!restored.ActiveRepeat && restored.ActiveStage == boss + 2, "resume original challenge after repeat");
            restored.ForceTimeout();
            restored.SetRepeatStage(1);
            restored.StartBattle();
            restored.SetRepeatStage(0);
            WinEncounter(restored);
            restored.StartBattle();
            Require(restored.ActiveFarming && restored.ActiveStage == restored.Highest, "resume original automatic farming after repeat");
        }

        private static void WinEncounter(TrialBattle battle)
        {
            foreach (var unit in battle.Units)
                if (!unit.Ally) unit.Hp = 0;
            battle.Tick(0.05f);
            Require(battle.Finished && battle.Won, "defeating all enemies wins the encounter");
        }

        private static void CheckSummonsAndSkills(TrialBattle battle)
        {
            int tickets = battle.Tickets;
            var first = battle.Summon(1)[0];
            Require(!first.IsNew && first.Level == 2 && battle.Tickets == tickets - 1, "duplicate raises only drawn hero");
            Require(!battle.Train(first.Hero, 0), "skill rejects insufficient gold");
            battle.Sell(battle.Inventory[1]);
            int gold = battle.Gold, cost = battle.SkillCost(first.Hero, 0);
            Require(battle.Train(first.Hero, 0) && battle.SkillLevel(first.Hero, 0) == 2 &&
                battle.SkillLevel(first.Hero, 1) == 1 && battle.Gold == gold - cost, "skill upgrade is per hero and slot");
            battle.CreditIdle(2 * battle.Data.UpgradeGold, 2 * battle.Data.UpgradeXp);
            Require(battle.UpgradeHero(first.Hero) && battle.HeroLevel(first.Hero) == 3 &&
                battle.HeroLevel((first.Hero + 1) % battle.Heroes.Count) == 1, "manual and duplicate levels accumulate on the same hero");
            Require(battle.Summon(10).Length == 10 && battle.Summon(10) == null &&
                battle.Tickets == tickets - 11, "ten pulls are all or nothing");
            UntilFinished(battle);
            Require(battle.Won && battle.Tickets == tickets - 11 + battle.Data.FirstWinTickets, "first win tickets");
        }
        private static void CheckIdleEquipment(TrialBattle battle)
        {
            const int seed = 24601;
            Require(battle.EstimateIdleEquipment(3600, seed).Items.Count == 0, "practice grants no idle gear");
            WinEncounter(battle);
            var before = battle.CaptureProgress();
            long seconds = battle.Data.IdleCapHours * 3600L;
            var reward = battle.EstimateIdleEquipment(seconds, seed);
            var again = battle.EstimateIdleEquipment(seconds, seed);
            Require(reward.Items.Count > 0 && reward.Converted > 0 && reward.ConversionGold == again.ConversionGold &&
                reward.Items.Count == again.Items.Count, "idle loot includes bag overflow and repeats the same roll");
            for (int i = 0; i < reward.Items.Count; i++)
                Require(reward.Items[i].Gear == again.Items[i].Gear, "pending idle gear is stable across previews");
            Require(!battle.CollectIdle(reward.ConversionGold, 12, reward, _ => false) &&
                battle.Inventory.Count == before.inventory.Length && battle.Gold == before.gold, "failed claim preserves bag and wallet");
            TrialBattle.Progress saved = null;
            Require(battle.CollectIdle(reward.ConversionGold, 12, reward, next => { saved = next; return true; }) &&
                battle.Gold == before.gold + reward.ConversionGold && battle.Experience == before.experience + 12 &&
                battle.Inventory.Count == before.inventory.Length + reward.Items.Count, "collects idle gear and converted gold together");
            var restored = new TrialBattle(battle.Data);
            Require(restored.RestoreProgress(saved) && restored.Inventory.Count == battle.Inventory.Count, "idle equipment survives reload");
            foreach (TrialData.GearSlot slot in Enum.GetValues(typeof(TrialData.GearSlot)))
            {
                int count = 0;
                foreach (var item in restored.Inventory) if (item.Gear.Slot == slot) count++;
                Require(count <= 10, "idle loot respects each equipment slot capacity");
            }
        }

        private static void CheckSkillCasts(TrialData data)
        {
            var battle = new TrialBattle(data);
            var save = battle.CaptureProgress();
            save.heroes = data.Heroes.ConvertAll(hero => new TrialBattle.HeroProgress {
                id = hero.Id, level = 1, skills = new[] { 1, 1, 1 }
            }).ToArray();
            Require(battle.RestoreProgress(save), "skill test roster");
            for (int hero = 0; hero < data.Heroes.Count; hero++)
            {
                battle.ApplyFormation(new[] { hero, (hero + 1) % data.Heroes.Count, (hero + 2) % data.Heroes.Count });
                battle.StartBattle();
                foreach (var unit in battle.Units) unit.Hp = unit.MaxHp = 100000;
                var observed = new System.Collections.Generic.HashSet<string>();
                for (int i = 0; i < 550; i++)
                {
                    battle.Tick(0.05f);
                    observed.Add(battle.Units[0].LastSkill);
                }
                for (int slot = 0; slot < 3; slot++)
                    Require(observed.Contains(data.Skills[hero, slot].Name), "cast " + data.Skills[hero, slot].Name);
            }
        }

        private static void CheckEquipment(TrialBattle battle)
        {
            var sword = battle.Inventory[0];
            var replacement = battle.Inventory[1];
            var armor = battle.Inventory[2];
            var necklace = battle.Inventory[4];
            var unit = battle.Units[0];
            int hero = battle.HeroAt(0);
            float attack = unit.Attack, hp = unit.MaxHp;
            unit.Hp *= 0.5f;
            Require(battle.Equip(hero, sword) && battle.Equip(hero, armor) && battle.Equip(hero, necklace),
                "three equipment slots");
            int baseArmor = battle.Data.Heroes[hero].BaseArmor;
            Require(unit.Attack == attack && unit.Armor == baseArmor &&
                unit.MaxHp == hp && unit.Hp == hp / 2, "equipment waits for next battle");
            Require(!battle.Sell(sword) && !battle.Equip(battle.HeroAt(1), sword), "equipped item ownership");
            Require(battle.Equip(hero, replacement) && sword.Owner == -1 && unit.Attack == attack,
                "replacement releases prior item");
            battle.StartBattle();
            var enemy = battle.Units[3];
            unit = battle.Units[0];
            Require(unit.Attack == attack + replacement.Gear.Bonus && unit.MaxHp == hp + necklace.Gear.Bonus &&
                unit.Armor == baseArmor + armor.Gear.Bonus, "new encounter applies all three equipment slots");
            enemy.Target = unit;
            enemy.Position = unit.Position;
            battle.Tick(0.05f);
            Require(Mathf.Approximately(unit.LastDamage, enemy.Attack * 100f / (100f + unit.Armor)),
                "armor reduces actual hit");
            Require(battle.Unequip(hero, TrialData.GearSlot.Armor) && unit.Armor == baseArmor + armor.Gear.Bonus, "removed armor remains active this battle");
            battle.StartBattle();
            Require(battle.Units[0].Armor == baseArmor, "next battle removes armor bonus");
            Require(battle.Sell(sword) && !battle.Sell(sword) && battle.Gold == sword.Gear.SellGold &&
                !battle.Equip(hero, sword), "sale credits once and removes item");
            battle.CreditIdle(battle.Data.EnhanceGold, 0);
            int balance = battle.Gold;
            Require(!battle.Enhance(replacement, _ => false) && battle.Gold == balance && replacement.Enhancement == 0,
                "failed equipment purchase is not applied");
            Require(battle.Enhance(replacement) && battle.Gold == balance - battle.Data.EnhanceGold &&
                battle.HeroStats(hero).attack == battle.Units[0].Attack + replacement.Gear.Step, "enhancement changes equipped stats");
            var restored = new TrialBattle(battle.Data);
            Require(restored.RestoreProgress(battle.CaptureProgress()) && restored.Equipped(hero, replacement.Gear.Slot).Enhancement == 1 &&
                restored.Units[0].Attack == battle.HeroStats(hero).attack, "enhanced equipment persists into the next encounter");
            int count = restored.Inventory.Count;
            WinEncounter(restored);
            Require(restored.GearReward != null && restored.Inventory.Count == count + 1 &&
                restored.Inventory[count].Owner < 0 && restored.Inventory[count].Enhancement == 0, "main victory drops unequipped gear");
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
