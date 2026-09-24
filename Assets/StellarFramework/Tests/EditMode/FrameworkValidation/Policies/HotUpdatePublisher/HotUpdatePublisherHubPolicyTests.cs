using System.IO;
using System.Reflection;
using NUnit.Framework;
using StellarFramework.Editor;
using StellarFramework.Editor.Modules;
using UnityEngine;

namespace StellarFramework.Tests.FrameworkValidation
{
    public sealed class HotUpdatePublisherHubPolicyTests
    {
        [Test]
        public void PublisherModuleIsRegisteredInHotUpdateToolsHubGroup()
        {
            var attribute = (StellarToolAttribute)typeof(HotUpdatePublisherHubModule)
                .GetCustomAttributes(typeof(StellarToolAttribute), false)[0];

            Assert.That(typeof(ToolModule).IsAssignableFrom(typeof(HotUpdatePublisherHubModule)), Is.True);
            Assert.That(attribute.Title, Is.EqualTo("HotUpdate Publisher"));
            Assert.That(attribute.Group, Is.EqualTo("热更新"));
            Assert.That(attribute.RequiredAssemblyNames, Does.Contain("StellarFramework.ToolsHub.HotUpdatePublisher.Editor"));
        }

        [Test]
        public void PublisherHubExposesRequiredSectionsAndOnlyThreePrimaryActions()
        {
            string source = ReadModuleSource();

            Assert.That(source, Does.Contain("Overview").And.Contain("Changes").And.Contain("Build")
                .And.Contain("Server").And.Contain("History").And.Contain("Advanced"));
            Assert.That(source, Does.Contain("Dry Run").And.Contain("Build & Publish"));
            Assert.That(source, Does.Contain("Local Folder Root").And.Contain("OpenFolderPanel"));
            Assert.That(CountOccurrences(source, "GUILayout.Button(new GUIContent(\"Dry Run\"") , Is.EqualTo(1));
            Assert.That(CountOccurrences(source, "GUILayout.Button(new GUIContent(\"Build\"") , Is.EqualTo(1));
            Assert.That(CountOccurrences(source, "GUILayout.Button(new GUIContent(\"Build & Publish\"") , Is.EqualTo(1));
        }

        [Test]
        public void PublisherHubShowsTruthfulReadinessAndAdvancedTools()
        {
            string source = ReadModuleSource();

            Assert.That(source, Does.Contain("Change Safety").And.Contain("Base App")
                .And.Contain("YooAsset Package").And.Contain("Remote Release")
                .And.Contain("HybridCLR").And.Contain("AOT").And.Contain("Server"));
            Assert.That(source, Does.Contain("HybridCLR Export").And.Contain("YooAsset Build")
                .And.Contain("Run Gate").And.Contain("Open Build Folder")
                .And.Contain("View Manifest").And.Contain("View BaseRelease"));
            Assert.That(source, Does.Contain("尚未接入"));
            Assert.That(source, Does.Contain("EditorPrefs"));
        }

        [Test]
        public void PublisherHubDisplaysGitProvenanceAndBlocksDirtyProduction()
        {
            string source = ReadModuleSource();

            Assert.That(source, Does.Contain("_gitSnapshot.Branch").And.Contain("_gitSnapshot.Commit")
                .And.Contain("_gitSnapshot.IsDirty"));
            Assert.That(source, Does.Contain("Production 发布被禁止"));
            Assert.That(source, Does.Contain("Development/Staging 可继续"));
        }

        private static string ReadModuleSource()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string path = Path.Combine(projectRoot, "Assets/StellarFramework/Editor/StellarToolsHub/Modules/HotUpdatePublisher/HotUpdatePublisherHubModule.cs");
            return File.ReadAllText(path);
        }

        private static int CountOccurrences(string value, string fragment)
        {
            int count = 0;
            int offset = 0;
            while ((offset = value.IndexOf(fragment, offset, System.StringComparison.Ordinal)) >= 0)
            {
                count++;
                offset += fragment.Length;
            }
            return count;
        }
    }
}
