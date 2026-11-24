using UnityEngine;

namespace AFKTrial
{
    public sealed partial class TrialBattle
    {
        // Shared one-second, eight-pose skill timeline. Effects occur at contact, not at windup.
        private bool TickAnimatedSkill(Unit caster, PendingHit[] hits)
        {
            float age = Elapsed - caster.SkillStart;
            if (age >= 1f) return false;
            if (caster.Skills.StunnedUntil > Elapsed) { caster.SkillStart = float.NegativeInfinity; return false; }
            var effect = caster.AnimatedSkill;
            int count = effect == SkillEffect.Double ? 2 : effect == SkillEffect.Flurry ? 3 : 1;
            float first = count == 2 ? .25f : count == 3 ? .375f : .5f;
            float spacing = count == 2 ? .25f : .125f;
            bool weaponMotion = effect == SkillEffect.Wave || effect == SkillEffect.Pierce || effect == SkillEffect.Weaken ||
                effect == SkillEffect.Stun || effect == SkillEffect.Double || effect == SkillEffect.Flurry ||
                effect == SkillEffect.ArmorStrike || effect == SkillEffect.MissingHp || effect == SkillEffect.DrainCurrentHp;
            if (weaponMotion)
                while (caster.SkillSwing < count && age >= first + caster.SkillSwing * spacing - .12f)
                {
                    caster.SkillSwing++;
                    WeaponSoundRequested?.Invoke(caster, false, true);
                }
            while (caster.SkillHit < count && age + .00001f >= first + caster.SkillHit * spacing)
            {
                var previousTarget = caster.Target;
                var target = caster.SkillTarget;
                if (target == null || !target.Alive) { caster.SkillHit = count; break; }
                caster.Target = target;
                caster.SkillHit++;
                if (count > 1)
                {
                    var numbers = NumbersFor(effect, caster.SkillRank, caster.Stats.Id);
                    float amount = AttackPower(caster) * numbers.AttackScale / count;
                    Strike(caster, target, amount, hits);
                    if (effect == SkillEffect.Double && caster.SkillHit == 1)
                        SkillApplied?.Invoke(SkillCue.KatanaCombo, caster, target, Elapsed);
                    hits[Units.IndexOf(target)].ComboCaster = caster;
                    hits[Units.IndexOf(target)].ComboRaw += amount * caster.RankMultiplier;
                    hits[Units.IndexOf(target)].ComboStart = caster.SkillStart;
                    hits[Units.IndexOf(target)].ComboFinal = caster.SkillHit == count;
                }
                else Cast(caster, effect, caster.SkillRank, hits);
                caster.Target = previousTarget;
            }
            return true;
        }
    }
}
