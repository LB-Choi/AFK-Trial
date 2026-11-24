using System;
using UnityEngine;

namespace AFKTrial
{
    public enum SkillEffect
    {
        None, Wave, Slow, Power, Pierce, Sunder, CurrentHp, Weaken, TeamHaste, Rally,
        Fortify, Guard, Stun, Mend, TeamArmor, Double, DoublePower, SwordOath, Taunt,
        Blind, Equalize, MissingHp, Frenzy, Flurry, ArmorStrike, TimedTaunt, DrainCurrentHp
    }

    public sealed partial class TrialBattle
    {
        public enum SkillCue
        {
            AttackUp, DefenseUp, ChainHeal, DefenseDown, AttackDown, Taunt,
            KatanaCombo, FrostSwordEnchant, StunStars, Bleeding
        }

        // One notification per affected unit, timestamped on the battle clock.
        public event Action<SkillCue, Unit, Unit, float> SkillApplied;

        internal struct TimedBonus
        {
            private float amount, end;
            public float Until => end;
            public float Value(float now) => now < end ? amount : 0;
            public void Set(float value, float until) { amount = value; end = until; }
        }

        internal sealed class SkillState
        {
            internal TimedBonus AttackUp, AttackDown, DefenseUp, DefenseDown, Haste, Slow;
            internal float StunnedUntil, HealLeft, HealRate;
            internal float BleedUntil;
            internal float NextBleedTick;
            internal float DamageReduction;
            internal float BlindUntil, TauntUntil;
            internal Unit Taunter;
            internal readonly float[] LastCast = new float[3];
            internal readonly int[] Ranks = new int[3];
            internal int LastAttackCount;
        }

        private float AttackPower(Unit unit) => unit.Attack * (1 + unit.Skills.AttackUp.Value(Elapsed)) *
            Mathf.Max(0f, 1 - unit.Skills.AttackDown.Value(Elapsed));

        private float SpeedMultiplier(Unit unit) => (1 + unit.Skills.Haste.Value(Elapsed)) * Mathf.Max(0f, 1 - unit.Skills.Slow.Value(Elapsed));

        private float EffectiveArmor(Unit unit) => unit.Armor * (1 + unit.Skills.DefenseUp.Value(Elapsed)) *
            Mathf.Max(0f, 1 - unit.Skills.DefenseDown.Value(Elapsed));

        private void UpdateSkillEffects(Unit unit, float dt)
        {
            var state = unit.Skills;
            float duration = Mathf.Min(dt, state.HealLeft);
            unit.Hp = Mathf.Min(unit.MaxHp, unit.Hp + state.HealRate * duration);
            state.HealLeft -= duration;
        }

        private void Strike(Unit caster, Unit target, float amount, PendingHit[] hits, bool pierce = false)
        {
            hits[Units.IndexOf(target)].Add(caster, amount, pierce);
            if (amount > 0) WeaponSoundRequested?.Invoke(caster, true, caster.SpecialAttackPending);
        }

        private void BasicStrike(Unit caster, PendingHit[] hits)
        {
            var target = caster.Target;
            int every = caster.Stats.SkillEvery;
            bool special = every > 0 && caster == ActiveBoss && caster.SpecialAttackPending;
            bool bash = special && caster.Stats.Id == "boss";
            // Retain semantic notifications; playback uses the typed weapon event from Strike.
            SoundRequested?.Invoke(special ? SoundCue.BossSpecial : SoundCue.Hit);
            int round = ActiveStage > 0 ? (ActiveStage - 1) / Data.RoundLength + 1 : 1;
            Strike(caster, target, AttackPower(caster) * (bash ? round : 1), hits);
            if (!special) return;
            if (bash)
            {
                target.Skills.StunnedUntil = Elapsed + .3f;
                if (target.WindingUp) { target.WindingUp = false; target.SwingStart = float.NegativeInfinity; }
                SkillApplied?.Invoke(SkillCue.StunStars, caster, target, Elapsed);
            }
            else if (caster.Stats.Id == "boss2")
            {
                if (target.Skills.NextBleedTick == 0f) target.Skills.NextBleedTick = Elapsed + 1f;
                target.Skills.BleedUntil = Elapsed + round;
                SkillApplied?.Invoke(SkillCue.Bleeding, caster, target, Elapsed);
            }
        }

        private void TickBleed(Unit target)
        {
            var state = target.Skills;
            const float tolerance = .0001f;
            if (state.NextBleedTick == 0f) return;
            while (state.NextBleedTick <= Elapsed + tolerance && state.NextBleedTick <= state.BleedUntil + tolerance)
            {
                float damage = target.Hp * .001f * target.DamageTakenMultiplier;
                target.Hp -= damage;
                target.LastDamage = damage;
                target.LastHitTime = Elapsed;
                target.LastDamageWasBleed = true;
                state.NextBleedTick += 1f;
            }
            if (state.BleedUntil <= Elapsed + tolerance) state.NextBleedTick = 0f;
        }

        private void TryCastSkill(Unit caster, PendingHit[] hits)
        {
            var state = caster.Skills;
            if (Elapsed - caster.SkillStart < 1f || caster.WindingUp || (Elapsed - caster.SwingStart) * caster.SwingSpeed < caster.Stats.AttackDuration) return;
            for (int slot = 0; slot < 3; slot++)
            {
                var skill = Data.Skills[caster.HeroIndex, slot];
                if (skill.Effect == SkillEffect.None) continue;
                int rank = state.Ranks[slot];
                int interval = skill.Interval;
                if (slot == 0)
                {
                    if (caster.Attacks - state.LastAttackCount < interval) continue;
                    state.LastAttackCount = caster.Attacks;
                }
                else if (Elapsed - state.LastCast[slot] < interval) continue;
                state.LastCast[slot] = Elapsed;
                caster.LastSkill = skill.Name;
                caster.LastSkillTime = Elapsed;
                caster.SkillStart = Elapsed;
                caster.LastSkillSlot = slot;
                caster.SkillRank = rank;
                caster.SkillHit = caster.SkillSwing = 0;
                caster.SkillTarget = caster.Target;
                caster.AnimatedSkill = skill.Effect;
                caster.Walking = false;
                return;
            }
        }

        private void Cast(Unit caster, SkillEffect effect, int rank, PendingHit[] hits)
        {
            var target = caster.Target;
            var state = caster.Skills;
            var numbers = NumbersFor(effect, rank, caster.Stats.Id);
            float fraction = numbers.Fraction, attack = AttackPower(caster);
            bool boss = BossStage && target == Units[3 + Data.BossSlot];
            switch (effect)
            {
                case SkillEffect.Wave:
                    foreach (var foe in Units) if (!foe.Ally && foe.Alive) Strike(caster, foe, attack * numbers.AttackScale, hits);
                    break;
                case SkillEffect.Slow:
                    if (!boss) target.Skills.Slow.Set(fraction, Elapsed + numbers.Duration);
                    break;
                case SkillEffect.Power:
                case SkillEffect.SwordOath:
                    state.AttackUp.Set(fraction, Elapsed + numbers.Duration);
                    SkillApplied?.Invoke(SkillCue.AttackUp, caster, caster, Elapsed);
                    if (effect == SkillEffect.Power && caster.Stats.Id == "ice_knight")
                        SkillApplied?.Invoke(SkillCue.FrostSwordEnchant, caster, caster, Elapsed);
                    break;
                case SkillEffect.Pierce: Strike(caster, target, attack, hits, true); break;
                case SkillEffect.ArmorStrike: Strike(caster, target, attack + EffectiveArmor(caster) * numbers.ArmorScale, hits); break;
                case SkillEffect.DrainCurrentHp:
                    hits[Units.IndexOf(target)].HealthLoss += target.Hp * Mathf.Min(1f, fraction) * caster.RankMultiplier;
                    break;
                case SkillEffect.Sunder:
                    target.Skills.DefenseDown.Set(fraction, Elapsed + numbers.Duration);
                    SkillApplied?.Invoke(SkillCue.DefenseDown, caster, target, Elapsed);
                    break;
                case SkillEffect.CurrentHp: Strike(caster, target, target.Hp * fraction, hits, true); break;
                case SkillEffect.Weaken:
                    Strike(caster, target, attack, hits);
                    target.Skills.AttackDown.Set(fraction, Elapsed + numbers.Duration);
                    SkillApplied?.Invoke(SkillCue.AttackDown, caster, target, Elapsed);
                    break;
                case SkillEffect.TeamHaste:
                case SkillEffect.TeamArmor:
                case SkillEffect.Rally:
                case SkillEffect.Mend:
                    foreach (var ally in Units)
                    {
                        if (!ally.Ally || !ally.Alive) continue;
                        if (effect == SkillEffect.TeamHaste) ally.Skills.Haste.Set(fraction, Elapsed + numbers.Duration);
                        if (effect == SkillEffect.TeamArmor)
                        {
                            ally.Skills.DefenseUp.Set(fraction, Elapsed + numbers.Duration);
                            SkillApplied?.Invoke(SkillCue.DefenseUp, caster, ally, Elapsed);
                        }
                        if (effect == SkillEffect.Rally && ally != caster)
                        {
                            ally.Skills.AttackUp.Set(fraction, Elapsed + numbers.Duration);
                            SkillApplied?.Invoke(SkillCue.AttackUp, caster, ally, Elapsed);
                        }
                        if (effect == SkillEffect.Mend)
                        {
                            ally.Skills.HealRate = (ally.MaxHp - ally.Hp) * fraction / numbers.Duration;
                            ally.Skills.HealLeft = numbers.Duration;
                            if (ally.Skills.HealRate > 0)
                                SkillApplied?.Invoke(SkillCue.ChainHeal, caster, ally, Elapsed);
                        }
                    }
                    break;
                case SkillEffect.Fortify:
                    state.DefenseUp.Set(fraction, Elapsed + numbers.Duration);
                    SkillApplied?.Invoke(SkillCue.DefenseUp, caster, caster, Elapsed);
                    break;
                case SkillEffect.Guard: state.DamageReduction = Mathf.Clamp01(fraction); break;
                case SkillEffect.Stun:
                    Strike(caster, target, attack, hits);
                    target.Skills.StunnedUntil = Elapsed + (boss ? numbers.BossDuration : numbers.Duration);
                    SkillApplied?.Invoke(SkillCue.StunStars, caster, target, Elapsed);
                    break;
                case SkillEffect.Double:
                    Strike(caster, target, attack * numbers.AttackScale, hits);
                    if (caster.Stats.Id == "katana_knight")
                        SkillApplied?.Invoke(SkillCue.KatanaCombo, caster, target, Elapsed);
                    break;
                case SkillEffect.DoublePower:
                    state.AttackUp.Set(fraction, Elapsed + numbers.Duration);
                    SkillApplied?.Invoke(SkillCue.AttackUp, caster, caster, Elapsed);
                    break;
                case SkillEffect.Taunt:
                case SkillEffect.TimedTaunt:
                case SkillEffect.Blind:
                    foreach (var foe in Units)
                        if (!foe.Ally && foe.Alive)
                        {
                            if (effect == SkillEffect.Blind) foe.Skills.BlindUntil = Elapsed + numbers.Duration;
                            else
                            {
                                foe.Skills.Taunter = caster;
                                foe.Skills.TauntUntil = effect == SkillEffect.TimedTaunt ? Elapsed + numbers.Duration : 0f;
                                SkillApplied?.Invoke(SkillCue.Taunt, caster, foe, Elapsed);
                            }
                        }
                    break;
                case SkillEffect.Equalize:
                    float hp = 0, capacity = 0;
                    foreach (var ally in Units)
                        if (ally.Ally && ally.Alive) { hp += ally.Hp; capacity += ally.MaxHp; }
                    foreach (var ally in Units)
                        if (ally.Ally && ally.Alive) ally.Hp = ally.MaxHp * Mathf.Min(1f, hp * (1f + fraction) / capacity);
                    break;
                case SkillEffect.MissingHp: Strike(caster, target, (target.MaxHp - target.Hp) * fraction, hits, true); break;
                case SkillEffect.Frenzy: state.Haste.Set(fraction, Elapsed + numbers.Duration); break;
                case SkillEffect.Flurry:
                    Strike(caster, target, attack * numbers.AttackScale, hits);
                    break;
            }
        }
    }
}
