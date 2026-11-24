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
                case SkillEffect.Guard: case SkillEffect.ReduceCurrentAttack: value.Fraction = Mathf.Clamp01(value.Fraction); break;
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
            void Duration() => Row("지속 시간", N(number.Duration) + "초");
            void Damage() => Row("공격력 비례 피해", N(number.AttackScale * 100) + "% 공격력");
            switch (effect)
            {
                case SkillEffect.Wave: Damage(); Row("대상", "모든 적"); break;
                case SkillEffect.Slow: Percent("공격 속도 감소"); Duration(); Row("보스", "면역"); break;
                case SkillEffect.Power: case SkillEffect.SwordOath: case SkillEffect.Rally: case SkillEffect.DoublePower:
                    Percent("공격력 증가"); Duration(); break;
                case SkillEffect.Pierce: Damage(); Row("적 방어력", "무시"); break;
                case SkillEffect.ArmorStrike: Damage(); Row("방어력 추가 피해", N(number.ArmorScale) + " × 방어력"); break;
                case SkillEffect.ReduceCurrentAttack: Percent("적 현재 공격력 감소"); Row("지속 시간", "전투 종료까지"); break;
                case SkillEffect.CurrentHp: Percent("적 현재 체력 비례 피해"); Row("적 방어력", "무시"); break;
                case SkillEffect.Sunder: Percent("방어력 감소"); Duration(); break;
                case SkillEffect.Weaken: Damage(); Percent("적 공격력 감소"); Duration(); break;
                case SkillEffect.TeamHaste: Percent("공격 속도 증가"); Duration(); break;
                case SkillEffect.TeamArmor: case SkillEffect.Fortify: Percent("방어력 증가"); Duration(); break;
                case SkillEffect.Mend: Percent("잃은 체력 회복"); Row("회복 시간", N(number.Duration) + "초"); break;
                case SkillEffect.Guard: Percent("최종 피해 감소"); Row("지속 시간", "전투 종료까지"); break;
                case SkillEffect.Stun: Damage(); Row("일반 적 기절", N(number.Duration) + "초"); Row("보스 기절", N(number.BossDuration) + "초"); break;
                case SkillEffect.Double: Damage(); break;
                case SkillEffect.Taunt: Row("적별 도발", "다음 기본 공격 1회"); break;
                case SkillEffect.TimedTaunt: Row("도발 시간", N(number.Duration) + "초"); break;
                case SkillEffect.Blind: Row("실명 시간", N(number.Duration) + "초"); break;
                case SkillEffect.Equalize: Percent("아군 현재 체력 비례 회복"); Row("회복 후", "체력 비율 균등화"); break;
                case SkillEffect.MissingHp: Percent("적 잃은 체력 비례 피해"); Row("적 방어력", "무시"); break;
                case SkillEffect.Frenzy: Row("공격 속도", N(1 + number.Fraction) + "배"); Duration(); break;
                case SkillEffect.Flurry: Row("공격 횟수 환산", number.Hits.ToString(CultureInfo.InvariantCulture)); Row("총 피해", N(number.AttackScale * 100) + "% 공격력"); break;
                default: Row("효과", "없음"); break;
            }
            return rows;
        }
    }
}
