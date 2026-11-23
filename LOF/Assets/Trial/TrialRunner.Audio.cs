using System;
using UnityEngine;

namespace AFKTrial
{
    public sealed partial class TrialRunner
    {
        private float musicVolume = .3f, effectsVolume = .65f;
        private float draftMusic, draftEffects;
        private AudioSource musicAudio, combatAudio, priorityAudio, uiAudio;
        private AudioClip hitClip, warningClip, specialClip, rewardClip;
        private TrialBattle audioBattle;
        private double nextHitSound, suppressHitsUntil;
        private bool audioCombatPaused, audioAway;
        private string audioSaveNotice;
        private float PreviewMusicVolume => systemMenu == SystemMenu.Audio ? draftMusic : musicVolume;

        private AudioSource AddAudioSource(bool loop = false)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false; source.loop = loop; source.spatialBlend = 0;
            return source;
        }
        private void EnsureAudio()
        {
            if (musicAudio == null)
            {
                musicAudio = AddAudioSource(true); combatAudio = AddAudioSource();
                priorityAudio = AddAudioSource(); uiAudio = AddAudioSource();
                combatAudio.priority = 160; priorityAudio.priority = 32;
                hitClip = Resources.Load<AudioClip>("Audio/CombatHit");
                warningClip = Resources.Load<AudioClip>("Audio/BossWarning");
                specialClip = Resources.Load<AudioClip>("Audio/BossSpecial");
                rewardClip = Resources.Load<AudioClip>("Audio/Reward");
                // Optional approved soundtrack. The rejected base ambience is excluded from Resources.
                musicAudio.clip = Resources.Load<AudioClip>("Audio/Background");
                musicAudio.volume = musicVolume * .4f;
                if (musicAudio.clip != null) musicAudio.Play();
            }
            if (audioBattle == battle) return;
            if (audioBattle != null) audioBattle.SoundRequested -= PlayBattleSound;
            audioBattle = battle;
            if (audioBattle != null) audioBattle.SoundRequested += PlayBattleSound;
            nextHitSound = suppressHitsUntil = 0;
        }
        private void LateUpdate()
        {
            EnsureAudio();
            bool duck = systemMenu != SystemMenu.Audio && (battle?.ActiveBoss?.SpecialAttackPending == true ||
                Time.realtimeSinceStartupAsDouble < suppressHitsUntil);
            float ambienceGain = duck ? .1f : .4f;
            musicAudio.volume = Mathf.MoveTowards(musicAudio.volume, PreviewMusicVolume * ambienceGain, Time.unscaledDeltaTime * .8f);
            if (PreviewMusicVolume <= 0) musicAudio.volume = 0;
            combatAudio.volume = priorityAudio.volume = effectsVolume;
            uiAudio.volume = systemMenu == SystemMenu.Audio ? draftEffects : effectsVolume;
            bool paused = atTitle || suspended || SystemMenuOpen || GuideOpen;
            if (paused && !audioCombatPaused) { combatAudio.Stop(); priorityAudio.Stop(); }
            audioCombatPaused = paused;
            if (suspended != audioAway)
            {
                if (suspended) { musicAudio.Pause(); uiAudio.Stop(); }
                else musicAudio.UnPause();
                audioAway = suspended;
            }
        }
        private void PlayBattleSound(TrialBattle.SoundCue cue)
        {
            if (!Application.isPlaying || suspended || effectsVolume <= 0) return;
            EnsureAudio();
            double now = Time.realtimeSinceStartupAsDouble;
            if (cue == TrialBattle.SoundCue.Reward)
            { uiAudio.Stop(); uiAudio.volume = effectsVolume; uiAudio.pitch = 1.4f; uiAudio.PlayOneShot(rewardClip, .45f); return; }
            if (atTitle || SystemMenuOpen || GuideOpen) return;
            if (cue == TrialBattle.SoundCue.Hit)
            {
                if (now < nextHitSound || now < suppressHitsUntil) return;
                nextHitSound = now + .15;
                combatAudio.volume = effectsVolume;
                combatAudio.Stop();
                combatAudio.PlayOneShot(hitClip, .22f);
            }
            else
            {
                combatAudio.Stop(); suppressHitsUntil = now + .6;
                priorityAudio.volume = effectsVolume;
                priorityAudio.Stop();
                priorityAudio.pitch = cue == TrialBattle.SoundCue.BossWarning ? .8f : 1f;
                priorityAudio.PlayOneShot(cue == TrialBattle.SoundCue.BossWarning ? warningClip : specialClip, .65f);
            }
        }
        private void ReleaseAudio()
        {
            if (audioBattle != null) audioBattle.SoundRequested -= PlayBattleSound;
            audioBattle = null;
            foreach (var source in new[] { musicAudio, combatAudio, priorityAudio, uiAudio })
                if (source != null) { source.Stop(); Destroy(source); }
            musicAudio = combatAudio = priorityAudio = uiAudio = null;
        }
        private bool SetAudioVolumes(float music, float effects)
        {
            if (float.IsNaN(music) || float.IsInfinity(music) || float.IsNaN(effects) || float.IsInfinity(effects)) return false;
            return SaveDisplaySettings(showDamageNumbers, Mathf.Clamp01(music), Mathf.Clamp01(effects));
        }
        private void DrawAudioSettings(int width, int height)
        {
            float previousMusic = draftMusic, previousEffects = draftEffects;
            Fill(new Rect(0, 0, width, height), new Color(.01f, .02f, .03f, .86f));
            GUI.BeginGroup(new Rect((width - 464) / 2, (height - 420) / 2, 464, 420));
            DrawKnightPanel(new Rect(0, 0, 464, 420));
            CourtHeading(new Rect(24, 15, 416, 35), "소리 설정", 27, TextAnchor.MiddleLeft);
            GUI.Label(new Rect(26, 65, 412, 26), "배경음  " + Mathf.RoundToInt(draftMusic * 100) + "%", label);
            draftMusic = GUI.HorizontalSlider(new Rect(26, 102, 412, 22), draftMusic, 0, 1);
            GUI.Label(new Rect(26, 127, 412, 38), musicAudio != null && musicAudio.clip != null
                ? "배경음 반복 재생 · 보스 예고 시 음량 감소"
                : "배경음 미등록 · 음량 설정은 저장할 수 있습니다.", small);
            GUI.Label(new Rect(26, 178, 412, 26), "효과음  " + Mathf.RoundToInt(draftEffects * 100) + "%", label);
            draftEffects = GUI.HorizontalSlider(new Rect(26, 215, 412, 22), draftEffects, 0, 1);
            if (previousMusic != draftMusic || previousEffects != draftEffects)
            { audioSaveNotice = null; settingsError = null; }
            if (GUI.Button(new Rect(26, 251, 200, 36), "효과음 미리 듣기", button))
            {
                EnsureAudio(); uiAudio.Stop(); uiAudio.volume = draftEffects; uiAudio.pitch = .8f;
                uiAudio.PlayOneShot(warningClip, .65f);
            }
            if (GUI.Button(new Rect(238, 251, 200, 36), "음량 저장", button))
                audioSaveNotice = SetAudioVolumes(draftMusic, draftEffects) ? "음량을 저장했습니다. 재접속 후에도 유지됩니다." : null;
            GUI.Label(new Rect(26, 304, 412, 48), settingsError ?? audioSaveNotice ??
                "조절 즉시 미리 듣기 · 0%는 음소거입니다.\n저장하지 않고 나가면 이전 음량으로 돌아갑니다.", small);
            if (GUI.Button(new Rect(130, 365, 204, 33), "뒤로", button)) BackFromSystemMenu();
            GUI.EndGroup();
        }
    }
}
