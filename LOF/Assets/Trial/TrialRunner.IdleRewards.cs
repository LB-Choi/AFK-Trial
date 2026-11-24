using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace AFKTrial
{
    internal struct TrialIdleReward
    {
        public long AwaySeconds, CreditedSeconds;
        public int Stage, Gold, Experience;
        public TrialBattle.IdleEquipment Equipment;
        public string EquipmentSummary;
    }

    public sealed partial class TrialRunner
    {
        [Serializable]
        private sealed class SaveRecord
        {
            public int version;
            public long lastSeenUtc, pendingSeconds, equipmentSeed;
            public TrialBattle.Progress progress;
        }

        private const int SaveVersion = 1;
        private const int CheckpointSeconds = 30;
        private const long MaxUtcSeconds = 253402300799L;
        private FileStream progressLock;
        private string progressPath, victoryBackupPath, idleSaveError;
        private long lastSeenUtc, pendingSeconds, equipmentSeed;
        private double nextCheckpoint;
        private bool suspended;
        private TrialIdleReward PendingIdleReward { get; set; }
        private bool HasPendingIdleReward => PendingIdleReward.Gold > 0 || PendingIdleReward.Experience > 0 ||
            PendingIdleReward.Equipment?.Items.Count > 0 || PendingIdleReward.Equipment?.Converted > 0;
        private static long UtcNow => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        private TrialIdleReward EstimateIdleReward(long seconds)
        {
            var data = battle.Data;
            int stage = battle.Highest;
            long credited = Math.Min(seconds, (long)data.IdleCapHours * 3600);
            long gold = stage == 0 ? data.PracticeGold : data.BaseGold + (long)stage * data.GoldPerStage;
            long experience = stage == 0 ? data.PracticeXp : data.BaseXp + (long)stage * data.XpPerStage;
            int Amount(long perClear) => (int)Math.Min(int.MaxValue, (double)perClear * credited *
                data.IdleEfficiencyPercent / (100d * data.IdleReferenceClearSeconds));
            return new TrialIdleReward {
                AwaySeconds = seconds, CreditedSeconds = credited, Stage = stage,
                Gold = Amount(gold), Experience = Amount(experience)
            };
        }

        private static string ProfileFolder
        {
            get
            {
                string root = Application.persistentDataPath;
                var parent = Directory.GetParent(root);
                if (parent != null && Path.GetFileName(root) == Application.productName)
                {
                    string legacy = Path.Combine(parent.FullName, "AFK Trial - Test", "AFKTrial");
                    if (Directory.Exists(legacy)) return legacy;
                }
                return Path.Combine(root, "AFKTrial");
            }
        }

        private void OpenProgressSession()
        {
            string folder = ProfileFolder;
            OpenProgressSessionAt(folder);
        }

        private void OpenProgressSessionAt(string folder)
        {
            Directory.CreateDirectory(folder);
            progressPath = Path.Combine(folder, "progress.json");
            victoryBackupPath = Path.Combine(folder, "last-victory.json");
            // Keep two players using this profile from both confirming the same return.
            progressLock = new FileStream(Path.Combine(folder, "session.lock"), FileMode.OpenOrCreate,
                FileAccess.ReadWrite, FileShare.None);
            try
            {
                SaveRecord record = null;
                if (File.Exists(progressPath))
                {
                    try { record = ReadProgress(progressPath); }
                    catch (InvalidDataException) when (File.Exists(victoryBackupPath))
                    {
                        record = ReadProgress(victoryBackupPath);
                        Debug.LogWarning("Recovered AFK Trial progress from the last victory.");
                    }
                }
                else if (File.Exists(victoryBackupPath)) record = ReadProgress(victoryBackupPath);

                long now = UtcNow;
                lastSeenUtc = record?.lastSeenUtc ?? now;
                pendingSeconds = record?.pendingSeconds ?? 0;
                equipmentSeed = record?.equipmentSeed ?? 0;
                suspended = false;
                CountTimeAway(now);
                WriteProgress(battle.CaptureProgress(), pendingSeconds);
                nextCheckpoint = Time.realtimeSinceStartupAsDouble + CheckpointSeconds;
            }
            catch
            {
                progressLock.Dispose();
                progressLock = null;
                throw;
            }
        }

        private SaveRecord ReadProgress(string path)
        {
            string json = File.ReadAllText(path);
            SaveRecord record;
            try { record = JsonUtility.FromJson<SaveRecord>(json); }
            catch (ArgumentException error) { throw new InvalidDataException("저장 파일 형식이 올바르지 않습니다.", error); }
            if (record != null && record.version > SaveVersion)
                throw new NotSupportedException("더 최신 버전에서 만든 저장 파일입니다.");
            if (record == null || record.version != SaveVersion || record.lastSeenUtc <= 0 ||
                record.lastSeenUtc > MaxUtcSeconds || record.pendingSeconds < 0 || record.pendingSeconds > MaxUtcSeconds ||
                !battle.RestoreProgress(record.progress))
                throw new InvalidDataException("저장된 진행을 불러오지 못했습니다.");
            return record;
        }

        private void CountTimeAway(long now)
        {
            if (equipmentSeed == 0) equipmentSeed = lastSeenUtc;
            pendingSeconds = Math.Min(MaxUtcSeconds, pendingSeconds + Math.Max(0L, now - lastSeenUtc));
            lastSeenUtc = Math.Max(lastSeenUtc, now);
            var reward = EstimateIdleReward(pendingSeconds);
            var gear = battle.EstimateIdleEquipment(reward.CreditedSeconds, unchecked((int)equipmentSeed));
            reward.Equipment = gear;
            reward.EquipmentSummary = DescribeIdleEquipment(gear);
            reward.Gold = (int)Math.Min((long)reward.Gold + gear.ConversionGold, int.MaxValue - battle.Gold);
            reward.Experience = Math.Min(reward.Experience, int.MaxValue - battle.Experience);
            PendingIdleReward = reward;
            // Keep short visits until they accumulate enough to produce a reward.
        }

        private void WriteProgress(TrialBattle.Progress progress, long unclaimedSeconds, bool victory = false)
        {
            var record = new SaveRecord { version = SaveVersion, lastSeenUtc = lastSeenUtc,
                pendingSeconds = unclaimedSeconds, equipmentSeed = unclaimedSeconds > 0 ? equipmentSeed : 0, progress = progress };
            byte[] bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(record));
            WriteAtomically(progressPath, bytes);
            if (victory) WriteAtomically(victoryBackupPath, bytes);
        }

        private static void WriteAtomically(string path, byte[] contents)
        {
            string tempPath = path + ".tmp";
            using (var file = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                file.Write(contents, 0, contents.Length);
                file.Flush(true);
            }
            if (File.Exists(path)) File.Replace(tempPath, path, null);
            else File.Move(tempPath, path);
        }

        private bool TrySaveProgress(TrialBattle.Progress progress, long unclaimedSeconds, bool victory = false)
        {
            try
            {
                WriteProgress(progress, unclaimedSeconds, victory);
                idleSaveError = null;
                return true;
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                idleSaveError = "진행을 저장하지 못했습니다. 저장 공간을 확인한 뒤 다시 시도해 주세요.";
                Debug.LogWarning(idleSaveError + " " + error.Message);
                return false;
            }
        }

        private void CheckpointProgress(bool victory = false)
        {
            if (!suspended) lastSeenUtc = Math.Max(lastSeenUtc, UtcNow);
            nextCheckpoint = Time.realtimeSinceStartupAsDouble + CheckpointSeconds;
            TrySaveProgress(battle.CaptureProgress(), pendingSeconds, victory);
        }

        private bool SaveMenuPurchase(TrialBattle.Progress progress)
        {
            if (progressLock == null || suspended || HasPendingIdleReward) return false;
            lastSeenUtc = Math.Max(lastSeenUtc, UtcNow);
            return TrySaveProgress(progress, pendingSeconds);
        }

        private bool TryPurchaseHeroLevel(int hero) => battle.UpgradeHero(hero, SaveMenuPurchase);
        private bool TryTrainSkill(int hero, int slot) => battle.Train(hero, slot, SaveMenuPurchase);

        private bool TrySummon(int count)
        {
            var pulls = battle.Summon(count, SaveMenuPurchase);
            if (pulls == null) return false;
            BeginSummonCeremony(pulls);
            return true;
        }

        private void ConfirmIdleReward()
        {
            if (progressLock == null || suspended || !HasPendingIdleReward) return;
            var reward = PendingIdleReward;
            lastSeenUtc = Math.Max(lastSeenUtc, UtcNow);
            if (!battle.CollectIdle(reward.Gold, reward.Experience, reward.Equipment, progress => TrySaveProgress(progress, 0))) return;
            PlayBattleSound(TrialBattle.SoundCue.Reward);
            pendingSeconds = equipmentSeed = 0;
            PendingIdleReward = default;
        }

        private void OnApplicationQuit() => CloseProgressSession();
        private void OnDisable()
        {
            ReleaseAudio();
            ReleaseSkillEffects();
            CloseProgressSession();
        }

        private void CloseProgressSession()
        {
            if (progressLock == null) return;
            try { CheckpointProgress(); }
            finally { progressLock.Dispose(); progressLock = null; }
        }
    }
}
