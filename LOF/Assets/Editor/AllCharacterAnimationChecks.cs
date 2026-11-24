using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AFKTrial.Editor
{
    public static class AllCharacterAnimationChecks
    {
        [MenuItem("AFK Trial/Check All Character Animations")]
        public static void Run()
        {
            try
            {
                TrialChecks.Run();
                var data = TrialData.Load();
                var report = new System.Text.StringBuilder();
                foreach (var hero in data.Heroes) {
                Require(!string.IsNullOrEmpty(hero.Animation), hero.Id + " animation mapping");
                string resource = data.RigResources[hero.Animation];
                var rig = new TrialCharacterRig(resource);
                Require(Mathf.Approximately(rig.ImpactTime, hero.ImpactSeconds), hero.Id + " CSV/rig impact marker");
                var reader = new System.Runtime.Serialization.Json.DataContractJsonSerializer(
                    typeof(TrialCharacterRig.Data), new System.Runtime.Serialization.Json.DataContractJsonSerializerSettings { UseSimpleDictionaryFormat = true });
                TrialCharacterRig.Data animation;
                using (var stream = new MemoryStream(Resources.Load<TextAsset>(resource).bytes))
                    animation = (TrialCharacterRig.Data)reader.ReadObject(stream);
                var keys = animation.animations["attack"].slots["pose"].attachment;
                Require(keys[4].name == "attack5" && Mathf.Approximately(keys[4].time, rig.ImpactTime), "crossed axe and hammer strike pose at impact");
                Require(Resources.Load<Texture2D>(data.HitTexture) != null, "Hit Flash texture loads");
                float configuredImpact = hero.ImpactSeconds;
                foreach (float impact in new[] { configuredImpact, .3f, 1.5f })
                {
                    hero.ImpactSeconds = impact;
                    var battle = new TrialBattle(data);
                    int index = data.Heroes.IndexOf(hero);
                    battle.ApplyFormation(new[] { index, (index + 1) % data.Heroes.Count, (index + 2) % data.Heroes.Count });
                    battle.StartBattle();
                    var attacker = battle.Units.Find(u => u.Stats == hero);
                    var target = battle.Units[3];
                    foreach (var unit in battle.Units) unit.Cooldown = 100;
                    attacker.Cooldown = 0;
                    attacker.Position = target.Position - Vector2.right * .1f;
                    attacker.Target = target;
                    float hp = target.Hp;
                    battle.Tick(.005f);
                    Require(attacker.WindingUp && target.Hp == hp, "windup has no early damage");
                    while (attacker.Attacks == 0 && battle.Elapsed < 3)
                    {
                        float expectedAge = (battle.Elapsed + .005f - attacker.SwingStart) * attacker.SwingSpeed;
                        battle.Tick(.005f);
                        if (expectedAge + .00001f < impact) Require(target.Hp == hp, "no damage before crossed axe and hammer strike");
                    }
                    float age = (battle.Elapsed - attacker.SwingStart) * attacker.SwingSpeed;
                    Require(attacker.Attacks == 1 && Mathf.Abs(age - impact) < .008f, "damage at configured impact");
                    Require(Mathf.Approximately(hp - target.Hp, attacker.Attack), "one attack damage");
                    Require(target.LastHitTime == battle.Elapsed && target.LastDamage > 0, "flash starts on damage tick");
                    float poseTime = age * rig.ImpactTime / impact + .00001f;
                    string pose = null;
                    foreach (var key in keys) if (key.time <= poseTime) pose = key.name;
                    Require(pose == "attack5", "damage and flash coincide with crossed axe and hammer strike pose");
                }
                hero.ImpactSeconds = configuredImpact;
                report.AppendLine(hero.Id + ": PASS; impact=" + configuredImpact + "; resources, damage, flash, attack5");
                }
                Require(data.Heroes.Count == 10, "ten heroes checked");
                Directory.CreateDirectory("Logs");
                File.WriteAllText("Logs/AllCharacterAnimationChecks.txt", "PASS: assets, crossed axe and hammer strike pose, damage and Hit Flash timestamp; default, adjusted and accelerated timing.\n" + DateTime.UtcNow.ToString("O") + "\n" + report.ToString());
                Debug.Log("[AFK Trial] PASS: All 10 character animation checks");
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                throw;
            }
        }

        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}







