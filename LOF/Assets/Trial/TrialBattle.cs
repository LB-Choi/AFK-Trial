using System;
using System.Collections.Generic;
using UnityEngine;

namespace AFKTrial
{
    // Deliberately small, in-memory test model. Values are NOT final balance data.
    public sealed partial class TrialBattle
    {
        public readonly TrialData Data;
        public readonly IReadOnlyList<string> Heroes;
        private readonly int[] formation;
        public bool BossStage => ActiveStage > 0 && ActiveStage % Data.BossEvery == 0;
        public TrialData.Role ActiveSynergy { get; private set; }

        public TrialData.Role FormationSynergy(int[] party)
        {
            for (int i = 0; i < party.Length; i++)
                for (int j = i + 1; j < party.Length; j++)
                    if (Data.Heroes[party[i]].Role == Data.Heroes[party[j]].Role)
                        return Data.Heroes[party[i]].Role;
            return TrialData.Role.None;
        }

        public int HeroAt(int slot) => formation[slot];

        public void ApplyFormation(int[] heroes)
        {
            if (heroes == null || heroes.Length != formation.Length)
                throw new ArgumentException("Choose three heroes.");
            var seen = new HashSet<int>();
            foreach (int hero in heroes)
                if (hero < 0 || hero >= Heroes.Count || !Owned(hero) || !seen.Add(hero))
                    throw new ArgumentException("Choose three distinct roster members.");
            Array.Copy(heroes, formation, heroes.Length);
        }

        public sealed class Unit
        {
            public string Name;
            public bool Ally;
            public TrialData.Fighter Stats;
            public float SwingSpeed = 1;
            public float Hp, MaxHp, Attack, Cooldown;
            public Vector2 Position;
            public Unit Target;
            internal readonly SkillState Skills = new SkillState();
            public string LastSkill;
            public float LastSkillTime = float.NegativeInfinity;
            public int Level, Attacks, Armor;
            public int HeroIndex = -1;
            public bool Walking, WindingUp;
            public float SwingStart = float.NegativeInfinity;
            public float LastHitTime = float.NegativeInfinity;
            public float LastDamage, LastHitHeight;
            public bool Alive => Hp > 0;
        }

        public readonly List<Unit> Units = new List<Unit>();
        public int Highest { get; private set; }
        public int Stage { get; private set; } = 1;
        public int ActiveStage { get; private set; }
        public bool ActiveFarming { get; private set; }
        public int RepeatStage { get; private set; }
        public bool ActiveRepeat { get; private set; }

        public bool SetRepeatStage(int stage, Func<Progress, bool> save = null)
        {
            if (stage < 0 || stage > Highest) return false;
            if (save != null)
            {
                var progress = CaptureProgress();
                progress.repeatStage = stage;
                if (!save(progress)) return false;
            }
            RepeatStage = stage;
            return true;
        }
        public TrialData.DungeonTier ActiveDungeon { get; private set; }
        public int Next => Highest + 1;
        public bool Farming { get; private set; }
        public int Gold { get; private set; }
        public int Experience { get; private set; }
        public float Elapsed { get; private set; }
        public float Limit => ActiveDungeon?.Seconds ?? (BossStage ? Data.BossSeconds : Data.NormalSeconds);
        public bool Finished { get; private set; }
        public bool Won { get; private set; }
        public int GoldReward { get; private set; }
        public int ExperienceReward { get; private set; }
        public string Result { get; private set; } = "Battle started";

        public sealed class Item
        {
            public readonly TrialData.Gear Gear;
            public int Owner { get; internal set; } = -1;
            public int Enhancement { get; internal set; }
            public int BonusAt(int level) => (int)Math.Min(int.MaxValue, Gear.Bonus + (long)level * Gear.Step);
            public int Bonus => BonusAt(Enhancement);
            public Item(TrialData.Gear gear) { Gear = gear; }
        }

        private readonly List<Item> inventory = new List<Item>();
        public IReadOnlyList<Item> Inventory => inventory;
        public Item Equipped(int hero, TrialData.GearSlot slot) =>
            inventory.Find(item => item.Owner == hero && item.Gear.Slot == slot);

        public long EnhancementLimit => 11L + Highest / Data.RoundLength;
        public long EnhanceCost(Item item) => (item.Enhancement + 1L) * Data.EnhanceGold;
        public bool CanEnhance(Item item) => item != null && inventory.Contains(item) && item.Enhancement < EnhancementLimit &&
            item.Enhancement < int.MaxValue && item.Bonus < int.MaxValue && Gold >= EnhanceCost(item);

        public bool Enhance(Item item, Func<Progress, bool> save = null)
        {
            if (!CanEnhance(item)) return false;
            int balance = Gold - (int)EnhanceCost(item);
            if (save != null)
            {
                var progress = CaptureProgress();
                progress.gold = balance;
                progress.inventory[inventory.IndexOf(item)].enhancement++;
                if (!save(progress)) return false;
            }
            Gold = balance;
            item.Enhancement++;
            return true;
        }

        public TrialData.Gear GearReward { get; private set; }
        public bool GearConverted { get; private set; }

        private const int GearSlotCapacity = 10;

        private TrialData.Gear RollGear(int stage, Func<int, int> next)
        {
            if (stage <= 0 || Data.StarterGear.Count == 0 ||
                next(100) >= (stage % Data.BossEvery == 0 ? Data.BossDrop : Data.NormalDrop)) return null;
            var slots = new List<TrialData.GearSlot>();
            foreach (var gear in Data.StarterGear)
                if (!slots.Contains(gear.Slot)) slots.Add(gear.Slot);
            var slot = slots[next(slots.Count)];
            var pool = Data.StarterGear.FindAll(gear => gear.Slot == slot);
            return pool[next(pool.Count)];
        }

        private void RollGearReward()
        {
            GearReward = RollGear(ActiveStage, count => UnityEngine.Random.Range(0, count));
            if (GearReward == null) return;
            GearConverted = inventory.FindAll(item => item.Gear.Slot == GearReward.Slot).Count >= GearSlotCapacity;
            if (GearConverted)
            {
                int balance = AddCurrency(Gold, GearReward.SellGold);
                GoldReward = AddCurrency(GoldReward, balance - Gold);
                Gold = balance;
            }
            else inventory.Add(new Item(GearReward));
        }

        public sealed class IdleEquipment
        {
            public readonly List<Item> Items = new List<Item>();
            public int Converted, ConversionGold;
        }

        public IdleEquipment EstimateIdleEquipment(long creditedSeconds, int seed)
        {
            var result = new IdleEquipment();
            if (Highest == 0) return result;
            long clears = creditedSeconds * Data.IdleEfficiencyPercent / (100L * Data.IdleReferenceClearSeconds);
            var random = new System.Random(seed);
            var counts = new Dictionary<TrialData.GearSlot, int>();
            foreach (var item in inventory)
            {
                counts.TryGetValue(item.Gear.Slot, out int count);
                counts[item.Gear.Slot] = count + 1;
            }
            for (long i = 0; i < clears; i++)
            {
                var gear = RollGear(Highest, random.Next);
                if (gear == null) continue;
                counts.TryGetValue(gear.Slot, out int count);
                if (count < GearSlotCapacity)
                {
                    counts[gear.Slot] = count + 1;
                    result.Items.Add(new Item(gear));
                }
                else
                {
                    result.Converted++;
                    result.ConversionGold = AddCurrency(result.ConversionGold, gear.SellGold);
                }
            }
            return result;
        }

        public bool CollectIdle(int gold, int experience, IdleEquipment equipment, Func<Progress, bool> save)
        {
            var progress = CaptureProgress();
            progress.gold = AddCurrency(Gold, gold);
            progress.experience = AddCurrency(Experience, experience);
            int index = progress.inventory.Length;
            Array.Resize(ref progress.inventory, index + equipment.Items.Count);
            foreach (var item in equipment.Items) progress.inventory[index++] = CaptureItem(item);
            if (!save(progress)) return false;
            Gold = progress.gold;
            Experience = progress.experience;
            inventory.AddRange(equipment.Items);
            return true;
        }

        public bool Equip(int hero, Item item)
        {
            if (hero < 0 || hero >= Heroes.Count || !Owned(hero) || item == null || item.Owner >= 0 || !inventory.Contains(item)) return false;
            var previous = Equipped(hero, item.Gear.Slot);
            if (previous != null) previous.Owner = -1;
            item.Owner = hero;
            return true;
        }

        public bool Unequip(int hero, TrialData.GearSlot slot)
        {
            if (hero < 0 || hero >= Heroes.Count) return false;
            var item = Equipped(hero, slot);
            if (item == null) return false;
            item.Owner = -1;
            return true;
        }

        public bool Sell(Item item)
        {
            if (item == null || item.Owner >= 0 || !inventory.Remove(item)) return false;
            Gold = AddCurrency(Gold, item.Gear.SellGold);
            return true;
        }

        public (float hp, float attack, int armor) HeroStats(int hero) => HeroStats(hero, HeroLevel(hero));

        public (float hp, float attack, int armor) HeroStats(int hero, int level)
        {
            var stats = Data.Heroes[hero];
            float hp = stats.Hp + (level - 1) * stats.HpPerLevel + (Equipped(hero, TrialData.GearSlot.Necklace)?.Bonus ?? 0);
            float attack = stats.Attack + (level - 1) * stats.AttackPerLevel + (Equipped(hero, TrialData.GearSlot.Weapon)?.Bonus ?? 0);
            if (Array.IndexOf(formation, hero) >= 0)
            {
                var synergy = FormationSynergy(formation);
                if (synergy == TrialData.Role.Support) { hp *= 1.05f; attack *= 1.05f; }
                if (synergy == TrialData.Role.Attack) attack *= 1.1f;
            }
            long armor = stats.BaseArmor + (long)(level - 1) * stats.ArmorPerLevel
                + (Equipped(hero, TrialData.GearSlot.Armor)?.Bonus ?? 0);
            return (hp, attack, (int)Math.Min(int.MaxValue, armor));
        }

        private readonly int[] heroLevels;
        private readonly int[,] skillLevels;
        private int duplicateStreak;
        public int Tickets { get; private set; }
        public int TicketReward { get; private set; }
        public int HeroLevel(int hero) => (uint)hero < heroLevels.Length ? heroLevels[hero] : 0;
        public bool Owned(int hero) => HeroLevel(hero) > 0;

        public (long gold, long experience) HeroUpgradeCost(int hero)
        {
            long level = HeroLevel(hero);
            return (level * Data.UpgradeGold, level * Data.UpgradeXp);
        }

        public bool CanUpgradeHero(int hero)
        {
            int level = HeroLevel(hero);
            var cost = HeroUpgradeCost(hero);
            return level > 0 && level < int.MaxValue && Gold >= cost.gold && Experience >= cost.experience;
        }

        public bool UpgradeHero(int hero, Func<Progress, bool> save = null)
        {
            if (!CanUpgradeHero(hero)) return false;
            var cost = HeroUpgradeCost(hero);
            int nextGold = Gold - (int)cost.gold;
            int nextExperience = Experience - (int)cost.experience;
            int nextLevel = heroLevels[hero] + 1;
            if (save != null)
            {
                var progress = CaptureProgress();
                progress.gold = nextGold;
                progress.experience = nextExperience;
                Array.Find(progress.heroes, entry => entry.id == Data.Heroes[hero].Id).level = nextLevel;
                if (!save(progress)) return false;
            }
            Gold = nextGold;
            Experience = nextExperience;
            heroLevels[hero] = nextLevel;
            return true;
        }

        public int SkillLevel(int hero, int slot) => skillLevels[hero, slot];
        public int SkillCost(int hero, int slot) => Data.Skills[hero, slot].BaseGoldCost * SkillLevel(hero, slot);
        public bool CanTrain(int hero, int slot) => Owned(hero) && SkillLevel(hero, slot) < Mathf.Min(5, HeroLevel(hero))
            && Gold >= SkillCost(hero, slot);

        public bool Train(int hero, int slot)
        {
            if (!CanTrain(hero, slot)) return false;
            Gold -= SkillCost(hero, slot);
            skillLevels[hero, slot]++;
            return true;
        }

        private void UnlockHero(int hero)
        {
            heroLevels[hero] = 1;
            for (int slot = 0; slot < 3; slot++) skillLevels[hero, slot] = 1;
        }

        public struct Pull
        {
            public int Hero, Level;
            public bool IsNew;
        }

        public Pull[] Summon(int count)
        {
            if ((count != 1 && count != 10) || Tickets < count) return null;
            var pulls = new Pull[count];
            Tickets -= count;
            for (int i = 0; i < count; i++)
            {
                int hero = UnityEngine.Random.Range(0, heroLevels.Length);
                if (duplicateStreak >= 9)
                {
                    var missing = new List<int>();
                    for (int h = 0; h < heroLevels.Length; h++) if (!Owned(h)) missing.Add(h);
                    if (missing.Count > 0) hero = missing[UnityEngine.Random.Range(0, missing.Count)];
                }
                bool isNew = !Owned(hero);
                if (isNew) { UnlockHero(hero); duplicateStreak = 0; }
                else
                {
                    if (heroLevels[hero] < int.MaxValue) heroLevels[hero]++;
                    duplicateStreak = Array.IndexOf(heroLevels, 0) >= 0 ? duplicateStreak + 1 : 0;
                }
                pulls[i] = new Pull { Hero = hero, IsNew = isNew, Level = HeroLevel(hero) };
            }
            return pulls;
        }

        public TrialBattle(TrialData data)
        {
            Data = data;
            foreach (var gear in data.StarterGear) inventory.Add(new Item(gear));
            Heroes = data.Heroes.ConvertAll(hero => hero.Name).AsReadOnly();
            formation = (int[])data.StartingFormation.Clone();
            heroLevels = new int[Heroes.Count];
            skillLevels = new int[Heroes.Count, 3];
            Tickets = data.StartingTickets;
            for (int hero = 0; hero < Heroes.Count; hero++)
                if (data.StartWithAllHeroes || Array.IndexOf(formation, hero) >= 0) UnlockHero(hero);
            StartBattle();
        }

        public void StartBattle() => StartEncounter(null);

        public bool TryStartDungeon(TrialData.DungeonKind kind, int tierIndex)
        {
            if (ActiveDungeon != null || (uint)kind >= Data.Dungeons.GetLength(0) ||
                (uint)tierIndex >= Data.Dungeons.GetLength(1)) return false;
            var dungeon = Data.Dungeons[(int)kind, tierIndex];
            if (!CanEnterDungeon(dungeon)) return false;
            StartEncounter(dungeon);
            return true;
        }

        private void StartEncounter(TrialData.DungeonTier dungeon)
        {
            Units.Clear();
            ActiveDungeon = dungeon;
            ActiveRepeat = dungeon == null && RepeatStage > 0;
            ActiveStage = dungeon == null ? (ActiveRepeat ? RepeatStage : Stage) : 0;
            ActiveFarming = dungeon == null && (ActiveRepeat || Farming);
            Finished = false;
            Won = false;
            GearReward = null;
            GearConverted = false;
            GoldReward = ExperienceReward = TicketReward = 0;
            Result = "Battle started";
            Elapsed = 0;
            ActiveSynergy = FormationSynergy(formation);
            for (int i = 0; i < formation.Length; i++)
            {
                int index = formation[i];
                var hero = Data.Heroes[index];
                var stats = HeroStats(index);
                Add(hero, hero.Name, true, i, stats.hp, stats.attack);
                Units[i].HeroIndex = index;
                for (int slot = 0; slot < 3; slot++) Units[i].Skills.Ranks[slot] = SkillLevel(index, slot);
                Units[i].Level = HeroLevel(index);
                Units[i].Armor = stats.armor;
            }
            float power = dungeon?.Power ?? (ActiveStage == 0 ? Data.PracticePower : Data.BasePower + ActiveStage * Data.PowerPerStage);
            var bossEnemy = ActiveStage % Data.RoundLength == 0 ? Data.RoundBoss : Data.Enemies["boss"];
            for (int i = 0; i < formation.Length; i++)
            {
                bool boss = BossStage && i == Data.BossSlot;
                var enemy = boss ? bossEnemy : Data.Enemies["normal"];
                Add(enemy, boss ? enemy.Name : enemy.Name + " " + (i + 1), false, i,
                    enemy.Hp * power, enemy.Attack * power);
            }
        }

        private void Add(TrialData.Fighter stats, string name, bool ally, int lane, float hp, float attack)
        {
            var position = new Vector2(ally ? -Data.SpawnDistance : Data.SpawnDistance, (lane - 1) * Data.LaneSpacing);
            Units.Add(new Unit { Stats = stats, Name = name, Ally = ally, Position = position,
                Hp = hp, MaxHp = hp, Attack = attack, SwingSpeed = Mathf.Max(1, stats.ImpactSeconds / stats.Interval) });
        }

        private struct PendingHit
        {
            public float Total, Piercing, Strongest, Height;

            public void Add(Unit attacker, float amount, bool pierce)
            {
                if (pierce) Piercing += amount; else Total += amount;
                if (amount <= Strongest) return;
                Strongest = amount;
                Height = attacker.Stats.HitHeight;
            }
        }

        public void Tick(float dt)
        {
            if (Finished || dt <= 0) return;
            Elapsed += dt;
            // Accumulate hits first so a simultaneous wipe is consistently a defeat.
            var hits = new PendingHit[Units.Count];
            foreach (Unit unit in Units)
            {
                unit.Walking = false;
                if (!unit.Alive) continue;
                UpdateSkillEffects(unit, dt);
                if (unit.Skills.StunnedUntil > Elapsed) continue;
                if (unit.Skills.Taunter != null && unit.Skills.Taunter.Alive) unit.Target = unit.Skills.Taunter;
                if (unit.Target == null || !unit.Target.Alive)
                {
                    if (unit.WindingUp) unit.SwingStart = float.NegativeInfinity;
                    unit.WindingUp = false;
                    unit.Target = null;
                    float closest = float.MaxValue;
                    foreach (Unit other in Units)
                    {
                        float distance = (other.Position - unit.Position).sqrMagnitude;
                        if (other.Alive && other.Ally != unit.Ally && distance < closest)
                        { closest = distance; unit.Target = other; }
                    }
                }
                float speed = SpeedMultiplier(unit);
                unit.Cooldown = Mathf.Max(0, unit.Cooldown - dt * speed);
                if (unit.Target == null) continue;
                var destination = unit.Target.Position;
                bool approaching;
                if (unit.Stats.ContactDistance > 0)
                {
                    // A fixed-reach weapon needs the target in its swing plane and at the drawn distance.
                    destination.x += unit.Ally ? -unit.Stats.ContactDistance : unit.Stats.ContactDistance;
                    approaching = Vector2.Distance(unit.Position, destination) > .01f;
                }
                else
                {
                    destination.y = unit.Position.y;
                    approaching = Mathf.Abs(unit.Position.x - destination.x) > unit.Stats.Range;
                }
                if (approaching)
                {
                    // A landed strike must finish its recovery before another approach.
                    if (!unit.WindingUp && (Elapsed - unit.SwingStart) * unit.SwingSpeed < unit.Stats.AttackDuration)
                        continue;
                    unit.Walking = true;
                    unit.WindingUp = false;
                    unit.SwingStart = float.NegativeInfinity;
                    unit.Position = Vector2.MoveTowards(unit.Position, destination, dt * unit.Stats.Speed);
                }
                else if (unit.Cooldown <= 0)
                {
                    float impact = unit.Stats.ImpactSeconds;
                    if (impact > 0)
                    {
                        if (!unit.WindingUp)
                        {
                            unit.SwingStart = Elapsed;
                            unit.SwingSpeed = Mathf.Max(1, impact / unit.Stats.Interval) * speed;
                            unit.WindingUp = true;
                        }
                        if ((Elapsed - unit.SwingStart) * unit.SwingSpeed + 0.00001f < impact) continue;
                        unit.WindingUp = false;
                    }
                    if (!unit.Skills.Blinded) Strike(unit, unit.Target, AttackPower(unit), hits);
                    unit.Skills.Blinded = false;
                    if (unit.Skills.Taunter != null) { unit.Skills.Taunter = null; unit.Target = null; }
                    unit.Cooldown = Mathf.Max(0, unit.Stats.Interval - impact / unit.SwingSpeed * speed);
                    unit.Attacks++;
                }
                if (unit.Ally && unit.Target != null) TryCastSkill(unit, hits);
            }
            bool allies = false, enemies = false;
            for (int i = 0; i < Units.Count; i++)
            {
                if (hits[i].Total > 0 || hits[i].Piercing > 0)
                {
                    Unit unit = Units[i];
                    float armor = unit.Armor * (1 + unit.Skills.DefenseUp.Value(Elapsed)) * (1 - unit.Skills.DefenseDown.Value(Elapsed));
                    float damage = hits[i].Total * (100f / (100f + armor)) + hits[i].Piercing;
                    if (unit.Ally && ActiveSynergy == TrialData.Role.Defense) damage *= 0.90f;
                    unit.LastDamage = Mathf.Min(unit.Hp, damage);
                    unit.LastHitTime = Elapsed;
                    unit.LastHitHeight = hits[i].Height;
                    unit.Hp -= unit.LastDamage;
                }
                if (!Units[i].Alive) continue;
                if (Units[i].Ally) allies = true; else enemies = true;
            }
            if (!allies || Elapsed >= Limit) EndBattle(false);
            else if (!enemies) EndBattle(true);
        }

        public void ForceTimeout() { if (!Finished) { Elapsed = Limit; EndBattle(false); } }

        public void RetreatFromDungeon()
        {
            if (ActiveDungeon != null) EndBattle(false);
        }

        private void EndBattle(bool won)
        {
            if (Finished) return;
            Finished = true;
            Won = won;
            foreach (var unit in Units)
            {
                unit.Walking = false;
                // Let attacks that already hit finish their recovery animation.
                if (unit.WindingUp) unit.SwingStart = float.NegativeInfinity;
                unit.WindingUp = false;
            }
            if (ActiveDungeon != null)
            {
                if (won) (GoldReward, ExperienceReward) = CreditDungeonVictory(ActiveDungeon);
                Result = won ? "Dungeon cleared" : "Dungeon defeat";
                return;
            }
            if (won)
            {
                int reward = ActiveStage == 0 ? Data.PracticeGold : Data.BaseGold + ActiveStage * Data.GoldPerStage;
                bool first = !ActiveFarming && ActiveStage > Highest;
                TicketReward = first ? (BossStage ? Data.FirstBossTickets : Data.FirstWinTickets) : 0;
                Tickets += TicketReward;
                int multiplier = first ? Data.FirstWinMultiplier : 1;
                GoldReward = reward * multiplier;
                ExperienceReward = (ActiveStage == 0 ? Data.PracticeXp : Data.BaseXp + ActiveStage * Data.XpPerStage) * multiplier;
                Gold = AddCurrency(Gold, GoldReward);
                Experience = AddCurrency(Experience, ExperienceReward);
                RollGearReward();
                Result = "VICTORY: +" + GoldReward + " gold";
                if (!ActiveFarming) { Highest = ActiveStage; Stage = Next; }
            }
            else if (ActiveRepeat) Result = "DEFEAT: no reward.";
            else
            {
                Result = "DEFEAT: no reward. Farming lower stage.";
                Stage = Farming ? Mathf.Max(0, Stage - 1) : Highest;
                Farming = true;
            }
        }

        public void Retry()
        {
            if (ActiveDungeon != null) return;
            RepeatStage = 0;
            Stage = Next;
            Farming = false;
            StartBattle();
        }

        public string StageName(int stage) => stage == 0 ? "PRACTICE" :
            "ROUND " + ((stage - 1) / Data.RoundLength + 1) + " / STAGE " + ((stage - 1) % Data.RoundLength + 1);
    }
}
