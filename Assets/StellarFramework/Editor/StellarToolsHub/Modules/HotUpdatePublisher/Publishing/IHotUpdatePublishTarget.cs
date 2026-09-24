using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace StellarFramework.Editor.HotUpdatePublisher
{
    /// <summary>单个待上传的不可变发布文件及其创建时的长度/SHA256 快照。</summary>
    public interface IHotUpdateRemoteFile
    {
        string RelativePath { get; }
        long Length { get; }
    }

    public sealed class HotUpdatePublishFile : IHotUpdateRemoteFile
    {
        private HotUpdatePublishFile(string relativePath, string sourcePath, long length, string sha256)
        {
            RelativePath = relativePath;
            SourcePath = sourcePath;
            Length = length;
            Sha256 = sha256;
        }

        public string RelativePath { get; }
        public string SourcePath { get; }
        public long Length { get; }
        public string Sha256 { get; }

        /// <summary>读取源文件并生成不可变校验快照。</summary>
        public static HotUpdatePublishFile FromFile(string relativePath, string sourcePath)
        {
            if (string.IsNullOrWhiteSpace(sourcePath)) throw new ArgumentException("Source path is required.", nameof(sourcePath));
            string absoluteSourcePath = Path.GetFullPath(sourcePath);
            var info = new FileInfo(absoluteSourcePath);
            if (!info.Exists) throw new FileNotFoundException("Publish source file was not found.", absoluteSourcePath);
            return new HotUpdatePublishFile(
                relativePath,
                absoluteSourcePath,
                info.Length,
                ComputeSha256(absoluteSourcePath));
        }

        internal static string ComputeSha256(string path)
        {
            using (SHA256 sha256 = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
            {
                byte[] hash = sha256.ComputeHash(stream);
                var builder = new StringBuilder(hash.Length * 2);
                for (int index = 0; index < hash.Length; index++) builder.Append(hash[index].ToString("x2"));
                return builder.ToString();
            }
        }
    }

    /// <summary>目标端单个文件的实际长度和 SHA256。</summary>
    public sealed class HotUpdatePublishTargetFileInfo
    {
        public HotUpdatePublishTargetFileInfo(string relativePath, long length, string sha256)
        {
            RelativePath = relativePath ?? string.Empty;
            Length = length;
            Sha256 = sha256 ?? string.Empty;
        }

        public string RelativePath { get; }
        public long Length { get; }
        public string Sha256 { get; }
    }

    /// <summary>一次明确要求更新的可变版本指针。不可变 Bundle/Manifest 文件不使用此请求。</summary>
    public sealed class HotUpdateVersionPublishRequest
    {
        public string PointerRelativePath { get; set; } = "PackageVersion";
        public string PackageVersion { get; set; } = string.Empty;
        public string ExpectedCurrentPackageVersion { get; set; } = string.Empty;
    }

    /// <summary>上传、查询、校验和最后切换版本指针的发布目标抽象。</summary>
    public interface IHotUpdatePublishTarget
    {
        Task<HotUpdatePublishTargetFileInfo> UploadAsync(HotUpdatePublishFile file, CancellationToken cancellationToken);
        Task<bool> ExistsAsync(string relativePath, CancellationToken cancellationToken);
        Task<HotUpdatePublishTargetFileInfo> GetInfoAsync(string relativePath, CancellationToken cancellationToken);
        Task VerifyAsync(IReadOnlyList<HotUpdatePublishFile> files, CancellationToken cancellationToken);
        Task PublishVersionAsync(HotUpdateVersionPublishRequest request, CancellationToken cancellationToken);
        Task RollbackAsync(HotUpdateVersionPublishRequest request, CancellationToken cancellationToken);
    }
}
