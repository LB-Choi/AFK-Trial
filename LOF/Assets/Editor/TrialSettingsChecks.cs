using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace AFKTrial.Editor
{
    internal static class TrialSettingsChecks
    {
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;
        private static void Require(bool value, string message)
        {
            if (!value) throw new InvalidOperationException("Settings check: " + message);
        }

        public static void Check(TrialData data)
        {
            string folder = Path.Combine(Application.temporaryCachePath, "afk-settings-" + Guid.NewGuid());
            Directory.CreateDirectory(folder);
            var host = new GameObject("Isolated settings checks");
            host.SetActive(false);
            var runner = host.AddComponent<TrialRunner>();
            void Set(string name, object value) => typeof(TrialRunner).GetField(name, Instance).SetValue(runner, value);
            object Get(string name) => typeof(TrialRunner).GetField(name, Instance).GetValue(runner);
            object Call(string name, params object[] args) => typeof(TrialRunner).GetMethod(name, Instance).Invoke(runner, args);
            void Menu(string name) => Set("systemMenu", Enum.Parse(typeof(TrialRunner).GetNestedType("SystemMenu", BindingFlags.NonPublic), name));
            string main = Path.Combine(folder, "progress.json"), backup = Path.Combine(folder, "last-victory.json");
            string settings = Path.Combine(folder, "settings.json");
            FileStream session = null;
            try
            {
                Set("settingsPath", settings);
                Call("LoadDisplaySettings");
                Require((bool)Get("showDamageNumbers"), "missing settings defaults ON");
                Require((bool)Call("SetDamageNumbers", false), "disable damage numbers saves");
                Set("showDamageNumbers", true);
                Call("LoadDisplaySettings");
                Require(!(bool)Get("showDamageNumbers"), "disabled setting survives reload");
                string settingsBefore = File.ReadAllText(settings);
                using (var locked = new FileStream(settings + ".tmp", FileMode.Create, FileAccess.Write, FileShare.None))
                    Require(!(bool)Call("SetDamageNumbers", true) && !(bool)Get("showDamageNumbers") &&
                        File.ReadAllText(settings) == settingsBefore, "failed preference save preserves previous value");

                var battle = new TrialBattle(data);
                var progress = battle.CaptureProgress();
                progress.highest = 5; progress.stage = 6; progress.gold = 1234; progress.tickets = 7;
                progress.goldVaultClears = 1;
                Require(battle.RestoreProgress(progress), "progress fixture");
                Set("battle", battle);
                Set("progressPath", main);
                Set("victoryBackupPath", backup);
                session = new FileStream(Path.Combine(folder, "session.lock"), FileMode.Create, FileAccess.ReadWrite, FileShare.None);
                Set("progressLock", session);
                Set("pendingSeconds", 120L); Set("equipmentSeed", 123L);
                Call("CheckpointProgress", true);

                // No combat ticks or restart delay accumulate in any system panel, even in a dungeon.
                for (int encounter = 0; encounter < 2; encounter++)
                {
                    if (encounter == 1) Require(battle.TryStartDungeon(TrialData.DungeonKind.GoldVault, 0), "pause dungeon fixture");
                    foreach (string menu in new[] { "Settings", "Help", "Information", "Reset", "Quit" })
                    {
                        Menu(menu);
                        Set("accumulator", .025f); Set("restartDelay", .3f);
                        Set("nextCheckpoint", double.MaxValue);
                        string before = JsonUtility.ToJson(battle.CaptureProgress());
                        float elapsed = battle.Elapsed, hp = battle.Units[0].Hp;
                        for (int tick = 0; tick < 4; tick++) Call("Update");
                        Require(battle.Elapsed == elapsed && battle.Units[0].Hp == hp &&
                            (float)Get("accumulator") == .025f && (float)Get("restartDelay") == .3f &&
                            JsonUtility.ToJson(battle.CaptureProgress()) == before, menu + " pauses combat without mutating the encounter");
                    }
                }
                battle.ForceTimeout();
                Menu("Settings");
                float delay = (float)Get("restartDelay");
                Call("Update");
                Require(battle.Finished && (float)Get("restartDelay") == delay, "completed encounter cannot advance while paused");
                battle.StartBattle();

                // Four dim rectangles cover exactly the complement of the highlighted target.
                foreach (bool landscape in new[] { true, false })
                {
                    Set("sideBattle", landscape);
                    int width = landscape ? 960 : 540, height = landscape ? 540 : 960;
                    for (int step = 0; step < 4; step++)
                    {
                        Set("helpStep", step);
                        var focus = (Rect)Call("HelpFocus", width, height);
                        var areas = (Rect[])typeof(TrialRunner).GetMethod("SpotlightDimAreas", BindingFlags.Static | BindingFlags.NonPublic)
                            .Invoke(null, new object[] { width, height, focus });
                        float area = focus.width * focus.height;
                        foreach (var dim in areas)
                        {
                            Require(dim.width >= 0 && dim.height >= 0 && !dim.Overlaps(focus), "dim leaves target unobscured");
                            area += dim.width * dim.height;
                        }
                        Require(Mathf.Approximately(area, width * height), "dim covers every point outside target");
                    }
                }

                using (var locked = new FileStream(main + ".tmp", FileMode.Create, FileAccess.Write, FileShare.None))
                    Require(!(bool)Call("SaveBeforeQuit"), "quit is blocked when saving fails");
                Require((bool)Call("SaveBeforeQuit"), "quit saves successfully after retry");

                Menu("Reset");
                string previousSave = File.ReadAllText(main);
                foreach (string invalid in new[] { "", "reset", "RESE" })
                {
                    Set("resetConfirmation", invalid);
                    Require(!(bool)Call("TryResetProgress") && File.ReadAllText(main) == previousSave && File.Exists(backup),
                        "reset requires exact confirmation and preserves files otherwise");
                }
                Set("resetConfirmation", "초기화");
                Menu("Settings");
                Require(!(bool)Call("TryResetProgress"), "confirmation text alone cannot reset outside confirmation screen");
                Menu("Reset");
                using (var locked = new FileStream(main + ".tmp", FileMode.Create, FileAccess.Write, FileShare.None))
                    Require(!(bool)Call("TryResetProgress") && ReferenceEquals(Get("battle"), battle) && File.ReadAllText(main) == previousSave,
                        "failed reset keeps active progress and committed main save");
                Require((bool)Call("TryResetProgress"), "confirmed reset succeeds after retry");
                var fresh = (TrialBattle)Get("battle");
                Require(fresh.Highest == 0 && fresh.Stage == 1 && fresh.Gold == 0 && fresh.Tickets == data.StartingTickets &&
                    !fresh.IsDungeonCleared(data.Dungeons[0, 0]), "reset restores starting progression and reward eligibility");
                Require((bool)Get("atTitle") && Get("progressLock") == null && !File.Exists(backup) &&
                    (long)Get("pendingSeconds") == 0 && (long)Get("equipmentSeed") == 0,
                    "reset returns to title, closes session and removes idle/recovery progress");
                Require(File.ReadAllText(settings) == settingsBefore && !(bool)Get("showDamageNumbers"), "reset preserves display preferences");
                var restored = new TrialBattle(data);
                Set("battle", restored);
                Call("ReadProgress", main);
                Require(JsonUtility.ToJson(restored.CaptureProgress()) == JsonUtility.ToJson(fresh.CaptureProgress()),
                    "fresh progress survives production save reload");
                Require(File.ReadAllText(main).Contains("\"pendingSeconds\":0"), "reset save cannot reclaim old offline time");
            }
            finally
            {
                session?.Dispose();
                Set("progressLock", null);
                UnityEngine.Object.DestroyImmediate(host);
                foreach (string file in Directory.GetFiles(folder)) File.Delete(file);
                Directory.Delete(folder);
            }
        }
    }
}
