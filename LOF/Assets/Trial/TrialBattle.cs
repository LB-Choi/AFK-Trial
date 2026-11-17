using System;
using System.Collections.Generic;
using UnityEngine;

namespace AFKTrial
{
    // Deliberately small, in-memory test model. Values are NOT final balance data.
    public sealed class TrialBattle
    {
        public sealed class Unit
        {
            public string Name;
            public bool Ally;
            public float Hp, MaxHp, Attack, Cooldown;
            public Vector2 Position;
            public Unit Target;
            public int Attacks;
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
        public int Cost => Level * 20;
        public int XpCost => Level * 10;
        public float Elapsed { get; private set; }
        public float Limit => Stage > 0 && Stage % 5 == 0 ? 60 : 30;
        public bool Finished { get; private set; }
        public string Result { get; private set; } = "Battle started";

        public TrialBattle() { StartBattle(); }

        public void StartBattle()
        {
            Units.Clear();
            Finished = false;
            Elapsed = 0;
            BattleLevel = Level;
            for (int i = 0; i < 3; i++)
                Add("ALLY " + (i + 1), true, new Vector2(-1.2f + i * 1.2f, -2),
                    100 + (Level - 1) * 35, 16 + (Level - 1) * 6);
            bool boss = Stage > 0 && Stage % 5 == 0;
            float power = Stage == 0 ? 0.3f : 0.65f + Stage * 0.2f;
            for (int i = 0; i < (boss ? 1 : 3); i++)
                Add(boss ? "BOSS" : "ENEMY " + (i + 1), false,
                    new Vector2(boss ? 0 : -1.2f + i * 1.2f, 2),
                    (boss ? 320 : 75) * power, (boss ? 28 : 11) * power);
        }

        private void Add(string name, bool ally, Vector2 position, float hp, float attack)
        {
            Units.Add(new Unit { Name = name, Ally = ally, Position = position,
                Hp = hp, MaxHp = hp, Attack = attack });
        }

        public void Tick(float dt)
        {
            if (Finished || dt <= 0) return;
            Elapsed += dt;
            // Accumulate hits first so a simultaneous wipe is consistently a defeat.
            float[] hits = new float[Units.Count];
            foreach (Unit unit in Units)
            {
                if (!unit.Alive) continue;
                if (unit.Target == null || !unit.Target.Alive)
                {
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
                if (Vector2.Distance(unit.Position, unit.Target.Position) > 0.65f)
                    unit.Position = Vector2.MoveTowards(unit.Position, unit.Target.Position, dt * 1.3f);
                else if (unit.Cooldown <= 0)
                {
                    hits[Units.IndexOf(unit.Target)] += unit.Attack;
                    unit.Cooldown = 1;
                    unit.Attacks++;
                }
            }
            bool allies = false, enemies = false;
            for (int i = 0; i < Units.Count; i++)
            {
                Units[i].Hp = Mathf.Max(0, Units[i].Hp - hits[i]);
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
                int reward = Stage == 0 ? 5 : 10 + Stage * 5;
                bool first = !Farming && Stage > Highest;
                Gold += reward * (first ? 3 : 1);
                Experience += (Stage == 0 ? 3 : 5 + Stage * 3) * (first ? 3 : 1);
                Result = "VICTORY: +" + reward * (first ? 3 : 1) + " gold";
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

        public static string StageName(int stage) => stage == 0 ? "PRACTICE" :
            "ROUND " + ((stage - 1) / 10 + 1) + " / STAGE " + ((stage - 1) % 10 + 1);
    }
}
