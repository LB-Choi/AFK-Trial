using System;
using UnityEngine;

namespace AFKTrial
{
    public sealed partial class TrialRunner
    {
        private bool applicationPaused, restartAfterAway;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private IntPtr playerWindow;
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        private static extern bool IsIconic(IntPtr window);
#endif
        private void InitializeLifecycle()
        {
            applicationPaused = restartAfterAway = false;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            // Keep polling while minimized; combat itself is gated by suspended.
            Application.runInBackground = true;
#endif
        }
        private bool WindowMinimized()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (playerWindow == IntPtr.Zero)
                using (var process = System.Diagnostics.Process.GetCurrentProcess())
                    playerWindow = process.MainWindowHandle;
            return playerWindow != IntPtr.Zero && IsIconic(playerWindow);
#else
            return false;
#endif
        }
        private bool RefreshLifecycle()
        {
            if (progressLock == null) return false;
            return SetSuspension(applicationPaused || WindowMinimized(), UtcNow);
        }
        private void OnApplicationFocus(bool focused) => RefreshLifecycle();
        private void OnApplicationPause(bool paused)
        {
            applicationPaused = paused;
            RefreshLifecycle();
        }
        private bool SetSuspension(bool away, long now)
        {
            if (progressLock == null || suspended == away) return false;
            if (away)
            {
                // Establish the exact start of absence, excluding time spent in menus/results.
                lastSeenUtc = Math.Max(lastSeenUtc, now);
                TrySaveProgress(battle.CaptureProgress(), pendingSeconds);
                suspended = true;
            }
            else
            {
                CountTimeAway(now);
                suspended = false;
                restartAfterAway = true;
                TrySaveProgress(battle.CaptureProgress(), pendingSeconds);
            }
            accumulator = restartDelay = 0;
            nextCheckpoint = Time.realtimeSinceStartupAsDouble + CheckpointSeconds;
            return true;
        }
        private void ResumeBattleAfterAway()
        {
            if (!restartAfterAway) return;
            restartAfterAway = false;
            // Completed result screens remain intact; their own buttons choose the next battle.
            battle.RestartCurrentEncounter();
            playingSkills.Clear();
            accumulator = restartDelay = 0;
        }
    }
}
