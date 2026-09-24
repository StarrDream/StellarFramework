using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace StellarFramework.Editor.HotUpdatePublisher
{
    /// <summary>
    /// V1 publishing target for a mounted folder, NAS, Nginx directory or CI staging path.
    /// Artifact paths are immutable: an existing path is reusable only when length and SHA256 match.
    /// </summary>
    public sealed class LocalFolderPublishTarget : IHotUpdatePublishTarget
    {
        private const int CopyBufferSize = 81920;
        private readonly string _targetRoot;

        /// <summary>Creates a LocalFolder target from the non-secret root saved in the environment profile.</summary>
        public LocalFolderPublishTarget(HotUpdateEnvironmentProfile profile)
            : this(profile, profile?.LocalFolderRoot)
        {
        }

        public LocalFolderPublishTarget(HotUpdateEnvironmentProfile profile, string localFolderRoot)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (string.IsNullOrWhiteSpace(localFolderRoot)) throw new ArgumentException("Local folder root is required.", nameof(localFolderRoot));
            if (!string.Equals(profile.PublishTarget, "LocalFolder", StringComparison.Ordinal))
                throw new ArgumentException("Environment profile PublishTarget must be 'LocalFolder'.", nameof(profile));

            HotUpdateEnvironmentProfileValidationResult validation = profile.Validate();
            if (!validation.IsValid)
                throw new ArgumentException(
                    "Environment profile is invalid: " + string.Join(Environment.NewLine, validation.Errors),
                    nameof(profile));

            string basePath = Path.GetFullPath(localFolderRoot);
            _targetRoot = Path.GetFullPath(Path.Combine(
                basePath,
                profile.RemoteRoot.Replace('/', Path.DirectorySeparatorChar)));
            EnsureContained(basePath, _targetRoot);
        }

        /// <summary>目标根目录（不含 profile 的 RemoteRoot）。</summary>
        public string TargetRoot => _targetRoot;

        public Task<HotUpdatePublishTargetFileInfo> UploadAsync(
            HotUpdatePublishFile file,
            CancellationToken cancellationToken)
        {
            if (file == null) throw new ArgumentNullException(nameof(file));
            cancellationToken.ThrowIfCancellationRequested();
            return Task.Run(() => UploadFile(file, cancellationToken), cancellationToken);
        }

        public Task<bool> ExistsAsync(string relativePath, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string absolutePath = ResolveTargetPath(relativePath);
            return Task.Run(() => File.Exists(absolutePath), cancellationToken);
        }

        public Task<HotUpdatePublishTargetFileInfo> GetInfoAsync(
            string relativePath,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string absolutePath = ResolveTargetPath(relativePath);
            return Task.Run(() => GetFileInfo(relativePath, absolutePath), cancellationToken);
        }

        public Task VerifyAsync(IReadOnlyList<HotUpdatePublishFile> files, CancellationToken cancellationToken)
        {
            if (files == null) throw new ArgumentNullException(nameof(files));
            cancellationToken.ThrowIfCancellationRequested();
            return Task.Run(() => VerifyFiles(files, cancellationToken), cancellationToken);
        }

        public Task PublishVersionAsync(
            HotUpdateVersionPublishRequest request,
            CancellationToken cancellationToken)
        {
            ValidateVersionRequest(request);
            cancellationToken.ThrowIfCancellationRequested();
            return Task.Run(() => WriteVersionPointer(request, cancellationToken), cancellationToken);
        }

        public Task RollbackAsync(
            HotUpdateVersionPublishRequest request,
            CancellationToken cancellationToken)
        {
            // Rollback points the same version file at a previously verified immutable package.
            // ExpectedCurrentPackageVersion provides a compare-and-swap guard against concurrent publishers.
            ValidateVersionRequest(request);
            cancellationToken.ThrowIfCancellationRequested();
            return Task.Run(() => WriteVersionPointer(request, cancellationToken), cancellationToken);
        }

        private HotUpdatePublishTargetFileInfo UploadFile(HotUpdatePublishFile file, CancellationToken cancellationToken)
        {
            string destinationPath = ResolveTargetPath(file.RelativePath);
            ValidateSourceSnapshot(file);

            if (File.Exists(destinationPath))
                return VerifyExistingImmutableFile(file, destinationPath);

            string directory = Path.GetDirectoryName(destinationPath);
            Directory.CreateDirectory(directory);
            string temporaryPath = Path.Combine(directory, ".upload-" + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                File.Copy(file.SourcePath, temporaryPath, false);
                cancellationToken.ThrowIfCancellationRequested();
                EnsureMatchesSnapshot(file, temporaryPath, "staged upload");

                try
                {
                    // File.Move without overwrite makes publication race-safe for immutable content.
                    File.Move(temporaryPath, destinationPath);
                }
                catch (IOException exception) when (File.Exists(destinationPath))
                {
                    try
                    {
                        return VerifyExistingImmutableFile(file, destinationPath);
                    }
                    catch (IOException conflict)
                    {
                        throw new IOException(
                            $"Immutable publish path '{file.RelativePath}' was concurrently created with different content. " +
                            $"Move error: {exception.Message} Conflict: {conflict.Message}", conflict);
                    }
                }

                return GetFileInfo(file.RelativePath, destinationPath);
            }
            finally
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
        }

        private void VerifyFiles(IReadOnlyList<HotUpdatePublishFile> files, CancellationToken cancellationToken)
        {
            for (int index = 0; index < files.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                HotUpdatePublishFile file = files[index];
                if (file == null) throw new ArgumentException($"Publish file at index {index} is null.", nameof(files));

                string path = ResolveTargetPath(file.RelativePath);
                ValidateSourceSnapshot(file);
                if (!File.Exists(path))
                    throw new FileNotFoundException($"Published file '{file.RelativePath}' is missing from target.", path);
                EnsureMatchesSnapshot(file, path, "target file");
            }
        }

        private HotUpdatePublishTargetFileInfo VerifyExistingImmutableFile(
            HotUpdatePublishFile file,
            string destinationPath)
        {
            try
            {
                EnsureMatchesSnapshot(file, destinationPath, "existing immutable file");
                return GetFileInfo(file.RelativePath, destinationPath);
            }
            catch (IOException exception)
            {
                throw new IOException(
                    $"Immutable publish path '{file.RelativePath}' already exists with different content. Existing files are never overwritten.",
                    exception);
            }
        }

        private static void ValidateSourceSnapshot(HotUpdatePublishFile file)
        {
            if (!File.Exists(file.SourcePath))
                throw new FileNotFoundException($"Publish source for '{file.RelativePath}' no longer exists.", file.SourcePath);
            EnsureMatchesSnapshot(file, file.SourcePath, "source file");
        }

        private static void EnsureMatchesSnapshot(HotUpdatePublishFile file, string path, string description)
        {
            var info = new FileInfo(path);
            if (info.Length != file.Length)
                throw new IOException($"{description} '{file.RelativePath}' length changed. Expected={file.Length}, Actual={info.Length}.");

            string actualHash = HotUpdatePublishFile.ComputeSha256(path);
            if (!string.Equals(file.Sha256, actualHash, StringComparison.OrdinalIgnoreCase))
                throw new IOException($"{description} '{file.RelativePath}' SHA256 changed. Expected={file.Sha256}, Actual={actualHash}.");
        }

        private HotUpdatePublishTargetFileInfo GetFileInfo(string relativePath, string absolutePath)
        {
            if (!File.Exists(absolutePath)) return null;
            var info = new FileInfo(absolutePath);
            return new HotUpdatePublishTargetFileInfo(
                NormalizeRelativePath(relativePath),
                info.Length,
                HotUpdatePublishFile.ComputeSha256(absolutePath));
        }

        private void WriteVersionPointer(HotUpdateVersionPublishRequest request, CancellationToken cancellationToken)
        {
            string pointerPath = ResolveTargetPath(request.PointerRelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(pointerPath));

            using (var mutex = new Mutex(false, GetVersionPointerMutexName(pointerPath)))
            {
                bool acquired = false;
                Stopwatch waitTimer = Stopwatch.StartNew();
                try
                {
                    while (!acquired)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        TimeSpan remaining = TimeSpan.FromSeconds(30) - waitTimer.Elapsed;
                        if (remaining <= TimeSpan.Zero)
                            throw new TimeoutException($"Timed out waiting to publish version pointer '{request.PointerRelativePath}'.");

                        try
                        {
                            acquired = mutex.WaitOne(remaining < TimeSpan.FromMilliseconds(100)
                                ? remaining
                                : TimeSpan.FromMilliseconds(100));
                        }
                        catch (AbandonedMutexException)
                        {
                            // A previous Editor/process stopped while owning the mutex. The pointer file update itself is atomic,
                            // so re-read its current value under the recovered lock and apply the caller's CAS check.
                            acquired = true;
                        }
                    }

                    WriteVersionPointerUnderLock(pointerPath, request, cancellationToken);
                }
                finally
                {
                    waitTimer.Stop();
                    if (acquired) mutex.ReleaseMutex();
                }
            }
        }

        private static void WriteVersionPointerUnderLock(
            string pointerPath,
            HotUpdateVersionPublishRequest request,
            CancellationToken cancellationToken)
        {
            string currentVersion = File.Exists(pointerPath)
                ? File.ReadAllText(pointerPath, Encoding.UTF8).Trim()
                : string.Empty;
            if (!string.Equals(currentVersion, request.ExpectedCurrentPackageVersion ?? string.Empty, StringComparison.Ordinal))
            {
                throw new IOException(
                    $"Version pointer changed before publish. Expected current='{request.ExpectedCurrentPackageVersion}', actual='{currentVersion}'.");
            }

            cancellationToken.ThrowIfCancellationRequested();
            string temporaryPath = pointerPath + ".publish-" + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporaryPath, request.PackageVersion + "\n", new UTF8Encoding(false));
                cancellationToken.ThrowIfCancellationRequested();
                if (File.Exists(pointerPath))
                    File.Replace(temporaryPath, pointerPath, null);
                else
                    File.Move(temporaryPath, pointerPath);
            }
            finally
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
        }

        private static string GetVersionPointerMutexName(string pointerPath)
        {
            byte[] pathBytes = Encoding.UTF8.GetBytes(Path.GetFullPath(pointerPath).ToUpperInvariant());
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(pathBytes);
                var suffix = new StringBuilder(hash.Length * 2);
                for (int index = 0; index < hash.Length; index++) suffix.Append(hash[index].ToString("x2"));
                return "StellarHotUpdateVersionPointer_" + suffix;
            }
        }

        private void ValidateVersionRequest(HotUpdateVersionPublishRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            ResolveTargetPath(request.PointerRelativePath);
            if (string.IsNullOrWhiteSpace(request.PackageVersion) || !IsSafeVersion(request.PackageVersion))
                throw new ArgumentException("PackageVersion must be a path-safe identifier.", nameof(request));
        }

        private string ResolveTargetPath(string relativePath)
        {
            string normalized = NormalizeRelativePath(relativePath);
            string candidate = Path.GetFullPath(Path.Combine(
                _targetRoot,
                normalized.Replace('/', Path.DirectorySeparatorChar)));
            EnsureContained(_targetRoot, candidate);
            return candidate;
        }

        private static string NormalizeRelativePath(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath)) throw new ArgumentException("A relative target path is required.", nameof(relativePath));
            string normalized = relativePath.Replace('\\', '/');
            if (normalized.StartsWith("/", StringComparison.Ordinal) || normalized.Contains(":"))
                throw new ArgumentException("Target paths must be relative and cannot contain a drive or URI scheme.", nameof(relativePath));

            string[] segments = normalized.Split('/');
            char[] invalidFileNameChars = Path.GetInvalidFileNameChars();
            for (int index = 0; index < segments.Length; index++)
            {
                string segment = segments[index];
                if (string.IsNullOrWhiteSpace(segment) || segment == "." || segment == "..")
                    throw new ArgumentException("Target paths cannot contain empty, '.' or '..' segments.", nameof(relativePath));
                if (segment.IndexOfAny(invalidFileNameChars) >= 0 || segment.IndexOfAny(new[] { '?', '#' }) >= 0)
                    throw new ArgumentException("Target path contains characters that are not safe for a local publish folder.", nameof(relativePath));
            }

            return normalized;
        }

        private static void EnsureContained(string root, string candidate)
        {
            string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string fullCandidate = Path.GetFullPath(candidate);
            string prefix = fullRoot + Path.DirectorySeparatorChar;
            if (!fullCandidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException($"Resolved path '{fullCandidate}' escapes publish root '{fullRoot}'.");
        }

        private static bool IsSafeVersion(string version)
        {
            if (version.Length > 64) return false;
            if (!IsAsciiAlphaNumeric(version[0])) return false;
            for (int index = 1; index < version.Length; index++)
            {
                char character = version[index];
                if (!IsAsciiAlphaNumeric(character) && character != '.' && character != '+' && character != '-') return false;
            }
            return true;
        }

        private static bool IsAsciiAlphaNumeric(char character)
        {
            return (character >= 'A' && character <= 'Z') ||
                   (character >= 'a' && character <= 'z') ||
                   (character >= '0' && character <= '9');
        }
    }
}
