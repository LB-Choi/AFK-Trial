using System;
using System.Linq;

namespace AFKTrial
{
    public sealed partial class TrialBattle
    {
        [Serializable]
        public sealed class SummonReceipt
        {
            public string heroId;
            public int level;
            public bool isNew;
        }
        private SummonReceipt[] pendingSummons;
        public int SummonDuplicateStreak => duplicateStreak;
        public int MissingSummonHeroes => heroLevels.Count(level => level == 0);
        public Pull[] PendingSummons => pendingSummons == null ? null : Array.ConvertAll(pendingSummons,
            p => new Pull { Hero = Data.Heroes.FindIndex(h => h.Id == p.heroId), Level = p.level, IsNew = p.isNew });
        private static SummonReceipt[] CopyReceipts(SummonReceipt[] records) => records == null || records.Length == 0 ? null :
            Array.ConvertAll(records, p => new SummonReceipt { heroId = p.heroId, level = p.level, isNew = p.isNew });

        // The same CSV percentages drive both the draw and the information panel.
        public int RollSummonHero(float roll)
        {
            double target = Math.Clamp(roll, 0, 1) * Data.SummonRates.Sum(rate => (double)rate);
            double cumulative = 0;
            for (int i = 0; i < Data.SummonRates.Length; i++)
            {
                cumulative += Data.SummonRates[i];
                if (target < cumulative) return i;
            }
            return Data.SummonRates.Length - 1;
        }

        public bool ConfirmSummons(Func<Progress, bool> save = null)
        {
            if (pendingSummons == null) return false;
            var progress = CaptureProgress();
            progress.pendingSummons = null;
            if (save != null && !save(progress)) return false;
            pendingSummons = null;
            return true;
        }

        private bool ValidSummonReceipts(SummonReceipt[] records, HeroProgress[] roster)
        {
            if (records == null || records.Length == 0) return true;
            if (records.Length != 1 && records.Length != 10) return false;
            var prior = new System.Collections.Generic.Dictionary<string, int>();
            foreach (var item in records)
            {
                if (item == null || item.level < 1) return false;
                int hero = Data.Heroes.FindIndex(h => h.Id == item.heroId);
                if (hero < 0 || roster[hero] == null || item.level > roster[hero].level ||
                    (item.isNew && item.level != 1)) return false;
                if (prior.TryGetValue(item.heroId, out int level) &&
                    (item.isNew || item.level != Math.Min((long)level + 1, int.MaxValue))) return false;
                prior[item.heroId] = item.level;
            }
            return true;
        }
    }
}
