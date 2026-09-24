using System;
using NUnit.Framework;
using StellarFramework.Editor.HotUpdatePublisher;

namespace StellarFramework.Tests.FrameworkValidation.Policies.HotUpdatePublisher
{
    public sealed class HotUpdateVersionPolicyTests
    {
        private readonly DailyHotUpdateVersionPolicy _policy = new DailyHotUpdateVersionPolicy();

        [Test]
        public void FirstVersionForUtcDateStartsAt001()
        {
            string version = _policy.CreateNextVersion(
                new DateTime(2026, 9, 24, 15, 30, 0, DateTimeKind.Utc), Array.Empty<string>());

            Assert.That(version, Is.EqualTo("2026.09.24.001"));
        }

        [Test]
        public void NextVersionUsesHighestValidSequenceForSameDate()
        {
            string version = _policy.CreateNextVersion(
                new DateTime(2026, 9, 24, 0, 0, 0, DateTimeKind.Utc),
                new[] { "2026.09.24.002", "2026.09.24.017", "2026.09.23.999", "not-a-version", "2026.09.24.9999" });

            Assert.That(version, Is.EqualTo("2026.09.24.018"));
        }

        [Test]
        public void DailySequenceExhaustionFailsExplicitly()
        {
            Assert.Throws<InvalidOperationException>(() => _policy.CreateNextVersion(
                new DateTime(2026, 9, 24, 0, 0, 0, DateTimeKind.Utc), new[] { "2026.09.24.999" }));
        }

        [Test]
        public void NonUtcTimestampIsRejected()
        {
            Assert.Throws<ArgumentException>(() => _policy.CreateNextVersion(
                new DateTime(2026, 9, 24, 0, 0, 0, DateTimeKind.Unspecified), Array.Empty<string>()));
        }
    }
}
