using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace CityBlocks.CloudCode
{
    public sealed class RewardBundle
    {
        public long Coins { get; set; }
        public long DeleteUses { get; set; }
        public long LevelUpUses { get; set; }
        public long LookAheadUses { get; set; }
        public long ExtraTurnsUses { get; set; }

        public bool IsValid => Coins >= 0 && DeleteUses >= 0 && LevelUpUses >= 0 && LookAheadUses >= 0 && ExtraTurnsUses >= 0;

        public Dictionary<string, long> Grants()
        {
            var grants = new Dictionary<string, long>();
            if (Coins > 0) grants[EconomyIds.Coins] = Coins;
            if (DeleteUses > 0) grants[EconomyIds.PowerUpCurrencies["delete"]] = DeleteUses;
            if (LevelUpUses > 0) grants[EconomyIds.PowerUpCurrencies["levelUp"]] = LevelUpUses;
            if (LookAheadUses > 0) grants[EconomyIds.PowerUpCurrencies["lookAhead"]] = LookAheadUses;
            if (ExtraTurnsUses > 0) grants[EconomyIds.PowerUpCurrencies["extraTurns"]] = ExtraTurnsUses;
            return grants;
        }

        public RewardBundle Copy() => new RewardBundle { Coins = Coins, DeleteUses = DeleteUses, LevelUpUses = LevelUpUses,
            LookAheadUses = LookAheadUses, ExtraTurnsUses = ExtraTurnsUses };
    }

    public sealed class RewardCatalog
    {
        private sealed class LevelEntry
        {
            public int LevelNumber { get; set; }
            public RewardBundle? Rewards { get; set; }
        }
        private sealed class Document
        {
            public int SchemaVersion { get; set; }
            public bool FirstWinOnly { get; set; }
            public LevelEntry[]? Levels { get; set; }
            public RewardBundle[]? Daily { get; set; }
        }
        private readonly Dictionary<int, RewardBundle> levels;
        private readonly RewardBundle[] daily;
        private static readonly Lazy<RewardCatalog> deployed = new Lazy<RewardCatalog>(LoadEmbedded);

        public static RewardCatalog Deployed => deployed.Value;
        public int LevelCount => levels.Count;
        public int DailyCount => daily.Length;

        private RewardCatalog(Dictionary<int, RewardBundle> levels, RewardBundle[] daily)
        {
            this.levels = levels;
            this.daily = daily;
        }

        public static RewardCatalog Parse(string json)
        {
            var document = JsonSerializer.Deserialize<Document>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new InvalidDataException("Reward configuration is empty.");
            if (document.SchemaVersion != 1 || !document.FirstWinOnly)
                throw new InvalidDataException("Reward configuration must use schema 1 and first-win-only level rewards.");
            if (document.Levels == null || document.Levels.Length == 0)
                throw new InvalidDataException("No level rewards configured.");
            var levels = new Dictionary<int, RewardBundle>();
            foreach (var entry in document.Levels)
            {
                if (entry == null || entry.LevelNumber < 1 || entry.Rewards == null || !entry.Rewards.IsValid ||
                    !levels.TryAdd(entry.LevelNumber, entry.Rewards.Copy()))
                    throw new InvalidDataException("Invalid or duplicate level reward entry.");
            }
            if (document.Daily == null || document.Daily.Length != 7 || document.Daily.Any(r => r == null || !r.IsValid))
                throw new InvalidDataException("Daily schedule must contain exactly seven non-negative reward entries.");
            return new RewardCatalog(levels, document.Daily.Select(r => r.Copy()).ToArray());
        }

        public RewardBundle ForLevel(int levelNumber) => levels.TryGetValue(levelNumber, out var reward)
            ? reward.Copy() : throw new ArgumentOutOfRangeException(nameof(levelNumber), "Level is not in the deployed catalog.");

        public RewardBundle ForStreakDay(int day) => day >= 1 && day <= 7
            ? daily[day - 1].Copy() : throw new ArgumentOutOfRangeException(nameof(day), "Streak day must be 1 through 7.");

        private static RewardCatalog LoadEmbedded()
        {
            using Stream stream = typeof(RewardCatalog).Assembly.GetManifestResourceStream("CityBlocks.RewardConfiguration.json")
                ?? throw new InvalidDataException("Exported reward configuration is missing from the deployed module.");
            using var reader = new StreamReader(stream);
            return Parse(reader.ReadToEnd());
        }
    }
}
