using System;
using UnityEngine;

namespace AFKTrial.Editor
{
    internal static class TrialBossWarningChecks
    {
        public static void Run() { Check(TrialData.Load()); Debug.Log("Boss warning checks PASS"); }
        static void Stun(TrialBattle.Unit unit, float until)
        {
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var state = typeof(TrialBattle.Unit).GetField("Skills", flags).GetValue(unit);
            state.GetType().GetField("StunnedUntil", flags).SetValue(state, until);
        }
        static void Require(bool ok, string message)
        { if (!ok) throw new InvalidOperationException("Boss warning: " + message); }
        public static void Check(TrialData data)
        {
            foreach (int stage in new[] { data.BossEvery, data.RoundLength })
            {
                var battle = new TrialBattle(data);
                var saved = battle.CaptureProgress(); saved.highest = stage - 1; saved.stage = stage;
                Require(battle.RestoreProgress(saved), "boss fixture");
                var boss = battle.ActiveBoss;
                var target = battle.Units[0];
                foreach (var unit in battle.Units)
                {
                    unit.Hp = unit.MaxHp = unit == boss || unit == target ? 100000 : 0;
                    if (unit.Ally) Stun(unit, 10000);
                }
                int specials = 0;
                int warningsHeard = 0, specialsHeard = 0;
                battle.SoundRequested += cue => {
                    if (cue == TrialBattle.SoundCue.BossWarning) warningsHeard++;
                    if (cue == TrialBattle.SoundCue.BossSpecial) specialsHeard++;
                };
                battle.SkillApplied += (cue, caster, victim, time) => { if (caster == boss) specials++; };
                for (int cycle = 1; cycle <= 2; cycle++)
                {
                    for (int i = 0; i < 2500 && !boss.SpecialAttackPending; i++) battle.Tick(.01f);
                    Require(boss.SpecialAttackPending && !boss.WindingUp, "warning replaces swing stage=" + stage + " cycle=" + cycle + " attacks=" + boss.Attacks + " time=" + battle.Elapsed + " finished=" + battle.Finished + " position=" + boss.Position + " target=" + target.Position);
                    Require(boss.Attacks == cycle * boss.Stats.SkillEvery - 1 && specials == cycle - 1, "warning not an attack or skill");
                    Require(warningsHeard == cycle && specialsHeard == cycle - 1, "warning sound once before special");
                    float hp = target.Hp, remaining = boss.Cooldown, elapsed = battle.Elapsed;
                    battle.Tick(0);
                    Require(boss.SpecialAttackPending && boss.Cooldown == remaining && battle.Elapsed == elapsed, "pause preserves warning");
                    Stun(boss, battle.Elapsed + .5f);
                    for (int i = 0; i < 20; i++) battle.Tick(.01f);
                    Require(boss.SpecialAttackPending && boss.Cooldown == remaining, "stun defers special");
                    Stun(boss, 0);
                    for (int i = 0; i < 40; i++) battle.Tick(.01f);
                    // Existing bleed can still tick; the first warning has no prior status effects.
                    if (cycle == 1) Require(target.Hp == hp, "warning deals no damage");
                    Require(boss.SpecialAttackPending && specials == cycle - 1, "full attack interval before special");
                    for (int i = 0; i < 400 && boss.SpecialAttackPending; i++) battle.Tick(.01f);
                    Require(!boss.SpecialAttackPending && boss.Attacks == cycle * boss.Stats.SkillEvery && specials == cycle, "next attack casts special once");
                    Require(target.Hp < hp, "special strike lands");
                    Require(specialsHeard == cycle, "special sound once on strike");
                    Stun(target, 10000);
                }
                for (int i = 0; i < 2500 && !boss.SpecialAttackPending; i++) battle.Tick(.01f);
                battle.ForceTimeout();
                Require(!boss.SpecialAttackPending, "end cancels warning");
                battle.StartBattle();
                foreach (var unit in battle.Units) Require(!unit.SpecialAttackPending && unit.Attacks == 0, "restart resets warning and count");
            }
        }
    }
}
