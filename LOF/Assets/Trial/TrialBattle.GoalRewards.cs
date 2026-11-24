using System;

namespace AFKTrial
{
    public sealed partial class TrialBattle
    {
        private bool goalIntroClaimed;
        private int goalBossClaimed, goalRoundClaimed;
        public bool IntroGoalRewardClaimed => goalIntroClaimed;
        public bool CanClaimIntroGoalReward => !goalIntroClaimed && CompletedGoals == GoalMask;
        public int PendingBossGoals => goalIntroClaimed ? Math.Max(0, Highest / Data.BossEvery - goalBossClaimed) : 0;
        public int PendingRoundGoals => goalIntroClaimed ? Math.Max(0, Highest / Data.RoundLength - goalRoundClaimed) : 0;
        public long NextGoalBossStage => ((long)goalBossClaimed + 1) * Data.BossEvery;
        public long NextGoalRound => (long)goalRoundClaimed + 1;

        // Persist the complete reward transaction before changing live currency or claim markers.
        public bool ClaimGoalReward(int kind, Func<Progress, bool> save)
        {
            bool intro = kind == 0;
            if (save == null || (intro ? !CanClaimIntroGoalReward :
                kind == 1 ? PendingBossGoals == 0 : kind == 2 ? PendingRoundGoals == 0 : true)) return false;
            int reward = intro ? 10 : 1;
            if (Tickets > int.MaxValue - reward) return false;
            var progress = CaptureProgress();
            progress.tickets += reward;
            if (intro)
            {
                progress.goalIntroClaimed = true;
                progress.goalBossClaimed = Highest / Data.BossEvery;
                progress.goalRoundClaimed = Highest / Data.RoundLength;
            }
            else if (kind == 1) progress.goalBossClaimed++;
            else progress.goalRoundClaimed++;
            if (!save(progress)) return false;
            Tickets = progress.tickets;
            goalIntroClaimed = progress.goalIntroClaimed;
            goalBossClaimed = progress.goalBossClaimed;
            goalRoundClaimed = progress.goalRoundClaimed;
            return true;
        }

        private bool ValidGoalRewards(Progress saved) => saved.goalBossClaimed >= 0 && saved.goalRoundClaimed >= 0 &&
            saved.goalBossClaimed <= saved.highest / Data.BossEvery && saved.goalRoundClaimed <= saved.highest / Data.RoundLength &&
            (saved.goalIntroClaimed ? saved.completedGoals == GoalMask : saved.goalBossClaimed == 0 && saved.goalRoundClaimed == 0);
    }
}
