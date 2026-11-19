using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AFKTrial.Editor
{
    public static class BanditChecks
    {
        [MenuItem("AFK Trial/Check Bandit Enemies")]
        public static void Run()
        {
            var data = TrialData.Load();
            var bandit = data.Enemies["normal"];
            Require(bandit.Animation == "bandit", "normal enemy uses the bandit animation");
            var captain = data.Enemies["boss"];
            var chief = data.Enemies["boss2"];
            Require(captain.Animation == "bandit_captain" && chief.Animation == "bandit_chief",
                "captain and chief have their own animations");
            var dagger = CheckClips(data, bandit, .35f, "attack3");
            var flail = CheckClips(data, captain, .50f, "attack4");
            var sword = CheckClips(data, chief, .52f, "attack4");
            foreach (float speed in new[] { 1f, 1.6f })
            {
                var ordinary = new TrialBattle(data);
                var enemies = ordinary.Units.Where(u => !u.Ally).ToArray();
                Require(enemies.Length == data.StartingFormation.Length && enemies.All(u => u.Stats == bandit),
                    "ordinary stage spawns bandits in every enemy lane");
                CheckStrike(ordinary, enemies[0], dagger, speed);
                var bossStage = ReachStage(data, data.BossEvery);
                CheckBossSlot(bossStage, captain);
                CheckStrike(bossStage, bossStage.Units.Single(u => u.Stats == captain), flail, speed);
                var finalStage = ReachStage(data, data.RoundLength);
                CheckBossSlot(finalStage, chief);
                CheckStrike(finalStage, finalStage.Units.Single(u => u.Stats == chief), sword, speed);
            }
            CheckRoundProgression(data);
            Debug.Log("[AFK Trial] PASS: bandit, captain and chief spawn, weapon contact, recovery and boss rotation");
        }

        private static TrialCharacterRig.Key[] CheckClips(TrialData data, TrialData.Fighter enemy,
            float impact, string contactPose)
        {
            string resource = data.RigResources[enemy.Animation];
            var rig = new TrialCharacterRig(resource);
            Require(Mathf.Approximately(rig.ImpactTime, impact) &&
                Mathf.Approximately(rig.ImpactTime, enemy.ImpactSeconds), enemy.Name + " impact marker and combat timing agree");

            var graph = TrialCharacterRig.ReadData(resource);
            var idle = graph.animations["idle"].slots["pose"].attachment;
            var walk = graph.animations["walk"].slots["pose"].attachment;
            var attack = graph.animations["attack"].slots["pose"].attachment;
            foreach (var loop in new[] { idle, walk })
                Require(loop[0].name == loop.Last().name && loop.Select(k => k.name).Distinct().Count() > 1,
                    "idle and walk loop back to their opening poses");
            Require(attack[0].name == idle[0].name && attack.Last().name == idle[0].name,
                "strike begins and ends in guard");
            Require(Mathf.Approximately(attack.First(k => k.name == contactPose).time, rig.ImpactTime),
                enemy.Name + " first reaches contact on the impact marker");
            var trail = graph.slots.Single(slot => slot.name == "attackTrail");
            var keys = graph.animations["attack"].slots[trail.name].attachment;
            Require(trail.attachment == null && !graph.animations["idle"].slots.ContainsKey(trail.name) &&
                !graph.animations["walk"].slots.ContainsKey(trail.name), enemy.Name + " hides its trail outside a strike");
            Require(keys.Any(k => Mathf.Approximately(k.time, impact) && k.name != null &&
                graph.skins[0].attachments[trail.name].ContainsKey(k.name)), enemy.Name + " has a trail on its contact pose");
            Require(keys.Last().name == null && keys.Last().time < attack.Last().time,
                enemy.Name + " clears the trail before returning to guard");
            return attack;
        }

        private static void CheckStrike(TrialBattle battle, TrialBattle.Unit attacker, TrialCharacterRig.Key[] attack, float speed)
        {
            var target = battle.Units.First(u => u.Ally && u.Position.y == attacker.Position.y);
            foreach (var unit in battle.Units)
                if (unit != attacker && unit != target) unit.Hp = 0;
            target.Cooldown = 100;
            attacker.SwingSpeed = speed;
            float initialHp = target.Hp;
            float spawnX = attacker.Position.x;
            battle.Tick(.01f);
            Require(attacker.Walking && attacker.Position.x < spawnX && attacker.Attacks == 0,
                "bandit approaches the party from the right");
            while (!attacker.WindingUp && !battle.Finished) battle.Tick(.01f);
            Require(attacker.WindingUp && !attacker.Walking && attacker.Target == target,
                "bandit plants its feet to attack in range");

            const float margin = .002f;
            battle.Tick(attacker.Stats.ImpactSeconds / speed - margin);
            Require(target.Hp == initialHp && attacker.Attacks == 0, "no damage before full extension");
            battle.Tick(margin);
            Require(attacker.Attacks == 1 && target.LastHitTime == battle.Elapsed && target.LastDamage > 0 &&
                Mathf.Approximately(initialHp - target.Hp, target.LastDamage), "one hit when the weapon reaches contact");
            Require(attacker.Stats.HitHeight > 0 && Mathf.Approximately(target.LastHitHeight, attacker.Stats.HitHeight),
                "the victim's hit slash uses the attacking weapon's contact height");
            float contactHp = target.Hp;
            float recovery = attack.Last().time - attacker.Stats.ImpactSeconds;
            battle.Tick(recovery / speed + margin);
            Require(attacker.Attacks == 1 && target.Hp == contactHp && !attacker.Walking && !attacker.WindingUp,
                "bandit recovers in range without another hit");
            target.Position.x = attacker.Position.x - attacker.Stats.Range - 1;
            battle.Tick(.01f);
            Require(attacker.Walking && attacker.Attacks == 1, "bandit resumes pursuit outside weapon range");

            target.Position.x = attacker.Position.x - attacker.Stats.Range * .5f;
            while (!attacker.WindingUp && !battle.Finished) battle.Tick(.01f);
            Require(attacker.WindingUp, "next attack winds up on reacquiring range");
            target.Position.x = attacker.Position.x - attacker.Stats.Range - 1;
            battle.Tick(.01f);
            Require(attacker.Walking && !attacker.WindingUp && float.IsNegativeInfinity(attacker.SwingStart) &&
                target.Hp == contactHp, "leaving range cancels the pending strike and its pose");
            if (attacker.Stats.ContactDistance > 0)
                CheckBossRetarget(battle, attacker, target);
        }

        private static void CheckBossRetarget(TrialBattle battle, TrialBattle.Unit boss, TrialBattle.Unit previous)
        {
            previous.Hp = 0;
            var target = battle.Units.First(u => u.Ally && u != previous);
            target.Hp = target.MaxHp;
            target.Cooldown = 100;
            target.Position.x = boss.Position.x + .1f;
            Vector2 start = boss.Position;
            int strikes = boss.Attacks;
            battle.Tick(.01f);
            Require(boss.Target == target && boss.Walking && boss.Position.x > start.x &&
                boss.Position.y != start.y && !boss.WindingUp, "boss steps back and changes lane for a close target");
            while (!boss.WindingUp && !battle.Finished) battle.Tick(.01f);
            var aim = target.Position + Vector2.right * boss.Stats.ContactDistance;
            Require(boss.WindingUp && Vector2.Distance(boss.Position, aim) <= .01f &&
                boss.Attacks == strikes && target.Hp == target.MaxHp, "boss repositions before winding up");
            var survivor = battle.Units.First(u => u.Ally && u != target && u != previous);
            survivor.Hp = survivor.MaxHp;
            survivor.Cooldown = 100;
            survivor.Position.x = boss.Position.x + .1f;
            target.Hp = 1;
            battle.Tick(boss.Stats.ImpactSeconds / boss.SwingSpeed);
            Require(!target.Alive && boss.Attacks == strikes + 1 && target.LastHitTime == battle.Elapsed,
                "the weapon defeats the new target on impact");
            var planted = boss.Position;
            float swingStart = boss.SwingStart;
            const float margin = .002f;
            float returnTime = (boss.Stats.AttackDuration - boss.Stats.ImpactSeconds) / boss.SwingSpeed;
            battle.Tick(returnTime - margin);
            Require(boss.Target == survivor && !boss.Walking && boss.Position == planted &&
                boss.SwingStart == swingStart && survivor.Hp == survivor.MaxHp, "the fatal hit plays through recovery");
            battle.Tick(margin * 2);
            Require(boss.Walking && boss.Position != planted && float.IsNegativeInfinity(boss.SwingStart) &&
                boss.Attacks == strikes + 1, "the boss walks toward the survivor after recovery");
        }

        private static void Win(TrialBattle battle)
        {
            foreach (var unit in battle.Units)
                if (!unit.Ally) unit.Hp = 0;
            battle.Tick(.01f);
        }

        private static TrialBattle ReachStage(TrialData data, int stage)
        {
            var battle = new TrialBattle(data);
            for (int i = 1; i < stage; i++)
            {
                Win(battle);
                battle.StartBattle();
            }
            Require(battle.ActiveStage == stage, "progression reaches the requested stage");
            return battle;
        }

        private static void CheckBossSlot(TrialBattle battle, TrialData.Fighter boss)
        {
            Require(battle.BossStage, "progression reaches the boss stage");
            var enemies = battle.Units.Where(u => !u.Ally).ToArray();
            for (int i = 0; i < enemies.Length; i++)
                Require(enemies[i].Stats == (i == battle.Data.BossSlot ? boss : battle.Data.Enemies["normal"]),
                    "only the designated boss lane contains the boss");
        }

        private static void CheckRoundProgression(TrialData data)
        {
            var battle = ReachStage(data, data.RoundLength + data.BossEvery);
            CheckBossSlot(battle, data.Enemies["boss"]);
            while (battle.ActiveStage < 2 * data.RoundLength)
            {
                Win(battle);
                battle.StartBattle();
            }
            var chief = data.Enemies["boss2"];
            CheckBossSlot(battle, chief);
            battle.ForceTimeout();
            battle.Retry();
            CheckBossSlot(battle, chief);
            Win(battle);
            Require(battle.TicketReward == data.FirstBossTickets, "the chief grants first-clear boss tickets");
            battle.StartBattle();
            battle.ForceTimeout();
            battle.StartBattle();
            Require(battle.ActiveFarming, "losing the next challenge returns to the cleared chief stage");
            CheckBossSlot(battle, chief);
            Win(battle);
            Require(battle.TicketReward == 0, "farming the chief does not repeat the first-clear reward");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Bandit check failed: " + message);
        }
    }
}
