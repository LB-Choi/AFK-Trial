using System;

namespace AFKTrial
{
    public sealed partial class TrialBattle
    {
        public enum Guide { FirstBattle, Growth, Equipment, BossDefeat, Dungeon, Idle }
        public const int GuideMask = 63;
        private int guideReady = 1, guideSeen, guideSkipped;
        public bool GuidePending(Guide guide) => (guideReady & (1 << (int)guide)) != 0 && !GuideSeen(guide);
        public bool GuideSeen(Guide guide) => (guideSeen & (1 << (int)guide)) != 0;
        public bool GuideSkipped(Guide guide) => (guideSkipped & (1 << (int)guide)) != 0;
        public void QueueGuide(Guide guide) => guideReady |= 1 << (int)guide;
        public void ObserveGuideConditions()
        {
            foreach (int hero in formation)
                if (hero >= 0 && CanUpgradeHero(hero)) { QueueGuide(Guide.Growth); break; }
            if (Highest >= Data.BossEvery) QueueGuide(Guide.Dungeon);
        }
        public void CompleteGuide(Guide guide, bool skipped)
        {
            int bit = 1 << (int)guide;
            guideReady |= bit;
            guideSeen |= bit;
            if (skipped) guideSkipped |= bit;
        }
        private static bool ValidGuides(Progress saved) =>
            saved.guideVersion >= 0 && saved.guideVersion <= 1 &&
            (saved.guideReady & ~GuideMask) == 0 && (saved.guideSeen & ~GuideMask) == 0 &&
            (saved.guideSkipped & ~saved.guideSeen) == 0;
        private void RestoreGuides(Progress saved)
        {
            guideReady = saved.guideVersion == 0 ? 1 : saved.guideReady;
            guideSeen = saved.guideSeen; guideSkipped = saved.guideSkipped;
            if (saved.guideVersion == 0)
            {
                // Historical events cannot be inferred reliably. Suppress old one-shot events.
                CompleteGuide(Guide.FirstBattle, true);
                CompleteGuide(Guide.Idle, true);
                if (saved.inventory.Length > 0) CompleteGuide(Guide.Equipment, true);
                if (saved.highest > 0) CompleteGuide(Guide.Growth, true);
                if (saved.farming || saved.highest >= Data.BossEvery) CompleteGuide(Guide.BossDefeat, true);
                if (saved.highest >= Data.BossEvery) CompleteGuide(Guide.Dungeon, true);
            }
        }
    }
}
