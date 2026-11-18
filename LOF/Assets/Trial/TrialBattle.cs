using System;
using System.Collections.Generic;
using UnityEngine;

namespace AFKTrial
{
    // Deliberately small, in-memory test model. Values are NOT final balance data.
    public sealed class TrialBattle
    {
        public readonly TrialData Data;
        public readonly IReadOnlyList<string> Heroes;
        private readonly int[] formation;
        private bool BossStage => Stage > 0 && Stage % Data.BossEvery == 0;
        public int HeroAt(int slot) => formation[slot];

        public void ApplyFormation(int[] heroes)
        {
            if (heroes == null || heroes.Length != formation.Length)
                throw new ArgumentException("Choose three heroes.");
            var seen = new HashSet<int>();
            foreach (int hero in heroes)
                if (hero < 0 || hero >= Heroes.Count || !seen.Add(hero))
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
            public int Attacks;
            public bool Walking, WindingUp;
            public float SwingStart = float.NegativeInfinity;
            public float LastHitTime = float.NegativeInfinity;
            public float LastDamage;
            public bool Alive => Hp > 0;
        }

        public readonly List<Unit> Units = new List<Unit>();
        public int Highest { get; private set; }
        public int Stage { get; private set; } = 1;
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
        public string Result { get; private set; } = "Battle started";

        private readonly float iceImpact;
        public TrialBattle(TrialData data, float iceImpact)
        {
            Data = data;
            Heroes = data.Heroes.ConvertAll(hero => hero.Name).AsReadOnly();
            formation = (int[])data.StartingFormation.Clone();
            this.iceImpact = iceImpact;
            StartBattle();
        }

        public void StartBattle()
        {
            Units.Clear();
            Finished = false;
            Elapsed = 0;
            BattleLevel = Level;
            for (int i = 0; i < formation.Length; i++)
            {
                var hero = Data.Heroes[formation[i]];
                Add(hero, hero.Name, true, i, hero.Hp + (Level - 1) * hero.HpPerLevel,
                    hero.Attack + (Level - 1) * hero.AttackPerLevel);
            }
            float power = Stage == 0 ? Data.PracticePower : Data.BasePower + Stage * Data.PowerPerStage;
            for (int i = 0; i < formation.Length; i++)
            {
                bool boss = BossStage && i == Data.BossSlot;
                var enemy = Data.Enemies[boss ? "boss" : "normal"];
                Add(enemy, boss ? enemy.Name : enemy.Name + " " + (i + 1), false, i,
                    enemy.Hp * power, enemy.Attack * power);
            }
        }

        private void Add(TrialData.Fighter stats, string name, bool ally, int lane, float hp, float attack)
        {
            var position = new Vector2(ally ? -Data.SpawnDistance : Data.SpawnDistance, (lane - 1) * Data.LaneSpacing);
            Units.Add(new Unit { Stats = stats, Name = name, Ally = ally, Position = position,
                Hp = hp, MaxHp = hp, Attack = attack, SwingSpeed = Mathf.Max(1, iceImpact / stats.Interval) });
        }

        public void Tick(float dt)
        {
            if (Finished || dt <= 0) return;
            Elapsed += dt;
            // Accumulate hits first so a simultaneous wipe is consistently a defeat.
            float[] hits = new float[Units.Count];
            foreach (Unit unit in Units)
            {
                unit.Walking = false;
                if (!unit.Alive) continue;
                if (unit.Target == null || !unit.Target.Alive)
                {
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
                if (Mathf.Abs(unit.Position.x - unit.Target.Position.x) > unit.Stats.Range)
                {
                    unit.Walking = true;
                    unit.WindingUp = false;
                    unit.Position.x = Mathf.MoveTowards(unit.Position.x, unit.Target.Position.x, dt * unit.Stats.Speed);
                }
                else if (unit.Cooldown <= 0)
                {
                    bool iceKnight = unit.Ally && unit.Stats.Animation == "ice_knight";
                    if (iceKnight)
                    {
                        if (!unit.WindingUp)
                        {
                            unit.SwingStart = Elapsed;
                            unit.WindingUp = true;
                        }
                        if ((Elapsed - unit.SwingStart) * unit.SwingSpeed + 0.00001f < iceImpact) continue;
                        unit.WindingUp = false;
                    }
                    hits[Units.IndexOf(unit.Target)] += unit.Attack;
                    unit.Cooldown = unit.Stats.Interval - (iceKnight ? iceImpact / unit.SwingSpeed : 0);
                    unit.Attacks++;
                }
            }
            bool allies = false, enemies = false;
            for (int i = 0; i < Units.Count; i++)
            {
                if (hits[i] > 0)
                {
                    Unit unit = Units[i];
                    unit.LastDamage = Mathf.Min(unit.Hp, hits[i]);
                    unit.LastHitTime = Elapsed;
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
            if (won)
            {
                int reward = Stage == 0 ? Data.PracticeGold : Data.BaseGold + Stage * Data.GoldPerStage;
                bool first = !Farming && Stage > Highest;
                Gold += reward * (first ? Data.FirstWinMultiplier : 1);
                Experience += (Stage == 0 ? Data.PracticeXp : Data.BaseXp + Stage * Data.XpPerStage) * (first ? Data.FirstWinMultiplier : 1);
                Result = "VICTORY: +" + reward * (first ? Data.FirstWinMultiplier : 1) + " gold";
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
