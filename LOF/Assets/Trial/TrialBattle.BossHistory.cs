using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AFKTrial
{
    public sealed partial class TrialBattle
    {
        [Serializable]
        public sealed class BossAttempt
        {
            public int stage;
            public string bossId, bossName;
            public bool won;
            public float seconds, remainingHp, maxHp;
            public string[] party, setup;
            public BossAttempt Copy() => new BossAttempt {
                stage = stage, bossId = bossId, bossName = bossName, won = won, seconds = seconds,
                remainingHp = remainingHp, maxHp = maxHp,
                party = (string[])party.Clone(), setup = (string[])setup.Clone()
            };
        }

        private readonly List<BossAttempt> bossHistory = new List<BossAttempt>();
        private BossAttempt bossEntry;
        public BossAttempt CompletedBossAttempt { get; private set; }
        public BossAttempt PreviousBossAttempt { get; private set; }
        public BossAttempt[] BossHistory => bossHistory.Select(item => item.Copy()).ToArray();

        private void BeginBossAttempt()
        {
            CompletedBossAttempt = PreviousBossAttempt = null;
            bossEntry = null;
            if (ActiveDungeon != null || ActiveBoss == null) return;
            bossEntry = new BossAttempt {
                stage = ActiveStage, bossId = ActiveBoss.Stats.Id, bossName = ActiveBoss.Name,
                maxHp = ActiveBoss.MaxHp, party = new string[formation.Length], setup = new string[formation.Length]
            };
            for (int i = 0; i < formation.Length; i++)
            {
                var unit = Units[i];
                bossEntry.party[i] = unit.HeroIndex < 0 ? "없음" : unit.Name + " / 레벨 " + unit.Level;
                if (unit.HeroIndex < 0) { bossEntry.setup[i] = "빈 슬롯"; continue; }
                string gear = string.Join("·", Enum.GetValues(typeof(TrialData.GearSlot)).Cast<TrialData.GearSlot>()
                    .Select(slot => { var item = Equipped(unit.HeroIndex, slot); return item == null ? "없음" : item.Gear.Name + "+" + item.Enhancement; }));
                bossEntry.setup[i] = $"체력 {unit.MaxHp:0.##} / 공격 {unit.Attack:0.##} / 방어 {unit.Armor}\n" +
                    "스킬 " + string.Join("/", unit.Skills.Ranks) + " / 장비 " + gear;
            }
        }

        private void CompleteBossAttempt()
        {
            if (bossEntry == null) return;
            var result = bossEntry.Copy();
            result.won = Won;
            result.seconds = Mathf.Clamp(Elapsed, 0, Limit);
            result.remainingHp = Mathf.Clamp(ActiveBoss.Hp, 0, result.maxHp);
            PreviousBossAttempt = bossHistory.LastOrDefault(item => item.stage == result.stage && item.bossId == result.bossId)?.Copy();
            CompletedBossAttempt = result.Copy();
            // Keep two attempts per exact stage/boss and at most 64 attempts overall.
            int matches = bossHistory.Count(item => item.stage == result.stage && item.bossId == result.bossId);
            if (matches >= 2) bossHistory.RemoveAt(bossHistory.FindIndex(item => item.stage == result.stage && item.bossId == result.bossId));
            bossHistory.Add(result);
            if (bossHistory.Count > 64) bossHistory.RemoveAt(0);
            bossEntry = null;
        }

        private bool ValidBossHistory(BossAttempt[] history)
        {
            if (history == null) return true; // Saves created before this feature.
            if (history.Length > 64) return false;
            var counts = new Dictionary<(int, string), int>();
            foreach (var item in history)
            {
                if (item == null || item.stage <= 0 || item.stage % Data.BossEvery != 0 ||
                    string.IsNullOrEmpty(item.bossId) || string.IsNullOrEmpty(item.bossName) ||
                    !Data.Enemies.ContainsKey(item.bossId) || float.IsNaN(item.seconds) || float.IsInfinity(item.seconds) || item.seconds < 0 ||
                    float.IsNaN(item.maxHp) || float.IsInfinity(item.maxHp) || item.maxHp <= 0 ||
                    float.IsNaN(item.remainingHp) || item.remainingHp < 0 || item.remainingHp > item.maxHp ||
                    item.party == null || item.setup == null || item.party.Length != 3 || item.setup.Length != 3 ||
                    item.party.Any(text => text == null || text.Length > 256) || item.setup.Any(text => text == null || text.Length > 2048)) return false;
                var key = (item.stage, item.bossId);
                counts.TryGetValue(key, out int count);
                if (count >= 2) return false;
                counts[key] = count + 1;
            }
            return true;
        }
    }
}
