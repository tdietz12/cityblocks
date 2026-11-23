using System.Globalization;
using CityBlocks.CloudCode;
using Microsoft.Extensions.Logging.Abstractions;

namespace CityBlocks.Tests
{
    public class PlayerModuleTests
    {
        [Test]
        public void GetServerTime_ReturnsCurrentUtcInRoundTripFormat()
        {
            var module = new PlayerModule(null!, NullLogger<PlayerModule>.Instance);
            DateTime before = DateTime.UtcNow;
            string value = module.GetServerTime();
            DateTime after = DateTime.UtcNow;

            Assert.That(value, Does.EndWith("Z"));
            DateTime parsed = DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            Assert.That(parsed.Kind, Is.EqualTo(DateTimeKind.Utc));
            Assert.That(parsed, Is.InRange(before, after));
        }

        [Test]
        public void FormatUtc_ConvertsLocalTimesToUtc()
        {
            var local = new DateTime(2026, 1, 31, 12, 0, 0, DateTimeKind.Local);
            string value = PlayerModule.FormatUtc(local);
            Assert.That(DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind), Is.EqualTo(local.ToUniversalTime()));
        }
    }
}
