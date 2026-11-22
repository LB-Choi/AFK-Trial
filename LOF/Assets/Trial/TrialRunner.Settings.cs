using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace AFKTrial
{
    public sealed partial class TrialRunner
    {
        private enum SystemMenu { Closed, Settings, Help, Information, Reset, Quit }
        [Serializable]
        private sealed class DisplaySettings
        {
            public int version = 1;
            public bool showDamageNumbers = true;
        }

        private SystemMenu systemMenu;
        private bool showDamageNumbers = true;
        private string settingsPath, settingsError, resetConfirmation = "";
        private int helpStep;
        private GUIStyle settingsBody;
        private bool SystemMenuOpen => systemMenu != SystemMenu.Closed;
        private bool CanOpenSystemMenu => !showRecruit && !HasPendingIdleReward && !HasDungeonResult;

        private void LoadDisplaySettings()
        {
            if (string.IsNullOrEmpty(settingsPath))
                settingsPath = Path.Combine(Application.persistentDataPath, "AFKTrial", "settings.json");
            showDamageNumbers = true;
            settingsError = null;
            try
            {
                if (!File.Exists(settingsPath)) return;
                var saved = JsonUtility.FromJson<DisplaySettings>(File.ReadAllText(settingsPath));
                if (saved == null || saved.version != 1) throw new InvalidDataException("Unsupported display settings.");
                showDamageNumbers = saved.showDamageNumbers;
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is ArgumentException)
            {
                settingsError = "Display settings could not be read. Default: damage numbers ON.";
                Debug.LogWarning(settingsError);
            }
        }

        private bool SetDamageNumbers(bool enabled)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(settingsPath));
                WriteAtomically(settingsPath, Encoding.UTF8.GetBytes(JsonUtility.ToJson(new DisplaySettings { showDamageNumbers = enabled })));
                showDamageNumbers = enabled;
                settingsError = null;
                return true;
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                settingsError = "Could not save this setting. Check storage and try again.";
                return false;
            }
        }

        private void OpenSystemMenu()
        {
            if (!CanOpenSystemMenu) return;
            systemMenu = SystemMenu.Settings;
            resetConfirmation = "";
        }

        private void BackFromSystemMenu()
        {
            systemMenu = systemMenu == SystemMenu.Settings ? SystemMenu.Closed : SystemMenu.Settings;
            resetConfirmation = "";
            GUI.FocusControl(null);
        }

        private bool TryResetProgress()
        {
            if (systemMenu != SystemMenu.Reset || resetConfirmation != "RESET" || progressLock == null || suspended) return false;
            var fresh = new TrialBattle(battle.Data);
            long oldSeen = lastSeenUtc;
            try
            {
                // Remove the recovery copy first so an old victory can never resurrect a reset profile.
                // If the main write fails, the existing main save and live battle remain intact.
                if (File.Exists(victoryBackupPath)) File.Delete(victoryBackupPath);
                lastSeenUtc = UtcNow;
                WriteProgress(fresh.CaptureProgress(), 0);
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                lastSeenUtc = oldSeen;
                settingsError = "Reset could not be saved. Your current progress is still active. Please retry.";
                return false;
            }
            battle.SkillApplied -= PlaySkillEffect;
            battle = fresh;
            battle.SkillApplied += PlaySkillEffect;
            playingSkills.Clear();
            pendingSeconds = equipmentSeed = 0;
            PendingIdleReward = default;
            idleSaveError = settingsError = null;
            resultDungeon = null;
            showRecruit = editing = choosingRepeat = false;
            featurePage = -1;
            heroView = HeroView.Menu;
            selectedDungeon = null;
            selectedGear = pendingSale = null;
            lastPulls = null;
            selectedSkill = -1;
            featureHero = selectedSlot = dungeonDifficulty = inventoryPage = 0;
            gearFilter = -1;
            accumulator = restartDelay = 0;
            animationStart = float.NegativeInfinity;
            progressLock.Dispose();
            progressLock = null;
            atTitle = true;
            systemMenu = SystemMenu.Closed;
            resetConfirmation = "";
            return true;
        }

        private bool SaveBeforeQuit()
        {
            if (progressLock == null) return true;
            CheckpointProgress();
            if (string.IsNullOrEmpty(idleSaveError)) return true;
            settingsError = "Could not save. Quit was cancelled; retry after checking storage.";
            return false;
        }

        private void QuitGame()
        {
            if (!SaveBeforeQuit()) return;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void DrawSystemMenu(int width, int height)
        {
            if (settingsBody == null)
                settingsBody = new GUIStyle(small) { fontSize = 17, wordWrap = true, alignment = TextAnchor.UpperLeft };
            if (systemMenu == SystemMenu.Help) { DrawSpotlightHelp(width, height); return; }
            Fill(new Rect(0, 0, width, height), new Color(0.015f, 0.025f, 0.04f, .82f));
            const float panelWidth = 464, panelHeight = 490;
            GUI.BeginGroup(new Rect((width - panelWidth) / 2, (height - panelHeight) / 2, panelWidth, panelHeight));
            DrawKnightPanel(new Rect(0, 0, panelWidth, panelHeight));
            string heading = systemMenu == SystemMenu.Settings ? "Settings" : systemMenu == SystemMenu.Information ? "Game information" :
                systemMenu == SystemMenu.Reset ? "Reset progress?" : "Quit game?";
            CourtHeading(new Rect(24, 14, 360, 42), heading, 29, TextAnchor.MiddleLeft);
            ArcadeText(new Rect(25, 58, 410, 23), atTitle ? "AFK TRIAL" : "BATTLE PAUSED", 13, HallAmber, TextAnchor.MiddleLeft);
            CourtRule(new Rect(24, 87, 416, 1), HallLine);
            if (systemMenu == SystemMenu.Settings)
            {
                GUI.Label(new Rect(26, 108, 275, 31), "Damage numbers", label);
                if (GUI.Button(new Rect(324, 104, 112, 35), showDamageNumbers ? "ON" : "OFF", button)) SetDamageNumbers(!showDamageNumbers);
                ArcadeText(new Rect(26, 144, 410, 24), "Only floating damage text is affected.", 14, BodyInk, TextAnchor.MiddleLeft);
                GUI.enabled = !atTitle;
                if (GUI.Button(new Rect(26, 184, 200, 43), "Help / guided tour", button)) { helpStep = 0; systemMenu = SystemMenu.Help; }
                GUI.enabled = true;
                if (GUI.Button(new Rect(238, 184, 200, 43), "Game information", button)) systemMenu = SystemMenu.Information;
                GUI.enabled = progressLock != null && !suspended;
                if (GUI.Button(new Rect(26, 243, 200, 43), "Reset progress", button)) { systemMenu = SystemMenu.Reset; resetConfirmation = ""; settingsError = null; }
                GUI.enabled = true;
                if (GUI.Button(new Rect(238, 243, 200, 43), "Quit game", button)) { systemMenu = SystemMenu.Quit; settingsError = null; }
                GUI.Label(new Rect(26, 306, 412, 47), atTitle ? "Enter the game to view the guided tour or reset progress." :
                    "Close settings to continue the same battle.\nEsc opens settings from other menus.", settingsBody);
            }
            else if (systemMenu == SystemMenu.Information)
            {
                GUI.Label(new Rect(26, 109, 412, 275), "AFK Trial  /  " + Application.version +
                    "\n\nA local single-player auto-battle prototype.\nCollect 10 knights, build a party of 1-3 and grow through main stages and resource dungeons." +
                    "\n\nProgress is saved on this computer. Display settings are kept separately and survive a progress reset." +
                    "\n\nBuilt with Unity. AI-assisted planning, art and implementation.", settingsBody);
            }
            else if (systemMenu == SystemMenu.Reset)
            {
                GUI.Label(new Rect(26, 108, 412, 155), "This permanently resets ALL game progress on this computer: knights, levels, skills, gear, currencies, summons, stages and dungeon first-clear rewards.\n\nOffline rewards and the recovery save are also cleared. This cannot be undone.", settingsBody);
                GUI.Label(new Rect(26, 270, 412, 27), "Type RESET to confirm:", label);
                GUI.SetNextControlName("ResetConfirmation");
                resetConfirmation = GUI.TextField(new Rect(26, 303, 412, 32), resetConfirmation, 5);
                GUI.enabled = resetConfirmation == "RESET" && progressLock != null && !suspended;
                if (GUI.Button(new Rect(26, 345, 412, 40), "Erase progress and return to title", button)) TryResetProgress();
                GUI.enabled = true;
            }
            else
            {
                GUI.Label(new Rect(26, 111, 412, 115), "Your progress will be saved before exiting.\n\nOn your next visit, the current main stage starts again and offline rewards are calculated.", settingsBody);
                if (GUI.Button(new Rect(26, 262, 412, 45), "Save and quit", button)) QuitGame();
            }
            if (!string.IsNullOrEmpty(settingsError)) GUI.Label(new Rect(26, 390, 412, 46), settingsError, small);
            if (GUI.Button(new Rect(130, 445, 204, 32), systemMenu == SystemMenu.Settings ? "Close / resume" : "Cancel / back", button)) BackFromSystemMenu();
            GUI.EndGroup();
        }

        private Rect HelpFocus(int width, int height)
        {
            float tray = sideBattle ? 420 : 696;
            switch (helpStep)
            {
                case 0: return new Rect(6, 4, width - 12, 106 + StageHeaderOffset);
                case 1: return sideBattle ? new Rect(12, tray + 6, 488, 107) : new Rect(12, tray + 9, width - 24, 164);
                case 2: return sideBattle ? new Rect(509, tray + 5, width - 515, 110) : new Rect(4, 875, width - 8, 80);
                default: return new Rect(10, tray - 47, width - 20, 45);
            }
        }

        private static Rect[] SpotlightDimAreas(int width, int height, Rect focus)
        {
            return new[] {
                new Rect(0, 0, width, focus.yMin), new Rect(0, focus.yMax, width, height - focus.yMax),
                new Rect(0, focus.yMin, focus.xMin, focus.height), new Rect(focus.xMax, focus.yMin, width - focus.xMax, focus.height)
            };
        }

        private void DrawSpotlightHelp(int width, int height)
        {
            Rect focus = HelpFocus(width, height);
            foreach (var area in SpotlightDimAreas(width, height, focus)) Fill(area, new Color(0.01f, .015f, .025f, .86f));
            CourtRule(new Rect(focus.x, focus.y, focus.width, 2), HallAmber);
            CourtRule(new Rect(focus.x, focus.yMax - 2, focus.width, 2), HallAmber);
            Fill(new Rect(focus.x, focus.y, 2, focus.height), HallAmber);
            Fill(new Rect(focus.xMax - 2, focus.y, 2, focus.height), HallAmber);
            bool dungeon = battle.ActiveDungeon != null;
            string[] headings = { "Stage and battle timer", "Your deployed knights", dungeon ? "Dungeon rewards" : "Growth and activities", dungeon ? "Dungeon outcome" : "Defeat, farming and retry" };
            string[] text = {
                "Battles are automatic. Main-stage bosses appear at stages 5 and 10 in each round. The timer is the remaining battle time.\n\nThe top-right menu pauses combat. In a dungeon, Retreat leaves without a reward.",
                "These are the knights fighting in the CURRENT battle. The bars show their remaining HP.\n\nDeploy 1-3 knights. Slot 1 is the front row: damage dealt and received both increase by 20%. Growth, gear and formation changes apply from the NEXT battle.",
                dungeon ? "Victory grants the displayed dungeon currency. Each dungeon difficulty also grants 1 summon ticket on its first clear only.\n\nDefeat or retreat grants nothing. Finish the result screen to return to the main stage." :
                    "Heroes: levels, skills and equipped gear. Formation: choose your party. Summon: spend tickets to recruit knights.\n\nHall opens gear, dungeons, stage repeat and idle rewards. Dungeons unlock after the first stage-5 boss. Offline rewards accumulate for up to 20 hours.",
                dungeon ? "The status line shows the current battle outcome. A defeat does not consume the first-clear reward.\n\nEasy, Normal and Hard unlock in order, independently for each dungeon. Cleared tiers can be repeated for currency, but not another first-clear ticket." :
                    "After defeat, your party farms a lower stage. Grow your knights or change formation, then press Retry when it appears here.\n\nA manually selected repeat stage continues until you stop it in Hall. Progress saves automatically. Close settings to resume the same battle."
            };
            const float boxWidth = 464, boxHeight = 270;
            float top = helpStep == 0 ? (sideBattle ? 167 : 230) : (sideBattle ? 96 : 210);
            GUI.BeginGroup(new Rect((width - boxWidth) / 2, top, boxWidth, boxHeight));
            DrawKnightPanel(new Rect(0, 0, boxWidth, boxHeight));
            CourtHeading(new Rect(20, 12, 424, 34), headings[helpStep], 23, TextAnchor.MiddleLeft);
            GUI.Label(new Rect(20, 56, 424, 160), text[helpStep], settingsBody);
            GUI.enabled = helpStep > 0;
            if (GUI.Button(new Rect(20, 226, 92, 32), "Previous", button)) helpStep--;
            GUI.enabled = true;
            if (GUI.Button(new Rect(123, 226, 104, 32), "Close", button)) systemMenu = SystemMenu.Settings;
            ArcadeText(new Rect(239, 226, 78, 32), (helpStep + 1) + " / 4", 14, HallAmber, TextAnchor.MiddleCenter);
            if (GUI.Button(new Rect(329, 226, 115, 32), helpStep == 3 ? "Done" : "Next", button))
            {
                if (helpStep == 3) systemMenu = SystemMenu.Settings;
                else helpStep++;
            }
            GUI.EndGroup();
        }
    }
}
