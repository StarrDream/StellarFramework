using NUnit.Framework;
using StellarFramework.Examples;

namespace StellarFramework.Tests.FrameworkValidation
{
    public sealed class FrameworkValidationReportTests
    {
        [Test]
        public void AddEntryTracksStatusCounts()
        {
            FrameworkValidationReport report = new FrameworkValidationReport();

            report.Add("ResKit", FrameworkValidationStatus.Passed, "Resources loaded.");
            report.Add("UIKit", FrameworkValidationStatus.Warning, "Stress test not run.");
            report.Add("HotUpdateKit", FrameworkValidationStatus.Failed, "Missing dll.bytes.");

            Assert.That(report.Count(FrameworkValidationStatus.Passed), Is.EqualTo(1));
            Assert.That(report.Count(FrameworkValidationStatus.Warning), Is.EqualTo(1));
            Assert.That(report.Count(FrameworkValidationStatus.Failed), Is.EqualTo(1));
            Assert.That(report.HasFailures, Is.True);
        }

        [Test]
        public void ClearRemovesEntriesAndFailureState()
        {
            FrameworkValidationReport report = new FrameworkValidationReport();
            report.Add("HotUpdateKit", FrameworkValidationStatus.Failed, "Missing dll.bytes.");

            report.Clear();

            Assert.That(report.Entries, Is.Empty);
            Assert.That(report.HasFailures, Is.False);
        }

        [Test]
        public void SummaryIncludesCountsAndMessages()
        {
            FrameworkValidationReport report = new FrameworkValidationReport();
            report.Add("ResKit", FrameworkValidationStatus.Passed, "Settings valid.");
            report.Add("UIKit", FrameworkValidationStatus.Warning, "Run stress test on device.");

            string summary = report.ToSummaryString();

            Assert.That(summary, Does.Contain("Passed=1"));
            Assert.That(summary, Does.Contain("Warning=1"));
            Assert.That(summary, Does.Contain("ResKit"));
            Assert.That(summary, Does.Contain("Run stress test on device."));
        }
    }
}
