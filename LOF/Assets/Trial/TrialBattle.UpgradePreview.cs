using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace AFKTrial
{
    public sealed partial class TrialBattle
    {
        // Shared by combat and the upgrade preview; values are before encounter modifiers.
        internal struct SkillNumbers
        {
            internal float Fraction, Duration, BossDuration, AttackScale, ArmorScale;
            internal long Hits;
        }

        internal static SkillNumbers NumbersFor(SkillEffect effect, int rank, string heroId)
        {
            var value = new SkillNumbers { Fraction = rank * .01f, AttackScale = 1, ArmorScale = rank, Hits = 2L * rank };
            switch (effect)
            {
                case SkillEffect.Wave: value.AttackScale = .5f * rank; break;
                case SkillEffect.Double: value.AttackScale = rank; break;
                case SkillEffect.Flurry: value.AttackScale = value.Hits; break;
                case SkillEffect.Slow: case SkillEffect.TeamHaste: case SkillEffect.Mend: value.Duration = 4; break;
                case SkillEffect.Power: case SkillEffect.Rally: value.Duration = 6; break;
                case SkillEffect.SwordOath: value.Duration = 7; break;
                case SkillEffect.Sunder: case SkillEffect.Weaken: value.Duration = 3; break;
                case SkillEffect.TeamArmor: value.Duration = 5; break;
                case SkillEffect.Fortify: value.Duration = heroId == "tower_knight" ? 4 : 5; break;
                case SkillEffect.Guard: case SkillEffect.DrainCurrentHp: value.Fraction = Mathf.Clamp01(value.Fraction); break;
                case SkillEffect.Stun: value.Duration = .2f * rank; value.BossDuration = .1f * rank; break;
                case SkillEffect.DoublePower: value.Fraction = 1; value.Duration = rank; break;
                case SkillEffect.Blind: value.Duration = .1f * rank; break;
                case SkillEffect.TimedTaunt: value.Duration = .2f * rank; break;
                case SkillEffect.Equalize: value.Fraction = .1f * rank; break;
                case SkillEffect.Frenzy: value.Fraction = rank - 1; value.Duration = 5; break;
            }
            return value;
        }

        public List<(string label, string value)> SkillPreview(int hero, int slot, int rank)
        {
            if (rank < 1) throw new ArgumentOutOfRangeException(nameof(rank));
            var effect = Data.Skills[hero, slot].Effect;
            var number = NumbersFor(effect, rank, Data.Heroes[hero].Id);
            var rows = new List<(string label, string value)>();
            string N(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);
            void Row(string label, string value) => rows.Add((label, value));
            void Percent(string label) => Row(label, N(number.Fraction * 100) + "%");
            void Duration() => Row("Duration", N(number.Duration) + "s");
            void Damage() => Row("ATK damage", N(number.AttackScale * 100) + "% ATK");
            switch (effect)
            {
                case SkillEffect.Wave: Damage(); Row("Targets", "All enemies"); break;
                case SkillEffect.Slow: Percent("Attack speed down"); Duration(); Row("Bosses", "Immune"); break;
                case SkillEffect.Power: case SkillEffect.SwordOath: case SkillEffect.Rally: case SkillEffect.DoublePower:
                    Percent("ATK increase"); Duration(); break;
                case SkillEffect.Pierce: Damage(); Row("Enemy DEF", "Ignored"); break;
                case SkillEffect.ArmorStrike: Damage(); Row("Extra DEF damage", N(number.ArmorScale) + " x own DEF"); break;
                case SkillEffect.DrainCurrentHp: Percent("Current enemy HP loss"); break;
                case SkillEffect.CurrentHp: Percent("Current enemy HP damage"); Row("Enemy DEF", "Ignored"); break;
                case SkillEffect.Sunder: Percent("DEF decrease"); Duration(); break;
                case SkillEffect.Weaken: Damage(); Percent("Enemy ATK decrease"); Duration(); break;
                case SkillEffect.TeamHaste: Percent("Attack speed increase"); Duration(); break;
                case SkillEffect.TeamArmor: case SkillEffect.Fortify: Percent("DEF increase"); Duration(); break;
                case SkillEffect.Mend: Percent("Missing HP healed"); Row("Heal over", N(number.Duration) + "s"); break;
                case SkillEffect.Guard: Percent("Final damage reduction"); Row("Duration", "Until battle ends"); break;
                case SkillEffect.Stun: Damage(); Row("Normal enemy stun", N(number.Duration) + "s"); Row("Boss stun", N(number.BossDuration) + "s"); break;
                case SkillEffect.Double: Damage(); break;
                case SkillEffect.Taunt: Row("Taunt per enemy", "Next 1 basic attack"); break;
                case SkillEffect.TimedTaunt: Row("Taunt duration", N(number.Duration) + "s"); break;
                case SkillEffect.Blind: Row("Blind duration", N(number.Duration) + "s"); break;
                case SkillEffect.Equalize: Percent("Ally current HP healed"); Row("After healing", "Equalize HP ratios"); break;
                case SkillEffect.MissingHp: Percent("Missing enemy HP damage"); Row("Enemy DEF", "Ignored"); break;
                case SkillEffect.Frenzy: Row("Attack speed", N(1 + number.Fraction) + " x normal"); Duration(); break;
                case SkillEffect.Flurry: Row("Equivalent hits", number.Hits.ToString(CultureInfo.InvariantCulture)); Row("Total damage", N(number.AttackScale * 100) + "% ATK"); break;
                default: Row("Effect", "None"); break;
            }
            return rows;
        }
    }
}
