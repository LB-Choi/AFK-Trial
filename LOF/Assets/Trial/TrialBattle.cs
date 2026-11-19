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
            StartBattle();
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
            public int Attacks, Armor;
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
        public int Next => Highest + 1;
        public bool Farming { get; private set; }
        public int Level { get; private set; } = 1;
        public int BattleLevel { get; private set; } = 1;
        public int Gold { get; private set; }
        public int Experience { get; private set; }
        public int Cost => Level * Data.UpgradeGold;
        public int XpCost => Level * Data.UpgradeXp;
        public float Elapsed { get; private set; }
        public float Limit => BossStage ? Data.BossSeconds : Data.NormalSeconds;
        public bool Finished { get; private set; }
        public bool Won { get; private set; }
        public int GoldReward { get; private set; }
        public int ExperienceReward { get; private set; }
        public string Result { get; private set; } = "Battle started";

        public sealed class Item
        {
            public readonly TrialData.Gear Gear;
            public int Owner { get; internal set; } = -1;
            public Item(TrialData.Gear gear) { Gear = gear; }
        }

        private readonly List<Item> inventory = new List<Item>();
        public IReadOnlyList<Item> Inventory => inventory;
        public Item Equipped(int hero, TrialData.GearSlot slot) =>
            inventory.Find(item => item.Owner == hero && item.Gear.Slot == slot);

        public bool Equip(int hero, Item item)
        {
            if (hero < 0 || hero >= Heroes.Count || !Owned(hero) || item == null || item.Owner >= 0 || !inventory.Contains(item)) return false;
            var previous = Equipped(hero, item.Gear.Slot);
            if (previous != null) previous.Owner = -1;
            item.Owner = hero;
            RefreshGear(hero);
            return true;
        }

        public bool Unequip(int hero, TrialData.GearSlot slot)
        {
            if (hero < 0 || hero >= Heroes.Count) return false;
            var item = Equipped(hero, slot);
            if (item == null) return false;
            item.Owner = -1;
            RefreshGear(hero);
            return true;
        }

        public bool Sell(Item item)
        {
            if (item == null || item.Owner >= 0 || !inventory.Remove(item)) return false;
            Gold = AddCurrency(Gold, item.Gear.SellGold);
            return true;
        }

        public (float hp, float attack, int armor) HeroStats(int hero, int level)
        {
            level = HeroLevel(hero, level);
            var stats = Data.Heroes[hero];
            float hp = stats.Hp + (level - 1) * stats.HpPerLevel + (Equipped(hero, TrialData.GearSlot.Necklace)?.Gear.Bonus ?? 0);
            float attack = stats.Attack + (level - 1) * stats.AttackPerLevel + (Equipped(hero, TrialData.GearSlot.Weapon)?.Gear.Bonus ?? 0);
            if (Array.IndexOf(formation, hero) >= 0)
            {
                if (ActiveSynergy == TrialData.Role.Support) { hp *= 1.05f; attack *= 1.05f; }
                if (ActiveSynergy == TrialData.Role.Attack) attack *= 1.1f;
            }
            return (hp, attack, Equipped(hero, TrialData.GearSlot.Armor)?.Gear.Bonus ?? 0);
        }

        private void RefreshGear(int hero)
        {
            int slot = Array.IndexOf(formation, hero);
            if (slot < 0) return;
            var unit = Units[slot];
            var stats = HeroStats(hero, BattleLevel);
            unit.Hp = Mathf.Clamp01(unit.Hp / unit.MaxHp) * stats.hp;
            unit.MaxHp = stats.hp;
            unit.Attack = stats.attack;
            unit.Armor = stats.armor;
        }

        private readonly bool[] owned;
        private readonly int[] levelOffset;
        private readonly int[,] skillLevels;
        private int duplicateStreak;
        public int Tickets { get; private set; }
        public int TicketReward { get; private set; }
        public bool Owned(int hero) => owned[hero];
        public int HeroLevel(int hero, int teamLevel) => owned[hero] ? Mathf.Max(1, teamLevel + levelOffset[hero]) : 0;
        public int SkillLevel(int hero, int slot) => skillLevels[hero, slot];
        public int SkillCost(int hero, int slot) => Data.Skills[hero, slot].BaseGoldCost * SkillLevel(hero, slot);
        public bool CanTrain(int hero, int slot) => Owned(hero) && SkillLevel(hero, slot) < Mathf.Min(5, HeroLevel(hero, Level))
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
            owned[hero] = true;
            levelOffset[hero] = 1 - Level;
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
                int hero = UnityEngine.Random.Range(0, owned.Length);
                if (duplicateStreak >= 9)
                {
                    var missing = new List<int>();
                    for (int h = 0; h < owned.Length; h++) if (!owned[h]) missing.Add(h);
                    if (missing.Count > 0) hero = missing[UnityEngine.Random.Range(0, missing.Count)];
                }
                bool isNew = !owned[hero];
                if (isNew) { UnlockHero(hero); duplicateStreak = 0; }
                else
                {
                    levelOffset[hero]++;
                    duplicateStreak = Array.IndexOf(owned, false) >= 0 ? duplicateStreak + 1 : 0;
                    RefreshGear(hero);
                }
                pulls[i] = new Pull { Hero = hero, IsNew = isNew, Level = HeroLevel(hero, Level) };
            }
            return pulls;
        }

        public TrialBattle(TrialData data)
        {
            Data = data;
            foreach (var gear in data.StarterGear) inventory.Add(new Item(gear));
            Heroes = data.Heroes.ConvertAll(hero => hero.Name).AsReadOnly();
            formation = (int[])data.StartingFormation.Clone();
            owned = new bool[Heroes.Count];
            levelOffset = new int[Heroes.Count];
            skillLevels = new int[Heroes.Count, 3];
            Tickets = data.StartingTickets;
            for (int hero = 0; hero < Heroes.Count; hero++)
                if (data.StartWithAllHeroes || Array.IndexOf(formation, hero) >= 0) UnlockHero(hero);
            StartBattle();
        }

        public void StartBattle()
        {
            Units.Clear();
            ActiveStage = Stage;
            ActiveFarming = Farming;
            Finished = false;
            Won = false;
            GoldReward = ExperienceReward = TicketReward = 0;
            Result = "Battle started";
            Elapsed = 0;
            BattleLevel = Level;
            ActiveSynergy = FormationSynergy(formation);
            for (int i = 0; i < formation.Length; i++)
            {
                int index = formation[i];
                var hero = Data.Heroes[index];
                var stats = HeroStats(index, BattleLevel);
                Add(hero, hero.Name, true, i, stats.hp, stats.attack);
                Units[i].Armor = stats.armor;
            }
            float power = Stage == 0 ? Data.PracticePower : Data.BasePower + Stage * Data.PowerPerStage;
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
            public float Total, Strongest, Height;

            public void Add(Unit attacker)
            {
                Total += attacker.Attack;
                if (attacker.Attack <= Strongest) return;
                Strongest = attacker.Attack;
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
                unit.Cooldown = Mathf.Max(0, unit.Cooldown - dt);
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
                            unit.WindingUp = true;
                        }
                        if ((Elapsed - unit.SwingStart) * unit.SwingSpeed + 0.00001f < impact) continue;
                        unit.WindingUp = false;
                    }
                    hits[Units.IndexOf(unit.Target)].Add(unit);
                    unit.Cooldown = unit.Stats.Interval - impact / unit.SwingSpeed;
                    unit.Attacks++;
                }
            }
            bool allies = false, enemies = false;
            for (int i = 0; i < Units.Count; i++)
            {
                if (hits[i].Total > 0)
                {
                    Unit unit = Units[i];
                    float damage = hits[i].Total * (100f / (100f + unit.Armor));
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
            if (won)
            {
                int reward = Stage == 0 ? Data.PracticeGold : Data.BaseGold + Stage * Data.GoldPerStage;
                bool first = !Farming && Stage > Highest;
                TicketReward = first ? (BossStage ? Data.FirstBossTickets : Data.FirstWinTickets) : 0;
                Tickets += TicketReward;
                int multiplier = first ? Data.FirstWinMultiplier : 1;
                GoldReward = reward * multiplier;
                ExperienceReward = (Stage == 0 ? Data.PracticeXp : Data.BaseXp + Stage * Data.XpPerStage) * multiplier;
                Gold = AddCurrency(Gold, GoldReward);
                Experience = AddCurrency(Experience, ExperienceReward);
                Result = "VICTORY: +" + GoldReward + " gold";
                if (!Farming) { Highest = Stage; Stage = Next; }
            }
            else
            {
                Result = "DEFEAT: no reward. Farming lower stage.";
                Stage = Farming ? Mathf.Max(0, Stage - 1) : Highest;
                Farming = true;
            }
        }

        public bool Upgrade()
        {
            if (Gold < Cost || Experience < XpCost) return false;
            Gold -= Cost;
            Experience -= XpCost;
            Level++;
            return true;
        }

        public void Retry()
        {
            Stage = Next;
            Farming = false;
            StartBattle();
        }

        public string StageName(int stage) => stage == 0 ? "PRACTICE" :
            "ROUND " + ((stage - 1) / Data.RoundLength + 1) + " / STAGE " + ((stage - 1) % Data.RoundLength + 1);
    }
}
