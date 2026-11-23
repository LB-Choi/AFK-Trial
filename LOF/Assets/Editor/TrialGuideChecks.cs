using System;
using System.IO;
using System.Reflection;
using UnityEngine;
namespace AFKTrial.Editor
{
    internal static class TrialGuideChecks
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        private static void Require(bool ok, string message)
        { if (!ok) throw new InvalidOperationException("Guides: " + message); }
        public static void Check(TrialData data)
        {
            var battle = new TrialBattle(data);
            Require(battle.GuidePending(TrialBattle.Guide.FirstBattle), "fresh game offers first battle guide");
            battle.CreditIdle(10000, 10000);
            battle.ObserveGuideConditions();
            Require(battle.GuidePending(TrialBattle.Guide.Growth), "affordable deployed hero queues growth");
            var state = battle.CaptureProgress();
            state.highest = data.BossEvery - 1; state.stage = data.BossEvery;
            Require(battle.RestoreProgress(state), "boss fixture");
            battle.ForceTimeout();
            Require(battle.GuidePending(TrialBattle.Guide.BossDefeat), "boss defeat trigger");
            state = battle.CaptureProgress(); state.highest = data.BossEvery; state.stage = state.highest + 1; state.farming = false;
            Require(battle.RestoreProgress(state), "dungeon fixture"); battle.ObserveGuideConditions();
            Require(battle.GuidePending(TrialBattle.Guide.Dungeon), "first boss clearance unlock trigger");
            var gear = battle.EstimateIdleEquipment(72000, 123);
            Require(gear.Items.Count > 0, "idle equipment fixture");
            Require(battle.CollectIdle(1, 1, gear, p => true) &&
                battle.GuidePending(TrialBattle.Guide.Equipment) && battle.GuidePending(TrialBattle.Guide.Idle), "idle and equipment trigger");
            foreach (TrialBattle.Guide guide in Enum.GetValues(typeof(TrialBattle.Guide)))
                battle.CompleteGuide(guide, true);
            var restored = new TrialBattle(data);
            Require(restored.RestoreProgress(JsonUtility.FromJson<TrialBattle.Progress>(JsonUtility.ToJson(battle.CaptureProgress()))), "guide state loads");
            foreach (TrialBattle.Guide guide in Enum.GetValues(typeof(TrialBattle.Guide)))
                Require(restored.GuideSeen(guide) && restored.GuideSkipped(guide) && !restored.GuidePending(guide), "skipped states survive reload");
            var legacy = battle.CaptureProgress();
            legacy.guideVersion = legacy.guideReady = legacy.guideSeen = legacy.guideSkipped = 0;
            Require(restored.RestoreProgress(legacy) && !restored.GuidePending(TrialBattle.Guide.FirstBattle) &&
                restored.GuideSeen(TrialBattle.Guide.Dungeon), "old progress suppresses historical guides");
            legacy.highest = 0; legacy.stage = 1;
            Require(restored.RestoreProgress(legacy) && !restored.GuideSeen(TrialBattle.Guide.Dungeon), "old early progress retains future dungeon guide");
            legacy.guideVersion = 1; legacy.guideSeen = 128;
            Require(!restored.RestoreProgress(legacy), "invalid flags rejected");

            string folder = Path.Combine(Application.temporaryCachePath, "guides-" + Guid.NewGuid());
            Directory.CreateDirectory(folder);
            var host = new GameObject("Isolated guide checks"); host.SetActive(false);
            FileStream session = null;
            try
            {
                var runner = host.AddComponent<TrialRunner>();
                void Set(string name, object value) => typeof(TrialRunner).GetField(name, Flags).SetValue(runner, value);
                object Get(string name) => typeof(TrialRunner).GetField(name, Flags).GetValue(runner);
                object Call(string name, params object[] args) => typeof(TrialRunner).GetMethod(name, Flags).Invoke(runner, args);
                battle = new TrialBattle(data); Set("battle", battle);
                string path = Path.Combine(folder, "progress.json");
                Set("progressPath", path); Set("victoryBackupPath", Path.Combine(folder, "victory.json"));
                session = new FileStream(Path.Combine(folder, "session.lock"), FileMode.Create, FileAccess.ReadWrite, FileShare.None);
                Set("progressLock", session); Set("nextCheckpoint", double.MaxValue);
                Set("activeGuide", -1); Set("atTitle", false);
                Set("showRecruit", true); Call("Update");
                Require((int)Get("activeGuide") == -1, "recruit result first");
                Set("showRecruit", false);
                battle.Summon(1); Call("Update");
                Require((int)Get("activeGuide") == -1, "unconfirmed summon first"); battle.ConfirmSummons();
                Set("bossReview", new TrialBattle.BossAttempt()); Call("Update");
                Require((int)Get("activeGuide") == -1, "boss result first"); Set("bossReview", null);
                var idleProperty = typeof(TrialRunner).GetProperty("PendingIdleReward", Flags);
                object idleReward = Activator.CreateInstance(idleProperty.PropertyType);
                idleProperty.PropertyType.GetField("Gold").SetValue(idleReward, 1);
                idleProperty.SetValue(runner, idleReward);
                Call("Update"); Require((int)Get("activeGuide") == -1, "idle result first");
                idleProperty.SetValue(runner, Activator.CreateInstance(idleProperty.PropertyType));
                Set("editing", true);
                Set("heroView", Enum.Parse(typeof(TrialRunner).GetNestedType("HeroView", BindingFlags.NonPublic), "Info"));
                Set("guideNotBefore", 0d); Call("Update");
                Require((int)Get("activeGuide") == -1, "hero detail is not interrupted with a hidden dock target");
                Set("editing", false);
                Set("guideNotBefore", 0d); Call("Update");
                Require((int)Get("activeGuide") == 0, "first guide opens independently");
                Set("accumulator", .025f); Set("restartDelay", .3f);
                float elapsed = battle.Elapsed, hp = battle.Units[0].Hp;
                string before = JsonUtility.ToJson(battle.CaptureProgress());
                for (int i = 0; i < 5; i++) Call("Update");
                Require(battle.Elapsed == elapsed && battle.Units[0].Hp == hp &&
                    (float)Get("accumulator") == .025f && (float)Get("restartDelay") == .3f &&
                    JsonUtility.ToJson(battle.CaptureProgress()) == before, "guide freezes combat, skills and timers");
                using (var locked = new FileStream(path + ".tmp", FileMode.Create, FileAccess.Write, FileShare.None))
                    Call("DismissGuide", true);
                Require((int)Get("activeGuide") == -1 && battle.GuideSkipped(TrialBattle.Guide.FirstBattle), "failed acknowledgement closes and remains skipped in memory");
                Call("CheckpointProgress", false);
                Call("ReadProgress", path);
                Require(battle.GuideSeen(TrialBattle.Guide.FirstBattle), "checkpoint retries acknowledgement and persists");
                battle.CreditIdle(10000, 10000); Set("guideNotBefore", 0d); Call("Update");
                Require((int)Get("activeGuide") == 1, "skipping one does not block later guide");
                Call("DismissGuide", false);
                Require(battle.GuideSeen(TrialBattle.Guide.Growth) && !battle.GuideSkipped(TrialBattle.Guide.Growth), "confirmed status distinct from skipped");
                Set("activeGuide", 4); Set("guideReplay", true);
                before = JsonUtility.ToJson(battle.CaptureProgress()); Call("DismissGuide", false);
                Require(JsonUtility.ToJson(battle.CaptureProgress()) == before, "manual replay does not alter progress");
                Set("guideReplay", false);
                foreach (bool side in new[] { true, false })
                foreach (bool editing in new[] { true, false })
                {
                    Set("sideBattle", side); Set("editing", editing);
                    int width = side ? 960 : 540, height = side ? 540 : 960;
                    for (int i = 0; i < 6; i++)
                    {
                        Set("activeGuide", i);
                        var focus = (Rect)Call("GuideFocus", width, height);
                        Require(focus.xMin >= 0 && focus.yMin >= 0 && focus.xMax <= width && focus.yMax <= height, "guide focus within canvas");
                    }
                }
                Set("activeGuide", -1);
                var font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Arial" }, 18);
                try
                {
                    var style = new GUIStyle { font = font, fontSize = 17, wordWrap = true };
                    var texts = (string[])typeof(TrialRunner).GetField("GuideTexts", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
                    foreach (string text in texts)
                    {
                        float textHeight = style.CalcHeight(new GUIContent(text), 424);
                        Require(textHeight > 0 && textHeight <= 165, "Korean guide text fits allocated panel height");
                    }
                }
                finally { UnityEngine.Object.DestroyImmediate(font); }
                var fresh = new TrialBattle(data);
                Require(fresh.GuidePending(TrialBattle.Guide.FirstBattle) && !fresh.GuideSeen(TrialBattle.Guide.Growth), "reset starts fresh");
            }
            finally
            {
                if (session != null) { typeof(TrialRunner).GetField("progressLock", Flags).SetValue(host.GetComponent<TrialRunner>(), null); session.Dispose(); }
                UnityEngine.Object.DestroyImmediate(host); Directory.Delete(folder, true);
            }
        }
    }
}
