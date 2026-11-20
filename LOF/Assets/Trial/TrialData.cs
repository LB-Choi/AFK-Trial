using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace AFKTrial
{
    public sealed class TrialData
    {
        public sealed class Skill
        {
            public string Name, Description;
            public int BaseGoldCost, Interval;
            public SkillEffect Effect;
        }
        public readonly Skill[,] Skills;
        public readonly bool StartWithAllHeroes;
        public readonly int StartingTickets, FirstWinTickets, FirstBossTickets;
        public readonly Dictionary<int, int> FirstClearRecruits = new Dictionary<int, int>();
        public readonly int IdleCapHours, IdleEfficiencyPercent, IdleReferenceClearSeconds;

        public enum DungeonKind { GoldVault, ExperienceArchive }
        public const int DungeonTierCount = 3;
        public readonly DungeonTier[,] Dungeons = new DungeonTier[2, DungeonTierCount];

        public sealed class DungeonTier
        {
            private static readonly string[] DifficultyNames = { "Easy", "Normal", "Hard" };
            public readonly DungeonKind Kind;
            public readonly int Index, Gold, Experience;
            public readonly float Power, Seconds;
            public string Name => Kind == DungeonKind.GoldVault ? "Gold Vault" : "Experience Archive";
            public string Difficulty => DifficultyNames[Index];

            internal DungeonTier(CsvRow row)
            {
                if (!Enum.TryParse(row.Text("kind"), out DungeonKind kind) || !Enum.IsDefined(typeof(DungeonKind), kind))
                    throw row.Error("unknown dungeon kind");
                Kind = kind;
                Index = row.Integer("tier", 1) - 1;
                if (Index >= DungeonTierCount) throw row.Error("tier must be 1 to 3");
                Power = row.Number("power", true);
                Seconds = row.Number("seconds", true);
                int reward = row.Integer("reward", 1);
                if (kind == DungeonKind.GoldVault) Gold = reward;
                else Experience = reward;
            }
        }

        public enum GearSlot { Weapon, Armor, Necklace }
        public enum GearRarity { Common, Uncommon, Rare }
        public sealed class Gear
        {
            public string Name;
            public GearSlot Slot;
            public GearRarity Rarity;
            public int Bonus, SellGold, Step;
        }
        public readonly List<Gear> StarterGear = new List<Gear>();

        public enum Role { None, Attack, Defense, Support }

        public sealed class Fighter
        {
            public string Id, Name, Portrait, Animation;
            public Role Role;
            public int BaseArmor, ArmorPerLevel;
            public int SkillEvery;
            public float Hp, Attack, HpPerLevel, AttackPerLevel, Speed, Range, Interval, ImpactSeconds;
            public float ContactDistance, AttackDuration, HitHeight;
        }

        public readonly Dictionary<string, string> RigResources = new Dictionary<string, string>();
        public readonly List<Fighter> Heroes = new List<Fighter>();
        public readonly Dictionary<string, Fighter> Enemies = new Dictionary<string, Fighter>();
        public readonly Fighter RoundBoss;
        public readonly int[] StartingFormation = { -1, -1, -1 };
        public readonly int EnhanceGold, NormalDrop, BossDrop;
        public readonly int RoundLength, BossEvery, BossSlot, PracticeGold, BaseGold, GoldPerStage,
            PracticeXp, BaseXp, XpPerStage, FirstWinMultiplier, UpgradeGold, UpgradeXp;
        public readonly float NormalSeconds, BossSeconds, PracticePower, BasePower, PowerPerStage,
            SpawnDistance, LaneSpacing, RestartDelay;
        public readonly string HitTexture;
        public readonly int HitColumns, HitRows, HitFrames;
        public readonly float HitFps, LandscapeSize, PortraitSize;
        public readonly Vector2 HitSparkPivot;

        public static TrialData Load() => new TrialData(Path.Combine(Application.streamingAssetsPath, "Data"));

        public TrialData(string directory)
        {
            foreach (var row in CsvRow.Read(directory, "Dungeons"))
            {
                var tier = new DungeonTier(row);
                if (Dungeons[(int)tier.Kind, tier.Index] != null) throw row.Error("duplicate dungeon tier");
                Dungeons[(int)tier.Kind, tier.Index] = tier;
            }
            foreach (var tier in Dungeons)
                if (tier == null) throw new InvalidDataException("Dungeons.csv requires three tiers for each dungeon.");
            var idle = CsvRow.Single(directory, "IdleRewards");
            IdleCapHours = idle.Integer("capHours", 1);
            IdleEfficiencyPercent = idle.Integer("efficiencyPercent", 1);
            if (IdleEfficiencyPercent > 100) throw idle.Error("efficiencyPercent must be at most 100");
            IdleReferenceClearSeconds = idle.Integer("referenceClearSeconds", 1);
            foreach (var row in CsvRow.Read(directory, "Equipment"))
            {
                if (!Enum.TryParse(row.Text("slot"), out GearSlot slot) || !Enum.IsDefined(typeof(GearSlot), slot))
                    throw row.Error("invalid equipment slot");
                if (!Enum.TryParse(row.Optional("rarity", "Common"), true, out GearRarity rarity) || !Enum.IsDefined(typeof(GearRarity), rarity))
                    throw row.Error("invalid equipment rarity");
                StarterGear.Add(new Gear { Name = row.Text("name"), Slot = slot, Rarity = rarity,
                    Bonus = row.Integer("bonus", 1), Step = row.Integer("bonusPerEnhancement", 1, 1), SellGold = row.Integer("sellGold", 1) });
            }
            var impacts = new Dictionary<string, float>();
            foreach (var row in CsvRow.Read(directory, "Animations"))
            {
                string id = row.Text("id");
                if (impacts.ContainsKey(id)) throw row.Error("duplicate animation " + id);
                RigResources.Add(id, row.Text("rigResource"));
                impacts.Add(id, row.Number("impactSeconds", true));
            }
            var ids = new HashSet<string>();
            foreach (var row in CsvRow.Read(directory, "Heroes"))
            {
                var hero = ReadFighter(row, row.Text("animation", true), impacts);
                if (!ids.Add(hero.Id)) throw row.Error("duplicate id " + hero.Id);
                if (!Enum.TryParse(row.Optional("role", "None"), true, out hero.Role) || !Enum.IsDefined(typeof(Role), hero.Role))
                    throw row.Error("invalid hero role");
                hero.Portrait = row.Text("portrait");
                hero.HpPerLevel = row.Number("hpPerLevel");
                hero.AttackPerLevel = row.Number("attackPerLevel");
                hero.BaseArmor = row.Integer("baseArmor", fallback: 0);
                hero.ArmorPerLevel = row.Integer("armorPerLevel", fallback: 0);
                int slot = row.Integer("startingSlot");
                int recruitStage = row.Integer("recruitStage", fallback: 0);
                if (recruitStage > 0)
                {
                    if (FirstClearRecruits.ContainsKey(recruitStage)) throw row.Error("duplicate recruitStage");
                    FirstClearRecruits.Add(recruitStage, Heroes.Count);
                }
                if (slot > StartingFormation.Length) throw row.Error("startingSlot must be 0 to 3");
                if (slot > 0)
                {
                    if (StartingFormation[slot - 1] >= 0) throw row.Error("duplicate startingSlot " + slot);
                    StartingFormation[slot - 1] = Heroes.Count;
                }
                Heroes.Add(hero);
            }
            if (Array.IndexOf(StartingFormation, -1) >= 0)
                throw new InvalidDataException("Heroes.csv requires one hero for each startingSlot 1, 2 and 3.");
            var summon = CsvRow.Single(directory, "Summoning");
            int all = summon.Integer("startWithAllHeroes");
            if (all > 1) throw summon.Error("startWithAllHeroes must be 0 or 1");
            StartWithAllHeroes = all == 1;
            StartingTickets = summon.Integer("startingTickets");
            FirstWinTickets = summon.Integer("firstWinTickets");
            FirstBossTickets = summon.Integer("firstBossTickets");
            Skills = new Skill[Heroes.Count, 3];
            foreach (var row in CsvRow.Read(directory, "Skills"))
            {
                string id = row.Text("heroId");
                int hero = Heroes.FindIndex(h => h.Id == id);
                int slot = row.Integer("slot", 1) - 1;
                if (hero < 0 || slot >= 3 || Skills[hero, slot] != null) throw row.Error("invalid or duplicate hero/slot");
                Skills[hero, slot] = new Skill { Name = row.Text("name"), Description = row.Text("description", true),
                    BaseGoldCost = row.Integer("baseGoldCost", 1), Interval = row.Integer("interval", 1, 1) };
                if (!Enum.TryParse(row.Optional("effect", "None"), out Skills[hero, slot].Effect) ||
                    !Enum.IsDefined(typeof(SkillEffect), Skills[hero, slot].Effect)) throw row.Error("invalid skill effect");
            }
            foreach (var skill in Skills)
                if (skill == null) throw new InvalidDataException("Skills.csv requires three slots per hero.");
            foreach (var row in CsvRow.Read(directory, "Enemies"))
            {
                var enemy = ReadFighter(row, row.Optional("animation", ""), impacts);
                enemy.SkillEvery = row.Integer("skillEvery", fallback: 0);
                if (row.Optional("hitHeight", "") != "") enemy.HitHeight = row.Number("hitHeight", true);
                if (row.Optional("contactDistance", "") != "")
                {
                    enemy.ContactDistance = row.Number("contactDistance", true);
                    if (enemy.ContactDistance > enemy.Range) throw row.Error("contactDistance exceeds attack range");
                    if (enemy.Animation.Length == 0) throw row.Error("contactDistance requires an enemy animation");
                    enemy.AttackDuration = TrialCharacterRig.AttackDurationAtImpact(RigResources[enemy.Animation], enemy.ImpactSeconds);
                }
                if (Enemies.ContainsKey(enemy.Id)) throw row.Error("duplicate id " + enemy.Id);
                Enemies.Add(enemy.Id, enemy);
            }
            if (!Enemies.ContainsKey("normal") || !Enemies.ContainsKey("boss"))
                throw new InvalidDataException("Enemies.csv requires normal and boss.");
            var p = CsvRow.Single(directory, "Progression");
            EnhanceGold = p.Integer("enhanceGold", 1, 20);
            NormalDrop = p.Integer("normalGearPercent", 0, 5);
            BossDrop = p.Integer("bossGearPercent", 0, 10);
            if (NormalDrop > 100 || BossDrop > 100) throw p.Error("invalid gear drop percentage");
            RoundLength = p.Integer("roundLength", 1);
            string roundBossId = p.Optional("roundBoss", "boss");
            if (!Enemies.TryGetValue(roundBossId, out RoundBoss)) throw p.Error("unknown roundBoss " + roundBossId);
            BossEvery = p.Integer("bossEvery", 1);
            BossSlot = p.Integer("bossSlot", 1) - 1;
            if (BossSlot >= StartingFormation.Length) throw p.Error("bossSlot must be 1 to 3");
            NormalSeconds = p.Number("normalSeconds", true);
            BossSeconds = p.Number("bossSeconds", true);
            PracticePower = p.Number("practicePower", true);
            BasePower = p.Number("basePower", true);
            PowerPerStage = p.Number("powerPerStage");
            PracticeGold = p.Integer("practiceGold");
            BaseGold = p.Integer("baseGold");
            GoldPerStage = p.Integer("goldPerStage");
            PracticeXp = p.Integer("practiceXp");
            BaseXp = p.Integer("baseXp");
            XpPerStage = p.Integer("xpPerStage");
            FirstWinMultiplier = p.Integer("firstWinMultiplier", 1);
            UpgradeGold = p.Integer("upgradeGoldPerLevel");
            UpgradeXp = p.Integer("upgradeXpPerLevel");
            SpawnDistance = p.Number("spawnDistance", true);
            LaneSpacing = p.Number("laneSpacing");
            RestartDelay = p.Number("restartDelay");
            var fx = CsvRow.Single(directory, "HitFlash");
            HitTexture = fx.Text("texture");
            HitColumns = fx.Integer("columns", 1);
            HitRows = fx.Integer("rows", 1);
            HitFrames = fx.Integer("frames", 1);
            if (HitFrames > (long)HitColumns * HitRows) throw fx.Error("frames exceeds the sheet grid");
            HitFps = fx.Number("fps", true);
            LandscapeSize = fx.Number("landscapeSize", true);
            PortraitSize = fx.Number("portraitSize", true);
            HitSparkPivot = new Vector2(fx.Optional("sparkX", "") == "" ? .5f : fx.Number("sparkX"),
                fx.Optional("sparkY", "") == "" ? .5f : fx.Number("sparkY"));
            if (HitSparkPivot.x > 1 || HitSparkPivot.y > 1) throw fx.Error("spark pivot must lie inside its atlas cell");
        }

        private static Fighter ReadFighter(CsvRow row, string animation, Dictionary<string, float> impacts)
        {
            var fighter = new Fighter {
                Id = row.Text("id"), Name = row.Text("name"), Hp = row.Number("hp", true),
                Attack = row.Number("attack"), Speed = row.Number("moveSpeed"), Range = row.Number("range"),
                Interval = row.Number("attackInterval", true), Animation = animation
            };
            if (animation.Length > 0 && !impacts.TryGetValue(animation, out fighter.ImpactSeconds))
                throw row.Error("unknown animation " + animation);
            return fighter;
        }
    }

    internal sealed class CsvRow
    {
        private readonly string location;
        private readonly Dictionary<string, string> fields = new Dictionary<string, string>();
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        private CsvRow(string file, int record, string[] names, string[] values)
        {
            location = file + ", record " + record;
            if (names.Length != values.Length) throw Error("unexpected number of fields");
            for (int i = 0; i < names.Length; i++)
            {
                if (names[i].Length == 0 || fields.ContainsKey(names[i])) throw Error("invalid column " + names[i]);
                fields.Add(names[i], values[i]);
            }
        }

        public InvalidDataException Error(string message) => new InvalidDataException(location + ": " + message);
        public string Optional(string key, string fallback) => fields.TryGetValue(key, out var value) ? value : fallback;
        public string Text(string key, bool allowEmpty = false)
        {
            if (!fields.TryGetValue(key, out var value) || (!allowEmpty && value.Length == 0)) throw Error("missing " + key);
            return value;
        }
        public float Number(string key, bool positive = false)
        {
            if (!float.TryParse(Text(key), NumberStyles.Float, Invariant, out var n) ||
                float.IsNaN(n) || float.IsInfinity(n) || n < 0 || (positive && n == 0))
                throw Error("invalid " + key);
            return n;
        }
        public int Integer(string key, int min = 0, int? fallback = null)
        {
            if (fallback.HasValue && !fields.ContainsKey(key)) return fallback.Value;
            if (!int.TryParse(Text(key), NumberStyles.Integer, Invariant, out var n) || n < min) throw Error("invalid " + key);
            return n;
        }
        public static CsvRow Single(string directory, string name)
        {
            var rows = Read(directory, name);
            if (rows.Count != 1) throw new InvalidDataException(name + ".csv requires exactly one data row.");
            return rows[0];
        }
        public static List<CsvRow> Read(string directory, string name)
        {
            string path = Path.Combine(directory, name + ".csv");
            var rows = new List<CsvRow>();
            string[] header = null;
            foreach (var values in Records(File.ReadAllText(path, new UTF8Encoding(false, true)), path))
            {
                if (values.Length == 1 && values[0].Length == 0) continue;
                if (header == null) header = values;
                else rows.Add(new CsvRow(path, rows.Count + 2, header, values));
            }
            if (rows.Count == 0) throw new InvalidDataException(path + " has no data rows.");
            return rows;
        }
        // Excel CSV permits escaped quotes, commas and CR/LF inside quoted cells.
        private static IEnumerable<string[]> Records(string input, string path)
        {
            var cells = new List<string>();
            var cell = new StringBuilder();
            bool quoted = false, closed = false;
            for (int i = 0; i <= input.Length; i++)
            {
                bool eof = i == input.Length;
                char c = eof ? '\n' : input[i];
                if (quoted)
                {
                    if (eof) throw new InvalidDataException(path + ": unterminated quoted field");
                    if (c != '"') cell.Append(c);
                    else if (i + 1 < input.Length && input[i + 1] == '"') { cell.Append('"'); i++; }
                    else { quoted = false; closed = true; }
                }
                else if (c == ',' || c == '\r' || c == '\n')
                {
                    cells.Add(cell.ToString().Trim());
                    cell.Clear();
                    closed = false;
                    if (c == ',') continue;
                    if (c == '\r' && i + 1 < input.Length && input[i + 1] == '\n') i++;
                    yield return cells.ToArray();
                    cells.Clear();
                }
                else if (c == '"' && cell.Length == 0 && !closed) quoted = true;
                else if (closed || c == '"') throw new InvalidDataException(path + ": unexpected character around quote");
                else cell.Append(c);
            }
        }
    }
}
