using CityBlocks.CloudCode;

namespace CityBlocks.Tests
{
    public class RulesTests
    {
        [Test]
        public void WelcomeBundle_IsCoinsOnly500()
        {
            Assert.That(Rules.WelcomeCoins, Is.EqualTo(500));
        }

        [Test]
        public void PlanMigration_NullOrEmpty_GrantsNothing()
        {
            Assert.That(Rules.PlanMigration(null), Is.Empty);
            Assert.That(Rules.PlanMigration(new Dictionary<string, int>()), Is.Empty);
        }

        [Test]
        public void PlanMigration_CapsEachTypeAndSkipsInvalid()
        {
            var plan = Rules.PlanMigration(new Dictionary<string, int>
            {
                { "delete", 3 },
                { "lookAhead", 500 },
                { "levelUp", 0 },
                { "extraTurns", -4 },
                { "cheatTurbo", 9 },
            });
            Assert.That(plan.Count, Is.EqualTo(2));
            Assert.That(plan["delete"], Is.EqualTo(3));
            Assert.That(plan["lookAhead"], Is.EqualTo(Rules.MaxMigratedPerPowerUp));
        }

        [Test]
        public void EveryPowerUpMapsToACurrency()
        {
            CollectionAssert.AreEquivalent(new[] { "PU_DELETE", "PU_LOOKAHEAD", "PU_LEVELUP", "PU_EXTRATURNS" }, EconomyIds.PowerUpCurrencies.Values);
        }
    }
}
