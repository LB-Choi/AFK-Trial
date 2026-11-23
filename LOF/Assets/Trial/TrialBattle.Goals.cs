using System;

namespace AFKTrial
{
    public sealed partial class TrialBattle
    {
        public const int GoalCount = 6;
        public const int GoalMask = (1 << GoalCount) - 1;
        private int completedGoals;

        // Stable bit positions are part of the save format. Earlier goals never gate later ones.
        public int CompletedGoals
        {
            get
            {
                int result = completedGoals;
                for (int hero = 0; hero < heroLevels.Length; hero++)
                {
                    if (heroLevels[hero] > 1) result |= 1;
                    for (int slot = 0; slot < 3; slot++)
                        if (skillLevels[hero, slot] > 1) result |= 1 << 3;
                }
                foreach (var item in inventory) if (item.Owner >= 0) result |= 1 << 1;
                if (Highest >= Data.BossEvery) result |= 1 << 2;
                if (clearedDungeonTiers[0] > 0 || clearedDungeonTiers[1] > 0) result |= 1 << 4;
                if (Highest >= Data.RoundLength) result |= 1 << 5;
                return result;
            }
        }
        public bool GoalComplete(int goal) => (uint)goal < GoalCount && (CompletedGoals & (1 << goal)) != 0;
        public int CurrentGoal
        {
            get { for (int i = 0; i < GoalCount; i++) if (!GoalComplete(i)) return i; return -1; }
        }
        public int CompletedGoalCount
        {
            get { int count = 0; for (int i = 0; i < GoalCount; i++) if (GoalComplete(i)) count++; return count; }
        }
        public int GoalTarget(int goal) => goal == 2 ? Data.BossEvery : goal == 5 ? Data.RoundLength : 1;
        public int GoalProgress(int goal) => GoalComplete(goal) ? GoalTarget(goal) :
            goal == 2 || goal == 5 ? Math.Min(Highest, GoalTarget(goal)) : 0;
    }
}
