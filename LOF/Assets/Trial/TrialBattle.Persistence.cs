using System;
using System.Collections.Generic;

namespace AFKTrial
{
    public sealed partial class TrialBattle
    {
        [Serializable]
        public sealed class Progress
        {
            public int highest, stage, level, gold, experience, tickets, duplicateStreak;
            public bool farming;
            public string[] formation;
            public HeroProgress[] heroes;
            public GearProgress[] inventory;
        }

        [Serializable]
        public sealed class HeroProgress
        {
            public string id;
            public int level;
            public int[] skills;
        }

        [Serializable]
        public sealed class GearProgress
        {
            public string name, ownerId;
            public TrialData.GearSlot slot;
            public int bonus, sellGold;
        }

        public Progress CaptureProgress()
        {
            var heroes = new List<HeroProgress>();
            for (int i = 0; i < owned.Length; i++)
            {
                if (!owned[i]) continue;
                heroes.Add(new HeroProgress {
                    id = Data.Heroes[i].Id, level = HeroLevel(i, Level),
                    skills = new[] { skillLevels[i, 0], skillLevels[i, 1], skillLevels[i, 2] }
                });
            }
            var items = new GearProgress[inventory.Count];
            for (int i = 0; i < items.Length; i++)
            {
                var item = inventory[i];
                items[i] = new GearProgress {
                    name = item.Gear.Name, slot = item.Gear.Slot, bonus = item.Gear.Bonus, sellGold = item.Gear.SellGold,
                    ownerId = item.Owner < 0 ? "" : Data.Heroes[item.Owner].Id
                };
            }
            return new Progress {
                highest = Highest, stage = Stage, farming = Farming, level = Level, gold = Gold,
                experience = Experience, tickets = Tickets, duplicateStreak = duplicateStreak,
                formation = Array.ConvertAll(formation, index => Data.Heroes[index].Id),
                heroes = heroes.ToArray(), inventory = items
            };
        }

        public bool RestoreProgress(Progress saved)
        {
            if (saved == null || saved.highest < 0 || saved.highest == int.MaxValue || saved.stage < 0 ||
                (saved.farming ? saved.stage > saved.highest : saved.stage != saved.highest + 1) ||
                saved.level < 1 || saved.gold < 0 || saved.experience < 0 || saved.tickets < 0 ||
                saved.duplicateStreak < 0 || saved.duplicateStreak > 9 ||
                saved.formation == null || saved.formation.Length != formation.Length ||
                saved.heroes == null || saved.inventory == null) return false;

            var roster = new HeroProgress[owned.Length];
            foreach (var hero in saved.heroes)
            {
                if (hero == null || hero.level < 1 || hero.skills == null || hero.skills.Length != 3 ||
                    Array.Exists(hero.skills, value => value < 1 || value > Math.Min(5, hero.level))) return false;
                int index = Data.Heroes.FindIndex(fighter => fighter.Id == hero.id);
                if (index < 0 || roster[index] != null) return false;
                roster[index] = hero;
            }
            var party = Array.ConvertAll(saved.formation, id => Data.Heroes.FindIndex(hero => hero.Id == id));
            if (Array.Exists(party, index => index < 0 || roster[index] == null) ||
                new HashSet<int>(party).Count != party.Length) return false;

            var items = new List<Item>();
            var equippedSlots = new HashSet<(int, TrialData.GearSlot)>();
            foreach (var gear in saved.inventory)
            {
                if (gear == null || string.IsNullOrEmpty(gear.name) || gear.bonus < 1 || gear.sellGold < 1 ||
                    !Enum.IsDefined(typeof(TrialData.GearSlot), gear.slot)) return false;
                int owner = -1;
                if (!string.IsNullOrEmpty(gear.ownerId))
                {
                    owner = Data.Heroes.FindIndex(hero => hero.Id == gear.ownerId);
                    if (owner < 0 || roster[owner] == null || !equippedSlots.Add((owner, gear.slot))) return false;
                }
                var definition = new TrialData.Gear {
                    Name = gear.name, Slot = gear.slot, Bonus = gear.bonus, SellGold = gear.sellGold
                };
                items.Add(new Item(definition) { Owner = owner });
            }

            Highest = saved.highest;
            Stage = saved.stage;
            Farming = saved.farming;
            Level = saved.level;
            Gold = saved.gold;
            Experience = saved.experience;
            Tickets = saved.tickets;
            duplicateStreak = saved.duplicateStreak;
            for (int i = 0; i < roster.Length; i++)
            {
                var hero = roster[i];
                owned[i] = hero != null;
                levelOffset[i] = hero == null ? 0 : hero.level - Level;
                for (int slot = 0; slot < 3; slot++)
                    skillLevels[i, slot] = hero == null ? 0 : hero.skills[slot];
            }
            Array.Copy(party, formation, party.Length);
            inventory.Clear();
            inventory.AddRange(items);
            StartBattle();
            return true;
        }

        public void CreditIdle(int gold, int experience)
        {
            if (gold < 0 || experience < 0) throw new ArgumentOutOfRangeException();
            Gold = AddCurrency(Gold, gold);
            Experience = AddCurrency(Experience, experience);
        }

        private static int AddCurrency(int balance, int award) => (int)Math.Min(int.MaxValue, (long)balance + award);
    }
}
