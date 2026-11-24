using UnityEngine;

namespace AFKTrial
{
    public sealed partial class TrialRunner
    {
        private enum CeremonyPhase { Closed, Appointment, Sealed, Reveal }
        private CeremonyPhase ceremony;
        private TrialBattle.Pull[] ceremonyPulls;
        private int revealedKnight;
        private float ceremonyAge;
        private const float PaperArrival = .72f, StampImpact = .22f, DoorStart = .92f, DoorEnd = 2.12f;

        private void BeginSummonCeremony(TrialBattle.Pull[] pulls)
        {
            ceremonyPulls = pulls;
            revealedKnight = 0;
            ceremonyAge = 0;
            ceremony = CeremonyPhase.Appointment;
        }

        private void StampAppointment()
        {
            if (ceremony != CeremonyPhase.Appointment || ceremonyAge < PaperArrival) return;
            ceremony = CeremonyPhase.Sealed;
            ceremonyAge = 0;
            if (!Application.isPlaying) return;
            EnsureAudio();
            uiAudio.pitch = 1;
            uiAudio.volume = effectsVolume;
            uiAudio.PlayOneShot(Resources.Load<AudioClip>("Audio/SummonSealAndDoors"));
        }

        private void UpdateSummonCeremony(float delta)
        {
            ceremonyAge += delta;
            if (ceremony != CeremonyPhase.Sealed || ceremonyAge < DoorEnd) return;
            ceremony = CeremonyPhase.Reveal;
            ceremonyAge = 0;
            PlayBattleSound(TrialBattle.SoundCue.Reward);
        }

        private void AdvanceSummonReveal(bool skip)
        {
            if (ceremony != CeremonyPhase.Reveal) return;
            if (!skip && revealedKnight + 1 < ceremonyPulls.Length)
            {
                revealedKnight++;
                ceremonyAge = 0;
                return;
            }
            ceremony = CeremonyPhase.Closed;
            ceremonyPulls = null;
            animationStart = Time.unscaledTime;
        }

        private void DrawSummonCeremony(int width, int height)
        {
            var screen = new Rect(0, 0, width, height);
            var doors = FeatureTexture("SummonUI/ChamberDoors");
            float open = ceremony == CeremonyPhase.Reveal ? 1 : ceremony == CeremonyPhase.Sealed
                ? Mathf.SmoothStep(0, 1, Mathf.InverseLerp(DoorStart, DoorEnd, ceremonyAge)) : 0;
            GUI.DrawTexture(screen, FeatureTexture("KnightUI/ChapterHall"), ScaleMode.ScaleAndCrop);
            float half = width * .5f;
            float leaf = Mathf.Lerp(half, Mathf.Min(48, width * .06f), open);
            GUI.DrawTextureWithTexCoords(new Rect(0, 0, leaf, height), doors, new Rect(0, 0, .5f, 1));
            GUI.DrawTextureWithTexCoords(new Rect(width - leaf, 0, leaf, height), doors, new Rect(.5f, 0, .5f, 1));
            if (open > 0 && open < 1)
                Fill(new Rect(leaf, 0, width - 2 * leaf, height), new Color(1, .81f, .50f, .42f * (1 - open)));
            Fill(screen, new Color(.035f, .026f, .045f, Mathf.Lerp(.49f, .20f, open)));
            if (open > 0 && open < 1)
            {
                float spread = Mathf.SmoothStep(0, 1, Mathf.Clamp01(open / .60f));
                float glowWidth = width * Mathf.Lerp(.035f, 1.40f, spread);
                float glowHeight = height * Mathf.Lerp(.95f, 1.35f, spread);
                float intensity = Mathf.SmoothStep(0, 1, Mathf.Clamp01(open / .12f))
                    * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.43f, 1, open)));
                var previousTint = GUI.color;
                GUI.color = new Color(1, 1, 1, intensity);
                GUI.DrawTexture(new Rect(half - glowWidth / 2, height * .53f - glowHeight / 2, glowWidth, glowHeight),
                    FeatureTexture("SummonUI/DoorRadiance"));
                GUI.color = previousTint;
            }

            if (ceremony == CeremonyPhase.Reveal)
            {
                DrawSummonedKnight(screen);
                return;
            }

            bool sealedPaper = ceremony == CeremonyPhase.Sealed;
            float enter = sealedPaper ? 1 : Mathf.SmoothStep(0, 1, ceremonyAge / PaperArrival);
            float exit = sealedPaper ? Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.70f, 1.24f, ceremonyAge)) : 0;
            float w = Mathf.Min(width * .87f, height * 1.08f), h = w * 2 / 3;
            float restY = (height - h) * .46f;
            float y = Mathf.Lerp(height + 12, restY, enter * (1 - exit));
            if (sealedPaper && ceremonyAge >= StampImpact)
                y += 5 * (1 - Mathf.Clamp01((ceremonyAge - StampImpact) / .10f));
            var paper = new Rect(half - w / 2, y, w, h);
            GUI.DrawTexture(paper, FeatureTexture("SummonUI/Appointment"));
            var ink = new Color32(71, 44, 35, 255);
            ArcadeText(new Rect(paper.x, y + h * .17f, w, 48), "기사 임명장", 34, ink, TextAnchor.MiddleCenter);
            CourtRule(new Rect(half - w * .24f, y + h * .38f, w * .48f, 1), new Color32(148, 104, 61, 175));
            ArcadeText(new Rect(paper.x, y + h * .42f, w, 40), "그대를 기사단의 일원으로 임명한다.", 19, ink, TextAnchor.MiddleCenter);
            float size = h * .21f;
            var mark = new Rect(paper.x + w * .70f, y + h * .60f, size, size);
            var tint = GUI.color;
            bool struck = sealedPaper && ceremonyAge >= StampImpact;
            GUI.color = struck ? new Color(.68f, .12f, .08f, 1) : new Color(.36f, .23f, .18f, .29f);
            GUI.DrawTexture(mark, FeatureTexture("KnightUI/OrderSeal"));
            GUI.color = tint;
            if (sealedPaper && ceremonyAge < .48f)
            {
                float lift = ceremonyAge < StampImpact ? 1 - ceremonyAge / StampImpact :
                    Mathf.InverseLerp(StampImpact, .48f, ceremonyAge);
                float diameter = size * (1 + lift * .45f);
                var stampArt = FeatureTexture("SummonUI/Stamp");
                float stampHeight = diameter * stampArt.height / stampArt.width;
                var stamp = new Rect(mark.center.x - diameter / 2 + 65 * lift,
                    mark.center.y - stampHeight - lift * height * .53f, diameter, stampHeight);
                GUI.DrawTexture(stamp, stampArt);
            }
            if (!sealedPaper && ceremonyAge >= PaperArrival)
            {
                ArcadeText(new Rect(0, paper.yMax + 5, width, 36), "클릭하여 날인", 18, HallLight, TextAnchor.MiddleCenter);
                if (GUI.Button(screen, GUIContent.none, GUIStyle.none)) StampAppointment();
            }
        }

        private void DrawSummonedKnight(Rect screen)
        {
            var pull = ceremonyPulls[revealedKnight];
            float appear = Mathf.SmoothStep(0, 1, ceremonyAge / .32f);
            float size = Mathf.Min(screen.width * .78f, screen.height * .67f) * Mathf.Lerp(.88f, 1, appear);
            var actor = new Rect(screen.center.x - size / 2, screen.height * .74f - size, size, size);
            var tint = GUI.color;
            GUI.color = Color.Lerp(new Color(.39f, .34f, .32f, .70f), Color.white, appear);
            GUI.DrawTexture(actor, portraits[pull.Hero], ScaleMode.ScaleToFit);
            GUI.color = tint;
            float footer = screen.height * .74f;
            ManuscriptFade(new Rect(0, footer, screen.width, screen.height - footer),
                new Color32(20, 23, 31, 207), HallInk);
            ArcadeText(new Rect(0, footer + 5, screen.width, 28),
                (revealedKnight + 1) + " / " + ceremonyPulls.Length, 16, HallAmber, TextAnchor.MiddleCenter);
            CourtHeading(new Rect(0, footer + 31, screen.width, 45), battle.Heroes[pull.Hero], 31, TextAnchor.MiddleCenter);
            bool last = revealedKnight == ceremonyPulls.Length - 1;
            ArcadeText(new Rect(0, footer + 79, screen.width, 30), last ? "클릭하여 획득 결과 보기" : "클릭하여 다음 기사 공개",
                16, HallLight, TextAnchor.MiddleCenter);
            if (ceremonyPulls.Length == 10 && GUI.Button(new Rect(screen.width - 115, 16, 97, 42), "SKIP", button))
            {
                AdvanceSummonReveal(true);
                return;
            }
            if (GUI.Button(screen, GUIContent.none, GUIStyle.none)) AdvanceSummonReveal(false);
        }
    }
}
