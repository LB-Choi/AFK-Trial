using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AFKTrial.Editor
{
    [InitializeOnLoad]
    public static class SemiSDChecks
    {
        static SemiSDChecks() { EditorApplication.update += Poll; }
        static void Poll()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlaying) return;
            if (!File.Exists("Logs/SemiSDChecks.request")) return;
            File.Delete("Logs/SemiSDChecks.request");
            Run();
        }
        [MenuItem("AFK Trial/Check Semi SD Integration")]
        public static void Run()
        {
            try
            {
                var data = TrialData.Load();
                int count = 0;
                foreach (string id in data.RigResources.Keys)
                {
                    foreach (string clip in new[] { "walk", "attack", "hit", "death" }) { CheckAtlas(id, clip); count++; }
                    if (id == "bandit_captain" || id == "bandit_chief")
                    { CheckAtlas(id, "idle"); CheckAtlas(id, "special"); count += 2; }
                }
                foreach (var hero in data.Heroes)
                    for (int slot = 0; slot < 3; slot++) { CheckAtlas(hero.Id, "skill" + slot); count++; }
                CheckAtlas("scythe_knight", "bell"); count++;
                Require(count == 87, "87 approved animation/VFX atlases");
                Require(TrialRunner.OneShotFrame(10f, 1f) == 7, "death final pose holds");
                foreach (var hero in data.Heroes)
                {
                    for (int slot = 0; slot < 3; slot++)
                    {
                        var battle = new TrialBattle(data);
                        var progress = battle.CaptureProgress();
                        progress.heroes = data.Heroes.ConvertAll(h => new TrialBattle.HeroProgress { id = h.Id, level = 1, skills = new[] { 1, 1, 1 } }).ToArray();
                        Require(battle.RestoreProgress(progress), "isolated test roster");
                        int index = data.Heroes.IndexOf(hero);
                        battle.ApplyFormation(new[] { index, (index + 1) % 10, (index + 2) % 10 });
                        battle.StartBattle();
                        var caster = battle.Units.Find(u => u.Stats == hero);
                        var target = battle.Units[3];
                        foreach (var unit in battle.Units) { unit.Cooldown = 100; unit.Hp = unit.MaxHp = 100000; }
                        caster.Target = target;
                        caster.SkillTarget = target;
                        caster.SkillStart = 0;
                        caster.LastSkillSlot = slot;
                        caster.SkillRank = 1;
                        caster.SkillHit = 0;
                        caster.AnimatedSkill = data.Skills[index, slot].Effect;
                        float hp = target.Hp;
                        battle.Tick(.2f);
                        Require(target.Hp == hp, hero.Id + " skill " + slot + " no windup damage");
                        int expected = caster.AnimatedSkill == SkillEffect.Double ? 2 : caster.AnimatedSkill == SkillEffect.Flurry ? 3 : 1;
                        for (int tick = 0; tick < 70; tick++) battle.Tick(.01f);
                        Require(caster.SkillHit == expected, hero.Id + " skill " + slot + " contact count");
                        if (expected > 1)
                        {
                            Require(target.DamageComboFinal, hero.Id + " final combo emphasis");
                            Require(Mathf.Abs(target.DamageComboTotal - (hp - target.Hp)) < .05f, hero.Id + " cumulative actual damage");
                        }
                    }
                }
                ApprovedAudioChecks.Run();
                Require(File.ReadAllText("Logs/ApprovedAudioChecks.txt").StartsWith("PASS"), "audio checks");
                File.WriteAllText("Logs/SemiSDChecks.txt", "PASS " + DateTime.UtcNow.ToString("O") + "\n87 atlases / 13 characters / 30 ally skills / 2 boss skills.\nSkill windup/contact counts, combo actual damage accumulation, death final hold, weapon audio timing verified.\n");
                Debug.Log("[AFK Trial] Semi SD integration checks PASS");
            }
            catch (Exception error) { File.WriteAllText("Logs/SemiSDChecks.txt", "FAIL\n" + error); Debug.LogException(error); }
        }
        static void CheckAtlas(string id, string clip)
        {
            var texture = Resources.Load<Texture2D>("SemiSD/" + id + "/" + clip);
            Require(texture && texture.width == 1280 && texture.height == 640, id + "/" + clip + " layout");
        }
        static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }

    public sealed class SemiSDReview : EditorWindow
    {
        readonly string[] ids = { "ice_knight", "scythe_knight", "banner_paladin", "lion_guardian", "hammer_maid", "katana_knight", "royal_knight", "sun_paladin", "tower_knight", "axe_warrior", "bandit", "bandit_captain", "bandit_chief" };
        int character, clipIndex, frame;
        bool play = true;
        [MenuItem("AFK Trial/Review Semi SD Animations")]
        public static void Open() { GetWindow<SemiSDReview>("Semi SD Review"); }
        void Update() { if (play) { frame = (int)(EditorApplication.timeSinceStartup * 8) % 8; Repaint(); } }
        void OnGUI()
        {
            character = EditorGUILayout.Popup("Character", character, ids);
            string[] clips = character < 10 ? new[] { "walk", "attack", "skill0", "skill1", "skill2", "hit", "death" }
                : character == 10 ? new[] { "walk", "attack", "hit", "death" } : new[] { "walk", "attack", "idle", "special", "hit", "death" };
            clipIndex = Mathf.Clamp(clipIndex, 0, clips.Length - 1);
            clipIndex = EditorGUILayout.Popup("Clip", clipIndex, clips);
            play = EditorGUILayout.Toggle("Play preview", play);
            frame = EditorGUILayout.IntSlider("Frame", frame + 1, 1, 8) - 1;
            var texture = Resources.Load<Texture2D>("SemiSD/" + ids[character] + "/" + clips[clipIndex]);
            Rect rect = GUILayoutUtility.GetRect(320, 320, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            EditorGUI.DrawRect(rect, new Color(.15f, .19f, .25f));
            float size = Mathf.Min(rect.width, rect.height);
            if (texture) GUI.DrawTextureWithTexCoords(new Rect(rect.center.x-size/2,rect.center.y-size/2,size,size),texture,new Rect(frame%4/4f,1-(frame/4+1)/2f,.25f,.5f));
        }
    }
}
