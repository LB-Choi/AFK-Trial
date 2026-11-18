using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AFKTrial.Editor
{
    public static class RoyalKnightChecks
    {
        [MenuItem("AFK Trial/Check Royal Knight Impact")]
        public static void Run()
        {
            try
            {
                TrialChecks.Run();
                var data = TrialData.Load();
                var hero = data.Heroes.Find(h => h.Id == "royal_knight");
                Require(hero.Animation == "royal_knight", "hero animation mapping");
                string resource = data.RigResources[hero.Animation];
                var rig = new TrialCharacterRig(resource);
                Require(Mathf.Approximately(rig.ImpactTime, .45f), "rig impact marker");
                var reader = new System.Runtime.Serialization.Json.DataContractJsonSerializer(
                    typeof(TrialCharacterRig.Data), new System.Runtime.Serialization.Json.DataContractJsonSerializerSettings { UseSimpleDictionaryFormat = true });
                TrialCharacterRig.Data animation;
                using (var stream = new MemoryStream(Resources.Load<TextAsset>(resource).bytes))
                    animation = (TrialCharacterRig.Data)reader.ReadObject(stream);
                var keys = animation.animations["attack"].slots["pose"].attachment;
                Require(keys[4].name == "attack5" && Mathf.Approximately(keys[4].time, rig.ImpactTime), "greatsword slash pose at impact");
                Require(Resources.Load<Texture2D>(data.HitTexture) != null, "Hit Flash texture loads");
                foreach (float impact in new[] { .45f, .3f, 1.5f })
                {
                    hero.ImpactSeconds = impact;
                    var battle = new TrialBattle(data);
                    battle.ApplyFormation(new[] { data.Heroes.IndexOf(hero), 0, 1 });
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
                        if (expectedAge + .00001f < impact) Require(target.Hp == hp, "no damage before greatsword slash");
                    }
                    float age = (battle.Elapsed - attacker.SwingStart) * attacker.SwingSpeed;
                    Require(attacker.Attacks == 1 && Mathf.Abs(age - impact) < .008f, "damage at configured impact");
                    Require(Mathf.Approximately(hp - target.Hp, attacker.Attack), "one attack damage");
                    Require(target.LastHitTime == battle.Elapsed && target.LastDamage > 0, "flash starts on damage tick");
                    float poseTime = age * rig.ImpactTime / impact + .00001f;
                    string pose = null;
                    foreach (var key in keys) if (key.time <= poseTime) pose = key.name;
                    Require(pose == "attack5", "damage and flash coincide with greatsword slash pose");
                }
                Directory.CreateDirectory("Logs");
                File.WriteAllText("Logs/RoyalKnightChecks.txt", "PASS: assets, greatsword slash pose, damage and Hit Flash timestamp; default, adjusted and accelerated timing.\n" + DateTime.UtcNow.ToString("O"));
                Debug.Log("[AFK Trial] PASS: Royal Knight impact checks");
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



