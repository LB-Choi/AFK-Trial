using System;
using System.Collections.Generic;

namespace AFKTrial
{
    public sealed partial class TrialBattle
    {
        private readonly int[] clearedDungeonTiers = new int[2];

        public bool CanEnterDungeon(TrialData.DungeonTier tier) =>
            tier != null && Data.Dungeons[(int)tier.Kind, tier.Index] == tier &&
            Highest >= Data.BossEvery && tier.Index <= clearedDungeonTiers[(int)tier.Kind];

        public bool IsDungeonCleared(TrialData.DungeonTier tier) =>
            tier != null && tier.Index < clearedDungeonTiers[(int)tier.Kind];

        private (int gold, int experience) CreditDungeonVictory(TrialData.DungeonTier tier)
        {
            int oldGold = Gold, oldExperience = Experience;
            int oldTickets = Tickets;
            if (!IsDungeonCleared(tier)) Tickets = AddCurrency(Tickets, tier.FirstClearTickets);
            TicketReward = Tickets - oldTickets;
            Gold = AddCurrency(Gold, tier.Gold);
            Experience = AddCurrency(Experience, tier.Experience);
            int kind = (int)tier.Kind;
            clearedDungeonTiers[kind] = Math.Max(clearedDungeonTiers[kind], tier.Index + 1);
            return (Gold - oldGold, Experience - oldExperience);
        }

        [Serializable]
        public sealed class Progress
        {
            public int highest, stage, gold, experience, tickets, duplicateStreak;
            // The version 1 save format requires a positive team baseline. Hero records carry absolute levels.
            public int level = 1;
            public int goldVaultClears, experienceArchiveClears, repeatStage;
            public bool farming;
            public string[] formation;
            public HeroProgress[] heroes;
            public GearProgress[] inventory;
            public BossAttempt[] bossHistory;
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
            public string name, ownerId, rarity;
            public TrialData.GearSlot slot;
            public int bonus, sellGold, enhancement, step;
        }

        private GearProgress CaptureItem(Item item) => new GearProgress {
            enhancement = item.Enhancement, step = item.Gear.Step,
            rarity = item.Gear.Rarity.ToString(), name = item.Gear.Name, slot = item.Gear.Slot, bonus = item.Gear.Bonus, sellGold = item.Gear.SellGold,
            ownerId = item.Owner < 0 ? "" : Data.Heroes[item.Owner].Id
        };

        public Progress CaptureProgress() => CaptureProgress(heroLevels);

        private Progress CaptureProgress(int[] levels)
        {
            var heroes = new List<HeroProgress>();
            for (int i = 0; i < heroLevels.Length; i++)
            {
                if (levels[i] == 0) continue;
                heroes.Add(new HeroProgress {
                    id = Data.Heroes[i].Id, level = levels[i],
                    skills = Owned(i) ? new[] { skillLevels[i, 0], skillLevels[i, 1], skillLevels[i, 2] } : new[] { 1, 1, 1 }
                });
            }
            var items = new GearProgress[inventory.Count];
            for (int i = 0; i < items.Length; i++)
                items[i] = CaptureItem(inventory[i]);
            return new Progress {
                highest = Highest, stage = Stage, farming = Farming, gold = Gold, repeatStage = RepeatStage,
                experience = Experience, tickets = Tickets, duplicateStreak = duplicateStreak,
                goldVaultClears = clearedDungeonTiers[(int)TrialData.DungeonKind.GoldVault],
                experienceArchiveClears = clearedDungeonTiers[(int)TrialData.DungeonKind.ExperienceArchive],
                formation = Array.ConvertAll(formation, index => index < 0 ? "" : Data.Heroes[index].Id),
                heroes = heroes.ToArray(), inventory = items, bossHistory = BossHistory
            };
        }

        public bool RestoreProgress(Progress saved)
        {
            if (saved == null || saved.highest < 0 || saved.highest == int.MaxValue || saved.stage < 0 ||
                (saved.farming ? saved.stage > saved.highest : saved.stage != saved.highest + 1) ||
                saved.repeatStage < 0 || saved.repeatStage > saved.highest ||
                saved.level < 1 || saved.gold < 0 || saved.experience < 0 || saved.tickets < 0 ||
                saved.duplicateStreak < 0 || saved.duplicateStreak > 9 ||
                saved.goldVaultClears < 0 || saved.goldVaultClears > TrialData.DungeonTierCount ||
                saved.experienceArchiveClears < 0 || saved.experienceArchiveClears > TrialData.DungeonTierCount ||
                saved.formation == null || saved.formation.Length != formation.Length ||
                saved.heroes == null || saved.inventory == null || !ValidBossHistory(saved.bossHistory)) return false;

            var roster = new HeroProgress[heroLevels.Length];
            foreach (var hero in saved.heroes)
            {
                if (hero == null || hero.level < 1 || hero.skills == null || hero.skills.Length != 3 ||
                    Array.Exists(hero.skills, value => value < 1 || value > hero.level)) return false;
                int index = Data.Heroes.FindIndex(fighter => fighter.Id == hero.id);
                if (index < 0 || roster[index] != null) return false;
                roster[index] = hero;
            }
            var party = Array.ConvertAll(saved.formation, id => Data.Heroes.FindIndex(hero => hero.Id == id));
            var deployed = new HashSet<int>();
            for (int i = 0; i < party.Length; i++)
            {
                if (saved.formation[i] == "") continue;
                int hero = party[i];
                if (hero < 0 || roster[hero] == null || !deployed.Add(hero)) return false;
            }
            if (deployed.Count == 0) return false;

            var items = new List<Item>();
            var equippedSlots = new HashSet<(int, TrialData.GearSlot)>();
            foreach (var gear in saved.inventory)
            {
                if (gear == null || string.IsNullOrEmpty(gear.name) || gear.enhancement < 0 || gear.step < 0 || gear.bonus < 1 || gear.sellGold < 1 ||
                    !Enum.IsDefined(typeof(TrialData.GearSlot), gear.slot)) return false;
                int owner = -1;
                if (!string.IsNullOrEmpty(gear.ownerId))
                {
                    owner = Data.Heroes.FindIndex(hero => hero.Id == gear.ownerId);
                    if (owner < 0 || roster[owner] == null || !equippedSlots.Add((owner, gear.slot))) return false;
                }
                var template = Data.StarterGear.Find(g => g.Name == gear.name && g.Slot == gear.slot);
                var rarity = template?.Rarity ?? TrialData.GearRarity.Common;
                if (!string.IsNullOrEmpty(gear.rarity) && (!Enum.TryParse(gear.rarity, out rarity) ||
                    !Enum.IsDefined(typeof(TrialData.GearRarity), rarity))) return false;
                var definition = new TrialData.Gear {
                    Rarity = rarity,
                    Step = gear.step > 0 ? gear.step : (template?.Step ?? 1),
                    Name = gear.name, Slot = gear.slot, Bonus = gear.bonus, SellGold = gear.sellGold
                };
                items.Add(new Item(definition) { Owner = owner, Enhancement = gear.enhancement });
            }

            Highest = saved.highest;
            RepeatStage = saved.repeatStage;
            Stage = saved.stage;
            Farming = saved.farming;
            Gold = saved.gold;
            Experience = saved.experience;
            Tickets = saved.tickets;
            duplicateStreak = saved.duplicateStreak;
            clearedDungeonTiers[(int)TrialData.DungeonKind.GoldVault] = saved.goldVaultClears;
            clearedDungeonTiers[(int)TrialData.DungeonKind.ExperienceArchive] = saved.experienceArchiveClears;
            for (int i = 0; i < roster.Length; i++)
            {
                var hero = roster[i];
                heroLevels[i] = hero?.level ?? 0;
                for (int slot = 0; slot < 3; slot++)
                    skillLevels[i, slot] = hero == null ? 0 : hero.skills[slot];
            }
            Array.Copy(party, formation, party.Length);
            inventory.Clear();
            inventory.AddRange(items);
            bossHistory.Clear();
            if (saved.bossHistory != null) bossHistory.AddRange(Array.ConvertAll(saved.bossHistory, item => item.Copy()));
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
