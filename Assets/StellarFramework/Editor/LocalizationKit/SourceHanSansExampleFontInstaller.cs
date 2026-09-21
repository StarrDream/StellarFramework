using System;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace StellarFramework.Localization.Editor
{
    public static class SourceHanSansExampleFontInstaller
    {
        public const string FontAssetPath =
            "Assets/StellarFramework/Samples/ArchitectureDemo/Fonts/SourceHanSans/SourceHanSansCN-Regular.otf";
        public const string LicenseAssetPath =
            "Assets/StellarFramework/Samples/ArchitectureDemo/Fonts/SourceHanSans/LICENSE.txt";
        public const string FontUrl =
            "https://raw.githubusercontent.com/adobe-fonts/source-han-sans/release/SubsetOTF/CN/SourceHanSansCN-Regular.otf";
        public const string LicenseUrl =
            "https://raw.githubusercontent.com/adobe-fonts/source-han-sans/release/LICENSE.txt";
        public const string FontSha256 =
            "E2BC8A2E7F37474B774FFF8DB758681ECE40BB6947A90D571BCE9DD60671A8E4";
        public const string LicenseSha256 =
            "FCAC737E761EC63DBFBDCE11030A1780161920D80315EDBA9C8BEFF1C2BAC5A2";

        private enum InstallStage
        {
            None = 0,
            Font = 1,
            License = 2,
        }

        private static InstallStage _stage;
        private static UnityWebRequest _request;
        private static byte[] _fontBytes;

        [MenuItem("Tools/Stellar Framework/Localization/Install Source Han Sans Example Font")]
        public static void InstallFromMenu()
        {
            if (_stage != InstallStage.None)
            {
                Debug.LogWarning("[SourceHanSansExampleFontInstaller] Install is already running.");
                return;
            }

            if (TryVerifyInstalled(out _))
            {
                AssetDatabase.ImportAsset(FontAssetPath, ImportAssetOptions.ForceSynchronousImport);
                AssetDatabase.ImportAsset(LicenseAssetPath, ImportAssetOptions.ForceSynchronousImport);
                Font font = AssetDatabase.LoadAssetAtPath<Font>(FontAssetPath);
                if (font == null)
                {
                    Debug.LogError(
                        "[SourceHanSansExampleFontInstaller] Verified files exist but Unity failed to import Font asset: " +
                        FontAssetPath);
                    return;
                }
                Debug.Log(
                    "[SourceHanSansExampleFontInstaller] Already installed and verified: " + FontAssetPath);
                return;
            }

            Debug.Log("[SourceHanSansExampleFontInstaller] Starting official Adobe Source Han Sans download.");
            StartRequest(InstallStage.Font, FontUrl);
        }

        public static bool TryVerifyInstalled(out string error)
        {
            if (!TryVerifyAssetFile(FontAssetPath, FontSha256, out error)) return false;
            if (!TryVerifyAssetFile(LicenseAssetPath, LicenseSha256, out error)) return false;
            error = null;
            return true;
        }

        private static void StartRequest(InstallStage stage, string url)
        {
            _stage = stage;
            _request = UnityWebRequest.Get(url);
            _request.SetRequestHeader("User-Agent", "StellarFramework-LocalizationKit");
            _request.SendWebRequest();
            EditorApplication.update -= TickInstall;
            EditorApplication.update += TickInstall;
        }

        private static void TickInstall()
        {
            if (_request == null || !_request.isDone) return;

            if (_request.result != UnityWebRequest.Result.Success)
            {
                Fail(
                    "Download failed for stage " + _stage +
                    ": " + _request.error);
                return;
            }

            byte[] bytes = _request.downloadHandler.data;
            if (_stage == InstallStage.Font)
            {
                if (!TryValidateBytes(bytes, FontSha256, "font", out string error))
                {
                    Fail(error);
                    return;
                }

                _fontBytes = bytes;
                DisposeRequest();
                StartRequest(InstallStage.License, LicenseUrl);
                return;
            }

            if (_stage == InstallStage.License)
            {
                if (!TryValidateBytes(bytes, LicenseSha256, "license", out string error))
                {
                    Fail(error);
                    return;
                }

                byte[] licenseBytes = bytes;
                try
                {
                    CommitVerifiedFiles(_fontBytes, licenseBytes);
                    Font font = AssetDatabase.LoadAssetAtPath<Font>(FontAssetPath);
                    if (font == null)
                        throw new InvalidOperationException(
                            "Unity failed to import Source Han Sans as Font asset: " + FontAssetPath);

                    Debug.Log(
                        "[SourceHanSansExampleFontInstaller] Installed and verified font=" +
                        font.name + " path=" + FontAssetPath);
                    Complete();
                }
                catch (Exception exception)
                {
                    Fail(exception.GetType().Name + ": " + exception.Message);
                }
            }
        }

        private static void CommitVerifiedFiles(byte[] fontBytes, byte[] licenseBytes)
        {
            WriteAtomic(FontAssetPath, fontBytes);
            WriteAtomic(LicenseAssetPath, licenseBytes);
            AssetDatabase.ImportAsset(FontAssetPath, ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(LicenseAssetPath, ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.SaveAssets();
        }

        private static void WriteAtomic(string assetPath, byte[] bytes)
        {
            string absolutePath = ToAbsolutePath(assetPath);
            string directory = Path.GetDirectoryName(absolutePath);
            if (string.IsNullOrEmpty(directory))
                throw new InvalidOperationException(
                    "Unable to resolve destination directory for " + assetPath);
            Directory.CreateDirectory(directory);

            string tempPath = absolutePath + ".download";
            File.WriteAllBytes(tempPath, bytes);
            if (File.Exists(absolutePath)) File.Delete(absolutePath);
            File.Move(tempPath, absolutePath);
        }

        private static bool TryValidateBytes(
            byte[] bytes,
            string expectedSha256,
            string label,
            out string error)
        {
            if (bytes == null || bytes.Length == 0)
            {
                error = "Downloaded " + label + " is empty.";
                return false;
            }

            string actual = ComputeSha256(bytes);
            if (!string.Equals(actual, expectedSha256, StringComparison.OrdinalIgnoreCase))
            {
                error =
                    "SHA256 mismatch for " + label +
                    ". expected=" + expectedSha256 +
                    " actual=" + actual;
                return false;
            }

            error = null;
            return true;
        }

        private static void Fail(string error)
        {
            Debug.LogError("[SourceHanSansExampleFontInstaller] " + error);
            Complete();
        }

        private static void Complete()
        {
            EditorApplication.update -= TickInstall;
            DisposeRequest();
            _fontBytes = null;
            _stage = InstallStage.None;
        }

        private static void DisposeRequest()
        {
            if (_request == null) return;
            _request.Dispose();
            _request = null;
        }

        private static bool TryVerifyAssetFile(
            string assetPath,
            string expectedSha256,
            out string error)
        {
            string absolutePath = ToAbsolutePath(assetPath);
            if (!File.Exists(absolutePath))
            {
                error = "Required font artifact is missing: " + assetPath;
                return false;
            }

            string actual = ComputeSha256(File.ReadAllBytes(absolutePath));
            if (!string.Equals(actual, expectedSha256, StringComparison.OrdinalIgnoreCase))
            {
                error =
                    "SHA256 mismatch for " + assetPath +
                    ". expected=" + expectedSha256 +
                    " actual=" + actual;
                return false;
            }

            error = null;
            return true;
        }

        private static string ComputeSha256(byte[] bytes)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(bytes);
                return BitConverter.ToString(hash).Replace("-", string.Empty);
            }
        }

        private static string ToAbsolutePath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot))
                throw new InvalidOperationException("Unable to resolve Unity project root.");
            return Path.Combine(
                projectRoot,
                assetPath.Replace('/', Path.DirectorySeparatorChar));
        }
    }
}
