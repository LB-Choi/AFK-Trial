using System;
using UnityEngine;

namespace AFKTrial
{
    public enum SkillEffect
    {
        None, Wave, Slow, Power, Pierce, Sunder, CurrentHp, Weaken, TeamHaste, Rally,
        Fortify, Guard, Stun, Mend, TeamArmor, Double, DoublePower, SwordOath, Taunt,
        Blind, Equalize, MissingHp, Frenzy, Flurry
    }

    public sealed partial class TrialBattle
    {
        internal struct TimedBonus
        {
            private float amount, end;
            public float Value(float now) => now < end ? amount : 0;
            public void Set(float value, float until) { amount = value; end = until; }
        }

        internal sealed class SkillState
        {
            internal TimedBonus AttackUp, AttackDown, DefenseUp, DefenseDown, Haste, Slow, Burst;
            internal float StunnedUntil, HealLeft, HealRate;
            internal bool Block, Blinded;
            internal Unit Taunter;
            internal readonly float[] LastCast = new float[3];
            internal readonly int[] Ranks = new int[3];
            internal int LastAttackCount;
        }

        private float AttackPower(Unit unit) => unit.Attack * (1 + unit.Skills.AttackUp.Value(Elapsed)) *
            (1 + unit.Skills.Burst.Value(Elapsed)) * (1 - unit.Skills.AttackDown.Value(Elapsed));

        private float SpeedMultiplier(Unit unit) => (1 + unit.Skills.Haste.Value(Elapsed)) * (1 - unit.Skills.Slow.Value(Elapsed));

        private void UpdateSkillEffects(Unit unit, float dt)
        {
            var state = unit.Skills;
            float duration = Mathf.Min(dt, state.HealLeft);
            unit.Hp = Mathf.Min(unit.MaxHp, unit.Hp + state.HealRate * duration);
            state.HealLeft -= duration;
        }

        private void Strike(Unit caster, Unit target, float amount, PendingHit[] hits, bool pierce = false)
        {
            if (target.Skills.Block) { target.Skills.Block = false; return; }
            hits[Units.IndexOf(target)].Add(caster, amount, pierce);
        }

        private void TryCastSkill(Unit caster, PendingHit[] hits)
        {
            var state = caster.Skills;
            for (int slot = 0; slot < 3; slot++)
            {
                var skill = Data.Skills[caster.HeroIndex, slot];
                if (skill.Effect == SkillEffect.None) continue;
                int rank = state.Ranks[slot];
                bool reducesInterval = skill.Effect == SkillEffect.Double || skill.Effect == SkillEffect.Equalize ||
                    skill.Effect == SkillEffect.Guard && caster.Stats.Id == "tower_knight";
                int interval = Math.Max(1, skill.Interval - (reducesInterval ? rank : 0));
                if (slot == 0)
                {
                    if (caster.Attacks - state.LastAttackCount < interval) continue;
                    state.LastAttackCount = caster.Attacks;
                }
                else if (Elapsed - state.LastCast[slot] < interval) continue;
                state.LastCast[slot] = Elapsed;
                caster.LastSkill = skill.Name;
                caster.LastSkillTime = Elapsed;
                Cast(caster, skill.Effect, rank, hits);
                return;
            }
        }

        private void Cast(Unit caster, SkillEffect effect, int rank, PendingHit[] hits)
        {
            var target = caster.Target;
            var state = caster.Skills;
            float fraction = rank * 0.01f, attack = AttackPower(caster);
            bool boss = BossStage && target == Units[3 + Data.BossSlot];
            switch (effect)
            {
                case SkillEffect.Wave:
                    foreach (var foe in Units) if (!foe.Ally && foe.Alive) Strike(caster, foe, attack * 0.5f * rank, hits);
                    break;
                case SkillEffect.Slow:
                    if (!boss) target.Skills.Slow.Set(fraction, Elapsed + 4);
                    break;
                case SkillEffect.Power:
                case SkillEffect.SwordOath:
                    state.AttackUp.Set(fraction, Elapsed + (effect == SkillEffect.Power ? 6 : 7));
                    break;
                case SkillEffect.Pierce: Strike(caster, target, attack, hits, true); break;
                case SkillEffect.Sunder: target.Skills.DefenseDown.Set(fraction, Elapsed + 3); break;
                case SkillEffect.CurrentHp: Strike(caster, target, target.Hp * fraction, hits, true); break;
                case SkillEffect.Weaken:
                    Strike(caster, target, attack, hits);
                    target.Skills.AttackDown.Set(fraction, Elapsed + 3);
                    break;
                case SkillEffect.TeamHaste:
                case SkillEffect.TeamArmor:
                case SkillEffect.Rally:
                case SkillEffect.Mend:
                    foreach (var ally in Units)
                    {
                        if (!ally.Ally || !ally.Alive) continue;
                        if (effect == SkillEffect.TeamHaste) ally.Skills.Haste.Set(fraction, Elapsed + 4);
                        if (effect == SkillEffect.TeamArmor) ally.Skills.DefenseUp.Set(fraction, Elapsed + 5);
                        if (effect == SkillEffect.Rally && ally != caster) ally.Skills.AttackUp.Set(fraction, Elapsed + 6);
                        if (effect == SkillEffect.Mend)
                        {
                            ally.Skills.HealRate = (ally.MaxHp - ally.Hp) * fraction / 4;
                            ally.Skills.HealLeft = 4;
                        }
                    }
                    break;
                case SkillEffect.Fortify: state.DefenseUp.Set(fraction, Elapsed + 5); break;
                case SkillEffect.Guard: state.Block = true; break;
                case SkillEffect.Stun:
                    Strike(caster, target, attack, hits);
                    target.Skills.StunnedUntil = Elapsed + (boss ? 0.1f : 0.2f) * rank;
                    break;
                case SkillEffect.Double: Strike(caster, target, attack * 2, hits); break;
                case SkillEffect.DoublePower: state.Burst.Set(1, Elapsed + rank); break;
                case SkillEffect.Taunt:
                case SkillEffect.Blind:
                    foreach (var foe in Units)
                        if (!foe.Ally && foe.Alive)
                        {
                            if (effect == SkillEffect.Blind) foe.Skills.Blinded = true;
                            else foe.Skills.Taunter = caster;
                        }
                    break;
                case SkillEffect.Equalize:
                    float hp = 0, capacity = 0;
                    foreach (var ally in Units)
                        if (ally.Ally && ally.Alive) { hp += ally.Hp; capacity += ally.MaxHp; }
                    foreach (var ally in Units)
                        if (ally.Ally && ally.Alive) ally.Hp = ally.MaxHp * (hp / capacity);
                    break;
                case SkillEffect.MissingHp: Strike(caster, target, (target.MaxHp - target.Hp) * fraction, hits, true); break;
                case SkillEffect.Frenzy: state.Haste.Set(rank - 1, Elapsed + 5); break;
                case SkillEffect.Flurry:
                    for (int i = 0; i < 2 * rank; i++) Strike(caster, target, attack, hits);
                    break;
            }
        }
    }
}
