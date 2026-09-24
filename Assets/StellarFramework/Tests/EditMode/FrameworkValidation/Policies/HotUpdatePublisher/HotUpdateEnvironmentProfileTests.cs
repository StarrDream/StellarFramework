using System;
using NUnit.Framework;
using StellarFramework.Editor.HotUpdatePublisher;
using UnityEngine;

namespace StellarFramework.Tests.Policies.HotUpdatePublisher
{
    public sealed class HotUpdateEnvironmentProfileTests
    {
        [TestCase(HotUpdateEnvironmentKind.Development)]
        [TestCase(HotUpdateEnvironmentKind.Staging)]
        [TestCase(HotUpdateEnvironmentKind.Production)]
        public void DefaultProfileHasStableEnvironmentIdAndNonSecretDefaults(HotUpdateEnvironmentKind environment)
        {
            HotUpdateEnvironmentProfile profile = HotUpdateEnvironmentProfile.CreateDefault(environment);

            Assert.That(profile.EnvironmentId, Is.EqualTo(environment.ToString()));
            Assert.That(profile.PublishTarget, Is.EqualTo("LocalFolder"));
            Assert.That(profile.LocalFolderRoot, Is.Empty);
            Assert.That(profile.CredentialProfileName, Is.Empty);
            Assert.That(profile.MainHostServer, Is.Empty);
        }

        [Test]
        public void Validate_AcceptsCompleteHttpsProfile()
        {
            HotUpdateEnvironmentProfile profile = CreateValidProfile(HotUpdateEnvironmentKind.Staging);

            HotUpdateEnvironmentProfileValidationResult result = profile.Validate();

            Assert.That(result.IsValid, Is.True, string.Join(Environment.NewLine, result.Errors));
        }

        [Test]
        public void Validate_ProductionRequiresHttps()
        {
            HotUpdateEnvironmentProfile profile = CreateValidProfile(HotUpdateEnvironmentKind.Production);
            profile.MainHostServer = "http://cdn.example.com";

            HotUpdateEnvironmentProfileValidationResult result = profile.Validate();

            Assert.That(result.IsValid, Is.False);
            Assert.That(string.Join("\n", result.Errors), Does.Contain("must use HTTPS"));
        }

        [TestCase("https://user:secret@cdn.example.com")]
        [TestCase("file:///local/path")]
        [TestCase("https://cdn.example.com/path?token=secret")]
        public void Validate_RejectsUnsafeHostUrls(string host)
        {
            HotUpdateEnvironmentProfile profile = CreateValidProfile(HotUpdateEnvironmentKind.Staging);
            profile.MainHostServer = host;

            HotUpdateEnvironmentProfileValidationResult result = profile.Validate();

            Assert.That(result.IsValid, Is.False);
        }

        [TestCase("../outside")]
        [TestCase("/absolute/root")]
        [TestCase("content/%2e%2e/secrets")]
        public void Validate_RejectsUnsafeRemoteRoots(string remoteRoot)
        {
            HotUpdateEnvironmentProfile profile = CreateValidProfile(HotUpdateEnvironmentKind.Development);
            profile.RemoteRoot = remoteRoot;

            HotUpdateEnvironmentProfileValidationResult result = profile.Validate();

            Assert.That(result.IsValid, Is.False);
            Assert.That(string.Join("\n", result.Errors), Does.Contain("RemoteRoot"));
        }

        [Test]
        public void EnvironmentVariableProviderReadsSecretWithoutPuttingItInProfileJson()
        {
            const string profileName = "TestPublisherToken";
            const string secretValue = "unit-test-secret-value";
            Assert.That(EnvironmentVariableCredentialProvider.TryGetEnvironmentVariableName(profileName, out string variableName), Is.True);
            string previousValue = Environment.GetEnvironmentVariable(variableName);
            try
            {
                Environment.SetEnvironmentVariable(variableName, secretValue);
                var provider = new EnvironmentVariableCredentialProvider();

                Assert.That(provider.TryGetSecret(profileName, out string secret), Is.True);
                Assert.That(secret, Is.EqualTo(secretValue));
                string profileJson = JsonUtility.ToJson(CreateValidProfile(HotUpdateEnvironmentKind.Staging));
                Assert.That(profileJson, Does.Not.Contain(secretValue));
            }
            finally
            {
                Environment.SetEnvironmentVariable(variableName, previousValue);
            }
        }

        [Test]
        public void LocalFolderRootIsPersistableWithoutPersistingCredentialValues()
        {
            const string localRoot = @"D:\MountedHotUpdate";
            HotUpdateEnvironmentProfile profile = CreateValidProfile(HotUpdateEnvironmentKind.Staging);
            profile.LocalFolderRoot = localRoot;

            string json = JsonUtility.ToJson(profile);
            HotUpdateEnvironmentProfile restored = JsonUtility.FromJson<HotUpdateEnvironmentProfile>(json);

            Assert.That(restored.LocalFolderRoot, Is.EqualTo(localRoot));
            Assert.That(json, Does.Not.Contain("unit-test-secret-value"));
        }

        [Test]
        public void EnvironmentVariableProviderRejectsInvalidNamesAndReportsMissingValues()
        {
            var provider = new EnvironmentVariableCredentialProvider();
            const string profileName = "MissingPublisherProfileForUnitTest";
            Assert.That(EnvironmentVariableCredentialProvider.TryGetEnvironmentVariableName(profileName, out string variableName), Is.True);
            string previousValue = Environment.GetEnvironmentVariable(variableName);

            try
            {
                Environment.SetEnvironmentVariable(variableName, null);
                Assert.That(provider.TryGetSecret("bad profile name", out string invalidSecret), Is.False);
                Assert.That(invalidSecret, Is.Null);
                Assert.That(provider.TryGetSecret(profileName, out string missingSecret), Is.False);
                Assert.That(missingSecret, Is.Null);
            }
            finally
            {
                Environment.SetEnvironmentVariable(variableName, previousValue);
            }
        }

        private static HotUpdateEnvironmentProfile CreateValidProfile(HotUpdateEnvironmentKind environment)
        {
            return new HotUpdateEnvironmentProfile
            {
                EnvironmentId = environment.ToString(),
                MainHostServer = "https://cdn.example.com",
                FallbackHostServer = "https://backup.example.com",
                RemoteRoot = "stellar/releases",
                PublishTarget = "LocalFolder",
                CredentialProfileName = "ReleaseToken"
            };
        }
    }
}
