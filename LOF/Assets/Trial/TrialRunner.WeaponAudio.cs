using System.Collections.Generic;
using UnityEngine;

namespace AFKTrial
{
    public sealed partial class TrialRunner
    {
        private readonly Dictionary<string, AudioClip> weaponClips = new Dictionary<string, AudioClip>();
        private readonly Dictionary<TrialBattle.Unit, float> lastWeaponHit = new Dictionary<TrialBattle.Unit, float>();

        internal static string WeaponSoundKey(string animation)
        {
            switch (animation)
            {
                case "katana_knight": return "Katana";
                case "banner_paladin": return "Spear";
                case "lion_guardian": case "bandit_captain": return "Mace";
                case "hammer_maid": return "Hammer";
                case "tower_knight": return "Shield";
                case "axe_warrior": return "Axe";
                case "bandit": return "Dagger";
                default: return "Sword";
            }
        }

        private void PlayWeaponSound(TrialBattle.Unit attacker, bool impact, bool special)
        {
            if (!Application.isPlaying || suspended || atTitle || effectsVolume <= 0) return;
            string weapon = WeaponSoundKey(attacker.Stats.Animation);
            if (impact)
            {
                // AoE targets share one physical strike; never restart a clip for each target.
                if (lastWeaponHit.TryGetValue(attacker, out float last) && last == battle.Elapsed) return;
                lastWeaponHit[attacker] = battle.Elapsed;
            }
            bool heavy = weapon == "Hammer" || weapon == "Mace" || weapon == "Shield" ||
                weapon == "Axe" || attacker.Stats.Animation == "sun_paladin" ||
                attacker.Stats.Animation == "royal_knight" || attacker.Stats.Animation == "scythe_knight";
            string key = impact ? weapon : heavy ? "SwingHeavy" : "SwingLight";
            if (!weaponClips.TryGetValue(key, out AudioClip clip))
            {
                clip = Resources.Load<AudioClip>("Audio/Weapons/" + key);
                weaponClips[key] = clip;
            }
            if (!clip) return;
            combatAudio.volume = effectsVolume;
            combatAudio.pitch = 1f;
            combatAudio.PlayOneShot(clip, impact ? (special ? .65f : .42f) : .28f);
        }
    }
}
