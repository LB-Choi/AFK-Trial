using System;
using UnityEngine;
namespace AFKTrial.Editor
{
    internal static class TrialAudioChecks
    {
        public static void Check()
        {
            var background = Resources.Load<AudioClip>("Audio/Background");
            if (background == null || background.length < 10)
                throw new InvalidOperationException("Approved Hope soundtrack is missing.");
            foreach (string name in new[] { "CombatHit", "BossWarning", "BossSpecial", "Reward" })
            {
                var clip = Resources.Load<AudioClip>("Audio/" + name);
                if (clip == null || clip.length <= 0 || clip.length > 5) throw new InvalidOperationException("Audio clip missing or too long: " + name);
                var samples = new float[clip.samples * clip.channels];
                if (!clip.GetData(samples, 0)) throw new InvalidOperationException("Audio data unavailable: " + name);
                float peak = 0;
                foreach (float sample in samples) peak = Mathf.Max(peak, Mathf.Abs(sample));
                if (peak < .001f) throw new InvalidOperationException("Silent audio: " + name);
                Debug.Log("Audio checked: " + name + " duration=" + clip.length + " peak=" + peak);
            }
        }
    }
}
