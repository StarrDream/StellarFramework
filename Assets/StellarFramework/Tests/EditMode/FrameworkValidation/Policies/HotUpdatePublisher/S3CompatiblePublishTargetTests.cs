using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using StellarFramework.Editor.HotUpdatePublisher;

namespace StellarFramework.Tests.Policies.HotUpdatePublisher
{
    public sealed class S3CompatiblePublishTargetTests
    {
        private string _root;
        private string _sourceRoot;
        private FakeObjectStore _store;
        private S3CompatiblePublishTarget _target;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "StellarS3TargetTests", Guid.NewGuid().ToString("N"));
            _sourceRoot = Path.Combine(_root, "source");
            Directory.CreateDirectory(_sourceRoot);
            _store = new FakeObjectStore();
            _target = CreateTarget(_store);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        [Test]
        public void UploadAsync_IsIdempotentForMatchingContentAndRejectsConflicts()
        {
            HotUpdatePublishFile original = CreateSource("bundle.bin", "first payload");
            HotUpdatePublishTargetFileInfo uploaded = _target.UploadAsync(original, CancellationToken.None).GetAwaiter().GetResult();
            HotUpdatePublishTargetFileInfo repeated = _target.UploadAsync(original, CancellationToken.None).GetAwaiter().GetResult();
            Assert.That(uploaded.Sha256, Is.EqualTo(original.Sha256));
            Assert.That(repeated.Length, Is.EqualTo(original.Length));

            HotUpdatePublishFile conflicting = CreateSource("bundle.bin", "different payload");
            Assert.Throws<IOException>(() => _target.UploadAsync(conflicting, CancellationToken.None).GetAwaiter().GetResult());
            Assert.That(Encoding.UTF8.GetString(_store.Read("hotupdate/Development/bundle.bin")), Is.EqualTo("first payload"));
        }

        [Test]
        public void VerifyAsync_UsesRemoteBytesAndDetectsTampering()
        {
            HotUpdatePublishFile file = CreateSource("manifest.json", "manifest payload");
            _target.UploadAsync(file, CancellationToken.None).GetAwaiter().GetResult();
            _target.VerifyAsync(new[] { file }, CancellationToken.None).GetAwaiter().GetResult();
            _store.Replace("hotupdate/Development/manifest.json", Encoding.UTF8.GetBytes("tampered"));

            Assert.Throws<IOException>(() => _target.VerifyAsync(new[] { file }, CancellationToken.None).GetAwaiter().GetResult());
        }

        [Test]
        public void UploadAsync_RejectsTraversalAndChangedSourceSnapshot()
        {
            Assert.Throws<ArgumentException>(() => _target.ExistsAsync("../outside", CancellationToken.None).GetAwaiter().GetResult());
            HotUpdatePublishFile file = CreateSource("changed.bin", "before");
            File.WriteAllText(file.SourcePath, "after");
            Assert.Throws<IOException>(() => _target.UploadAsync(file, CancellationToken.None).GetAwaiter().GetResult());
            Assert.That(_store.Count, Is.EqualTo(0));
        }

        [Test]
        public void PublishVersionAsync_UsesCreateOnlyThenCompareAndSwapForRollback()
        {
            var first = new HotUpdateVersionPublishRequest
            {
                PointerRelativePath = "PackageVersion",
                PackageVersion = "2.0.0",
                ExpectedCurrentPackageVersion = string.Empty
            };
            _target.PublishVersionAsync(first, CancellationToken.None).GetAwaiter().GetResult();

            var stale = new HotUpdateVersionPublishRequest
            {
                PointerRelativePath = "PackageVersion",
                PackageVersion = "2.1.0",
                ExpectedCurrentPackageVersion = "1.0.0"
            };
            Assert.Throws<IOException>(() => _target.PublishVersionAsync(stale, CancellationToken.None).GetAwaiter().GetResult());

            var rollback = new HotUpdateVersionPublishRequest
            {
                PointerRelativePath = "PackageVersion",
                PackageVersion = "1.9.0",
                ExpectedCurrentPackageVersion = "2.0.0"
            };
            _target.RollbackAsync(rollback, CancellationToken.None).GetAwaiter().GetResult();
            Assert.That(Encoding.UTF8.GetString(_store.Read("hotupdate/Development/PackageVersion")), Is.EqualTo("1.9.0\n"));
        }

        [Test]
        public void Constructor_RejectsInvalidS3ProfileAndOptions()
        {
            HotUpdateEnvironmentProfile profile = CreateProfile();
            profile.PublishTarget = "LocalFolder";
            Assert.Throws<ArgumentException>(() => new S3CompatiblePublishTarget(profile, CreateOptions(), null, _store));

            profile.PublishTarget = "S3Compatible";
            S3CompatiblePublishTargetOptions invalid = CreateOptions();
            invalid.RequestTimeoutMilliseconds = 999;
            Assert.Throws<ArgumentOutOfRangeException>(() => new S3CompatibleSignedObjectStoreClient(invalid, new S3CompatibleCredentials("access", "secret")));
        }

        [Test]
        public void CredentialFailureDoesNotEchoSecretAndRequiresTlsForRemoteEndpoint()
        {
            HotUpdateEnvironmentProfile profile = CreateProfile();
            var provider = new FixedCredentialProvider("not-json-secret");
            InvalidDataException invalid = Assert.Throws<InvalidDataException>(() =>
                new S3CompatiblePublishTarget(profile, CreateOptions(), provider));
            Assert.That(invalid.ToString(), Does.Not.Contain("not-json-secret"));

            var credentials = new FixedCredentialProvider("{\"accessKeyId\":\"id\",\"secretAccessKey\":\"secret\"}");
            S3CompatiblePublishTargetOptions remoteHttp = CreateOptions();
            remoteHttp.ServiceEndpoint = new Uri("http://storage.example.test");
            ArgumentException tls = Assert.Throws<ArgumentException>(() => new S3CompatiblePublishTarget(profile, remoteHttp, credentials));
            Assert.That(tls.Message, Does.Not.Contain("secret"));
        }

        [Test]
        public void Signer_IsDeterministicAndAuthorizationDoesNotContainSecret()
        {
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["host"] = "s3.example.test",
                ["x-amz-date"] = "20260924T120000Z",
                ["x-amz-content-sha256"] = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
                ["if-none-match"] = "*"
            };
            var credentials = new S3CompatibleCredentials("ACCESS123", "private-signing-key");
            Uri uri = new Uri("https://s3.example.test/bucket/a%20b/file.bin");
            string first = S3V4Signer.CreateAuthorizationHeader("PUT", uri, headers, headers["x-amz-content-sha256"], credentials, "us-east-1");
            string second = S3V4Signer.CreateAuthorizationHeader("PUT", uri, headers, headers["x-amz-content-sha256"], credentials, "us-east-1");

            Assert.That(first, Is.EqualTo(second));
            Assert.That(first, Does.StartWith("AWS4-HMAC-SHA256 Credential=ACCESS123/20260924/us-east-1/s3/aws4_request"));
            Assert.That(first, Does.Contain("SignedHeaders=host;if-none-match;x-amz-content-sha256;x-amz-date"));
            Assert.That(first, Does.Not.Contain("private-signing-key"));
        }

        private HotUpdatePublishFile CreateSource(string relativePath, string contents)
        {
            string path = Path.Combine(_sourceRoot, Guid.NewGuid().ToString("N") + ".bin");
            File.WriteAllText(path, contents);
            return HotUpdatePublishFile.FromFile(relativePath, path);
        }

        private static S3CompatiblePublishTarget CreateTarget(FakeObjectStore store)
        {
            return new S3CompatiblePublishTarget(CreateProfile(), CreateOptions(), null, store);
        }

        private static HotUpdateEnvironmentProfile CreateProfile()
        {
            return new HotUpdateEnvironmentProfile
            {
                EnvironmentId = "Development",
                MainHostServer = "https://cdn.example.test",
                FallbackHostServer = string.Empty,
                RemoteRoot = "hotupdate/Development",
                PublishTarget = "S3Compatible",
                CredentialProfileName = "DevelopmentCdn"
            };
        }

        private static S3CompatiblePublishTargetOptions CreateOptions()
        {
            return new S3CompatiblePublishTargetOptions
            {
                ServiceEndpoint = new Uri("https://s3.example.test"),
                Bucket = "stellar-builds",
                Region = "us-east-1"
            };
        }

        private sealed class FixedCredentialProvider : IHotUpdateCredentialProvider
        {
            private readonly string _secret;
            public FixedCredentialProvider(string secret) { _secret = secret; }
            public bool TryGetSecret(string credentialProfileName, out string secret)
            {
                secret = _secret;
                return !string.IsNullOrEmpty(secret);
            }
        }

        private sealed class FakeObjectStore : IS3CompatibleObjectStoreClient
        {
            private readonly Dictionary<string, byte[]> _objects = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            private readonly Dictionary<string, string> _etags = new Dictionary<string, string>(StringComparer.Ordinal);
            public int Count => _objects.Count;

            public Task<S3CompatibleObjectInfo> GetInfoAsync(string key, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!_objects.TryGetValue(key, out byte[] bytes)) return Task.FromResult<S3CompatibleObjectInfo>(null);
                return Task.FromResult(new S3CompatibleObjectInfo
                {
                    Key = key,
                    Length = bytes.Length,
                    ETag = _etags[key],
                    Sha256 = ComputeSha256(bytes)
                });
            }

            public Task<string> GetObjectSha256Async(string key, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(ComputeSha256(_objects[key]));
            }

            public Task<string> GetTextAsync(string key, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(Encoding.UTF8.GetString(_objects[key]));
            }

            public Task PutFileAsync(string key, string sourcePath, long length, string sha256, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (_objects.ContainsKey(key)) throw new S3CompatiblePreconditionFailedException("conditional conflict", null);
                Store(key, File.ReadAllBytes(sourcePath));
                return Task.CompletedTask;
            }

            public Task PutTextAsync(string key, string text, string expectedETag, bool createOnly, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (createOnly ? _objects.ContainsKey(key) : !_etags.TryGetValue(key, out string current) || current != expectedETag)
                    throw new S3CompatiblePreconditionFailedException("conditional conflict", null);
                Store(key, Encoding.UTF8.GetBytes(text));
                return Task.CompletedTask;
            }

            public byte[] Read(string key) => _objects[key];
            public void Replace(string key, byte[] bytes) => Store(key, bytes);

            private void Store(string key, byte[] bytes)
            {
                _objects[key] = bytes;
                _etags[key] = "\"" + ComputeSha256(bytes) + "\"";
            }

            private static string ComputeSha256(byte[] bytes)
            {
                using (SHA256 sha256 = SHA256.Create())
                {
                    byte[] hash = sha256.ComputeHash(bytes);
                    var builder = new StringBuilder(hash.Length * 2);
                    for (int index = 0; index < hash.Length; index++) builder.Append(hash[index].ToString("x2"));
                    return builder.ToString();
                }
            }
        }
    }
}
