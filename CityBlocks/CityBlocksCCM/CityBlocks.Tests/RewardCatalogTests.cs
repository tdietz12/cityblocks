using System.IO;
using CityBlocks.CloudCode;

namespace CityBlocks.Tests
{
    public class RewardCatalogTests
    {
        private static string Json(string levelReward = "{}", string dailyReward = "{}", bool firstOnly = true)
        {
            return "{\"schemaVersion\":1,\"firstWinOnly\":" + (firstOnly ? "true" : "false") +
                ",\"levels\":[{\"levelNumber\":1,\"rewards\":" + levelReward + "}],\"daily\":[" +
                string.Join(",", Enumerable.Repeat(dailyReward, 7)) + "]}";
        }

        [Test]
        public void EmbeddedCatalog_LoadsAuthoredData()
        {
            Assert.That(RewardCatalog.Deployed.LevelCount, Is.GreaterThan(0));
            Assert.That(RewardCatalog.Deployed.DailyCount, Is.EqualTo(7));
            Assert.That(new RewardCatalogModule().GetRewardCatalogStatus().FirstWinOnly, Is.True);
        }

        [Test]
        public void MixedRewards_MapToCorrectEconomyIds()
        {
            var catalog = RewardCatalog.Parse(Json("{\"coins\":20,\"deleteUses\":1,\"levelUpUses\":2,\"lookAheadUses\":3,\"extraTurnsUses\":4}"));
            var grants = catalog.ForLevel(1).Grants();
            Assert.That(grants["COINS"], Is.EqualTo(20));
            Assert.That(grants["PU_DELETE"], Is.EqualTo(1));
            Assert.That(grants["PU_LEVELUP"], Is.EqualTo(2));
            Assert.That(grants["PU_LOOKAHEAD"], Is.EqualTo(3));
            Assert.That(grants["PU_EXTRATURNS"], Is.EqualTo(4));
        }

        [Test]
        public void DailyCoinAndPowerUpRewards_AreSupported()
        {
            var catalog = RewardCatalog.Parse(Json(dailyReward: "{\"coins\":100,\"deleteUses\":2}"));
            Assert.That(catalog.ForStreakDay(7).Grants().Count, Is.EqualTo(2));
        }

        [Test]
        public void ZeroRewards_ProduceNoGrant()
        {
            Assert.That(RewardCatalog.Parse(Json()).ForLevel(1).Grants(), Is.Empty);
        }

        [Test]
        public void InvalidRewardsOrReplayPolicy_AreRejected()
        {
            Assert.Throws<InvalidDataException>(() => RewardCatalog.Parse(Json("{\"coins\":-1}")));
            Assert.Throws<InvalidDataException>(() => RewardCatalog.Parse(Json(dailyReward: "{\"lookAheadUses\":-1}")));
            Assert.Throws<InvalidDataException>(() => RewardCatalog.Parse(Json(firstOnly: false)));
        }

        [Test]
        public void DuplicateLevelOrWrongDayCount_AreRejected()
        {
            string input = Json();
            Assert.Throws<InvalidDataException>(() => RewardCatalog.Parse(input.Replace("\"levels\":[{\"levelNumber\":1,\"rewards\":{}}]",
                "\"levels\":[{\"levelNumber\":1,\"rewards\":{}},{\"levelNumber\":1,\"rewards\":{}}]")));
            Assert.Throws<InvalidDataException>(() => RewardCatalog.Parse(input.Replace("\"daily\":[{},{},{},{},{},{},{}]", "\"daily\":[{}]")));
        }

        [Test]
        public void UnknownLevelAndInvalidDay_AreRejected()
        {
            var catalog = RewardCatalog.Parse(Json());
            Assert.Throws<ArgumentOutOfRangeException>(() => catalog.ForLevel(2));
            Assert.Throws<ArgumentOutOfRangeException>(() => catalog.ForStreakDay(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => catalog.ForStreakDay(8));
        }

        [Test]
        public void LookupReturnsCopy_NotMutableServerConfiguration()
        {
            var catalog = RewardCatalog.Parse(Json("{\"coins\":20}"));
            catalog.ForLevel(1).Coins = 999;
            Assert.That(catalog.ForLevel(1).Coins, Is.EqualTo(20));
        }
    }
}
