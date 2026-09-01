using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace StellarFramework.Tests.FrameworkValidation
{
    public sealed class ToolsHubUiConsolidationPolicyTests
    {
        [Test]
        public void PopupBasedToolsHubModulesMustProvideCreateView()
        {
            string builtinModules = ReadAssetText("Assets/StellarFramework/Editor/StellarToolsHub/Modules/BuiltinModules.cs");
            string configKitHub = ReadAssetText("Assets/StellarFramework/Editor/StellarToolsHub/Modules/ConfigKit/ConfigKitHubModule.cs");

            Assert.That(builtinModules, Does.Contain("public override VisualElement CreateView()"));
            Assert.That(configKitHub, Does.Contain("public override VisualElement CreateView()"));
        }

        [Test]
        public void PopupBasedToolsHubModulesMustNotOpenStandaloneWindows()
        {
            string builtinModules = ReadAssetText("Assets/StellarFramework/Editor/StellarToolsHub/Modules/BuiltinModules.cs");
            string configKitHub = ReadAssetText("Assets/StellarFramework/Editor/StellarToolsHub/Modules/ConfigKit/ConfigKitHubModule.cs");

            Assert.That(builtinModules, Does.Not.Contain("CombinedMeshColliderWindow.ShowWindow()"));
            Assert.That(builtinModules, Does.Not.Contain("DictionarySerializerWindow.ShowWindow()"));
            Assert.That(builtinModules, Does.Not.Contain("ListSerializerWindow.ShowWindow()"));
            Assert.That(builtinModules, Does.Not.Contain("FolderContentCopyTool.ShowWindow()"));
            Assert.That(builtinModules, Does.Not.Contain("ActionEngineEditorWindow.ShowWindow()"));
            Assert.That(builtinModules, Does.Not.Contain("URPMaterialConverterWindow.Open()"));
            Assert.That(configKitHub, Does.Not.Contain("ConfigKitWindow.ShowWindow()"));
        }

        [Test]
        public void OriginalPopupToolTypesMustNoLongerBeEditorWindows()
        {
            Assert.That(
                ReadAssetText("Assets/StellarFramework/Editor/StellarToolsHub/Modules/CombinedMeshColliderWindow.cs"),
                Does.Not.Contain(": EditorWindow"));
            Assert.That(
                ReadAssetText("Assets/StellarFramework/Editor/StellarToolsHub/Modules/DictionarySerializerWindow.cs"),
                Does.Not.Contain(": EditorWindow"));
            Assert.That(
                ReadAssetText("Assets/StellarFramework/Editor/StellarToolsHub/Modules/ListSerializerWindow.cs"),
                Does.Not.Contain(": EditorWindow"));
            Assert.That(
                ReadAssetText("Assets/StellarFramework/Editor/StellarToolsHub/Modules/FolderContentCopyTool.cs"),
                Does.Not.Contain(": EditorWindow"));
            Assert.That(
                ReadAssetText("Assets/StellarFramework/Editor/StellarToolsHub/Modules/ActionKit/ActionEngineEditorWindow.cs"),
                Does.Not.Contain(": EditorWindow"));
            Assert.That(
                ReadAssetText("Assets/StellarFramework/Editor/StellarToolsHub/Modules/RuntimeTools/URPMaterialConverterWindow.cs"),
                Does.Not.Contain(": EditorWindow"));
            Assert.That(
                ReadAssetText("Assets/StellarFramework/Editor/StellarToolsHub/Modules/ConfigKit/ConfigKitWindow.cs"),
                Does.Not.Contain(": EditorWindow"));
        }

        [Test]
        public void ToolsHubLayoutKeepsFooterAndHeaderOutOfGrowLayout()
        {
            string source = ReadAssetText("Assets/StellarFramework/Editor/StellarToolsHub/Core/StellarFrameworkTools.cs");

            Assert.That(source, Does.Contain("headerCard.style.flexGrow = 0f;"));
            Assert.That(source, Does.Contain("footer.style.flexGrow = 0f;"));
        }

        private static string ReadAssetText(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            return File.ReadAllText(Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar)));
        }
    }
}
