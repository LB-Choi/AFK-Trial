using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace AFKTrial.Editor
{
    internal static class TrialUpgradePreviewChecks
    {
        private static void Require(bool value, string message)
        {
            if (!value) throw new InvalidOperationException("Upgrade preview: " + message);
        }

        public static void Check(TrialData data)
        {
            var battle = new TrialBattle(data);
            var fixture = battle.CaptureProgress();
            fixture.heroes = data.Heroes.Select(hero => new TrialBattle.HeroProgress {
                id = hero.Id, level = 3, skills = new[] { 1, 1, 1 }
            }).ToArray();
            fixture.gold = fixture.experience = 100000;
            var stateField = typeof(TrialBattle.Unit).GetField("Skills", BindingFlags.NonPublic | BindingFlags.Instance);
            int Rank(TrialBattle.Unit unit, int slot)
            {
                object state = stateField.GetValue(unit);
                return ((int[])state.GetType().GetField("Ranks", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(state))[slot];
            }
            string Preview(int hero, int slot, int rank) => string.Join(";", battle.SkillPreview(hero, slot, rank).Select(row => row.label + ":" + row.value));
            for (int hero = 0; hero < data.Heroes.Count; hero++)
            {
                Require(battle.RestoreProgress(fixture), "fixture restores");
                battle.ApplyFormation(new[] { hero, (hero + 1) % data.Heroes.Count, (hero + 2) % data.Heroes.Count });
                battle.StartBattle();
                string before = JsonUtility.ToJson(battle.CaptureProgress());
                var stats = battle.HeroStats(hero, 4);
                for (int slot = 0; slot < 3; slot++)
                {
                    var current = battle.SkillPreview(hero, slot, 1);
                    var next = battle.SkillPreview(hero, slot, 2);
                    Require(current.Count > 0 && current.Count <= 3 && current.Count == next.Count &&
                        current.Select(row => row.label).SequenceEqual(next.Select(row => row.label)), "all 30 skills have aligned comparison rows");
                    Require(current.All(row => row.value != "None"), "live skills have effect previews");
                }
                Require(JsonUtility.ToJson(battle.CaptureProgress()) == before, "preview never spends or mutates progress");
                var unit = battle.Units[0];
                float oldHp = unit.MaxHp, oldAttack = unit.Attack;
                int oldArmor = unit.Armor;
                Require(battle.UpgradeHero(hero) && battle.HeroStats(hero) == stats &&
                    unit.MaxHp == oldHp && unit.Attack == oldAttack && unit.Armor == oldArmor && unit.Level == 3,
                    "hero preview matches purchase while current combat is unchanged");
                for (int slot = 0; slot < 3; slot++)
                {
                    string predicted = Preview(hero, slot, 2);
                    Require(battle.Train(hero, slot) && Rank(unit, slot) == 1 && Preview(hero, slot, battle.SkillLevel(hero, slot)) == predicted,
                        "skill purchase matches preview but keeps current combat rank");
                }
                battle.StartBattle();
                unit = battle.Units[0];
                Require(unit.MaxHp == stats.hp && unit.Attack == stats.attack && unit.Armor == stats.armor &&
                    Enumerable.Range(0, 3).All(slot => Rank(unit, slot) == 2), "next combat uses predicted stats and skill ranks");
                var saved = JsonUtility.FromJson<TrialBattle.Progress>(JsonUtility.ToJson(battle.CaptureProgress()));
                Require(battle.RestoreProgress(saved) && battle.HeroStats(hero) == stats, "upgraded stats survive reload");
            }

            Require(battle.RestoreProgress(fixture), "equipment fixture");
            int owner = battle.HeroAt(0);
            foreach (TrialData.GearSlot slot in Enum.GetValues(typeof(TrialData.GearSlot)))
            {
                var item = battle.Inventory.First(entry => entry.Gear.Slot == slot);
                battle.Equip(owner, item);
                battle.StartBattle();
                var active = battle.Units[0];
                var oldStats = (active.MaxHp, active.Attack, active.Armor);
                int expected = item.BonusAt(item.Enhancement + 1);
                Require(battle.Enhance(item) && item.Bonus == expected && (active.MaxHp, active.Attack, active.Armor) == oldStats,
                    "each gear type matches preview without altering active unit");
                var next = battle.HeroStats(owner);
                battle.StartBattle();
                Require((battle.Units[0].MaxHp, battle.Units[0].Attack, battle.Units[0].Armor) == next, "next battle uses enhanced gear");
            }

            int Find(string id) => data.Heroes.FindIndex(hero => hero.Id == id);
            Require(Preview(Find("hammer_maid"), 0, 1).Contains("Normal enemy stun:0.2s") &&
                Preview(Find("hammer_maid"), 0, 2).Contains("Boss stun:0.2s"), "normal and boss stun scaling");
            Require(Preview(Find("scythe_knight"), 0, 1) == Preview(Find("scythe_knight"), 0, 2) &&
                Preview(Find("royal_knight"), 2, 1) == Preview(Find("royal_knight"), 2, 2), "non-scaling skills stay unchanged");
            Require(Preview(Find("tower_knight"), 2, 100) == Preview(Find("tower_knight"), 2, 101), "capped reduction does not promise further benefit");
            Require(Preview(Find("axe_warrior"), 1, 1).Contains("1 x normal") &&
                Preview(Find("axe_warrior"), 1, 2).Contains("2 x normal") &&
                Preview(Find("sun_paladin"), 2, 2).Contains("20%"), "speed multiplier and equalize healing scaling");

            // Exercise actual cast outputs independently of the display's formatting code.
            battle.RestoreProgress(fixture);
            var caster = battle.Units[0];
            caster.Attack = 100;
            caster.Target = battle.Units[3];
            var hitType = typeof(TrialBattle).GetNestedType("PendingHit", BindingFlags.NonPublic);
            var cast = typeof(TrialBattle).GetMethod("Cast", BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var effect in new[] { SkillEffect.Wave, SkillEffect.Double, SkillEffect.Flurry })
            for (int rank = 1; rank <= 2; rank++)
            {
                var hits = Array.CreateInstance(hitType, battle.Units.Count);
                cast.Invoke(battle, new object[] { caster, effect, rank, hits });
                float actual = (float)hitType.GetField("Total").GetValue(hits.GetValue(3));
                float expected = effect == SkillEffect.Wave ? 50 * rank : effect == SkillEffect.Double ? 100 * rank : 200 * rank;
                Require(Mathf.Approximately(actual, expected), "damage scaling remains correct after sharing preview values");
            }
        }
    }
}
