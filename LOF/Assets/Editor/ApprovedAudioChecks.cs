using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AFKTrial.Editor
{
    [InitializeOnLoad]
    public static class ApprovedAudioChecks
    {
        static ApprovedAudioChecks() { EditorApplication.delayCall += AutoRun; }
        static void AutoRun()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) { EditorApplication.delayCall += AutoRun; return; }
            if (!File.Exists("Logs/ApprovedAudioChecks.request")) return;
            File.Delete("Logs/ApprovedAudioChecks.request");
            Run();
        }
        [MenuItem("AFK Trial/Check Approved Audio")]
        public static void Run()
        {
            try
            {
                var bgm = Resources.Load<AudioClip>("Audio/Background");
                Require(bgm != null && bgm.length > 10, "Hope loads");
                foreach (string key in new[] { "Sword", "Katana", "Spear", "Mace", "Hammer", "Shield", "Dagger", "Axe", "SwingLight", "SwingHeavy" })
                {
                    var clip = Resources.Load<AudioClip>("Audio/Weapons/" + key);
                    Require(clip != null && clip.length > .03f && clip.length < 1f, key + " clip duration");
                }
                var data = TrialData.Load();
                foreach (var hero in data.Heroes)
                {
                    Require(hero.AttackDuration > hero.ImpactSeconds, hero.Id + " attack includes recovery frames");
                    var battle = new TrialBattle(data);
                        var progress = battle.CaptureProgress();
                        progress.heroes = data.Heroes.ConvertAll(h => new TrialBattle.HeroProgress { id = h.Id, level = 1, skills = new[] { 1, 1, 1 } }).ToArray();
                        Require(battle.RestoreProgress(progress), "isolated test roster");
                    int index = data.Heroes.IndexOf(hero);
                    battle.ApplyFormation(new[] { index, (index + 1) % 10, (index + 2) % 10 });
                    battle.StartBattle();
                    var attacker = battle.Units.Find(u => u.Stats == hero);
                    var target = battle.Units[3];
                    foreach (var unit in battle.Units) unit.Cooldown = 100;
                    attacker.Target = target;
                    attacker.Position = target.Position - Vector2.right * .1f;
                    attacker.Cooldown = 0;
                    float swing = -1, impact = -1;
                    battle.WeaponSoundRequested += (unit, hit, special) => {
                        if (unit != attacker) return;
                        if (hit) impact = battle.Elapsed; else swing = battle.Elapsed;
                    };
                    while (impact < 0 && battle.Elapsed < 3) battle.Tick(.005f);
                    Require(swing >= 0 && impact > swing, hero.Id + " swing before impact");
                    Require(target.LastHitTime == impact && target.LastDamage > 0, hero.Id + " audio / damage / flash same tick");
                    // Regression: zero cooldown and due skills cannot replace an active action.
                    foreach (var unit in battle.Units) { unit.Hp = unit.MaxHp = 100000; unit.Cooldown = 100; }
                    bool sawSkill = false;
                    for (int tick = 0; tick < 2400; tick++)
                    {
                        float before = battle.Elapsed, swingStart = attacker.SwingStart, skillStart = attacker.SkillStart;
                        float swingEnd = swingStart + hero.AttackDuration / attacker.SwingSpeed;
                        bool recovering = !attacker.WindingUp && before < swingEnd;
                        bool casting = before - skillStart < 1f;
                        attacker.Cooldown = 0;
                        battle.Tick(.005f);
                        if (recovering && battle.Elapsed < swingEnd)
                        {
                            Require(attacker.SwingStart == swingStart, hero.Id + " recovery not overwritten by basic attack");
                            Require(attacker.SkillStart == skillStart, hero.Id + " recovery not overwritten by skill");
                        }
                        if (casting && battle.Elapsed - skillStart < 1f)
                        {
                            Require(attacker.SkillStart == skillStart && attacker.SwingStart == swingStart, hero.Id + " skill not overwritten");
                        }
                        if (attacker.SkillStart != skillStart)
                        {
                            sawSkill = true;
                            Require(battle.Elapsed >= swingEnd, hero.Id + " skill starts after attack ends");
                        }
                    }
                    Require(sawSkill, hero.Id + " ready skill is not starved by basic attacks");
                }
                Directory.CreateDirectory("Logs");
                File.WriteAllText("Logs/ApprovedAudioChecks.txt", "PASS " + DateTime.UtcNow.ToString("O") + "\nHope + 10 SFX load; all 10 heroes swing before impact; damage and flash share impact timestamp.\n");
            }
            catch (Exception error) { File.WriteAllText("Logs/ApprovedAudioChecks.txt", "FAIL\n" + error); Debug.LogException(error); }
        }
        static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
