using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using StellarFramework.Editor.DevTools;
using UnityEditor;

namespace StellarFramework.Tests.FrameworkValidation
{
    public sealed class DeveloperQuickToolsLogicTests
    {
        [Test]
        public void AddOrMoveRecentDeduplicatesAndKeepsNewestFirst()
        {
            var scenes = new List<DeveloperQuickSceneReference>
            {
                new DeveloperQuickSceneReference { Guid = "a", Path = "Assets/A.unity", Name = "A" },
                new DeveloperQuickSceneReference { Guid = "b", Path = "Assets/B.unity", Name = "B" }
            };

            DeveloperQuickToolsLogic.AddOrMoveRecent(
                scenes,
                new DeveloperQuickSceneReference { Guid = "a", Path = "Assets/Renamed/A.unity", Name = "A2" },
                12);

            Assert.That(scenes, Has.Count.EqualTo(2));
            Assert.That(scenes[0].Guid, Is.EqualTo("a"));
            Assert.That(scenes[0].Path, Is.EqualTo("Assets/Renamed/A.unity"));
            Assert.That(scenes[1].Guid, Is.EqualTo("b"));
        }

        [Test]
        public void AddOrMoveRecentRespectsLimit()
        {
            var scenes = new List<DeveloperQuickSceneReference>();

            for (int i = 0; i < 5; i++)
            {
                DeveloperQuickToolsLogic.AddOrMoveRecent(
                    scenes,
                    new DeveloperQuickSceneReference { Guid = i.ToString(), Path = $"Assets/{i}.unity" },
                    3);
            }

            Assert.That(scenes, Has.Count.EqualTo(3));
            Assert.That(scenes[0].Guid, Is.EqualTo("4"));
            Assert.That(scenes[2].Guid, Is.EqualTo("2"));
        }

        [Test]
        public void ClampTimeScaleKeepsDebugRange()
        {
            Assert.That(DeveloperQuickToolsLogic.ClampTimeScale(-10f), Is.EqualTo(0f));
            Assert.That(DeveloperQuickToolsLogic.ClampTimeScale(150f), Is.EqualTo(100f));
            Assert.That(DeveloperQuickToolsLogic.ClampTimeScale(float.NaN), Is.EqualTo(1f));
        }

        [Test]
        public void CalculateFixedDeltaTimeScalesFromBaseAndKeepsZeroStable()
        {
            Assert.That(DeveloperQuickToolsLogic.CalculateFixedDeltaTime(0.02f, 10f), Is.EqualTo(0.2f).Within(0.0001f));
            Assert.That(DeveloperQuickToolsLogic.CalculateFixedDeltaTime(0.02f, 0f), Is.EqualTo(0.02f).Within(0.0001f));
        }

        [Test]
        public void MenuLabelsPreferAllScenesAndFlatFavorites()
        {
            string source = System.IO.File.ReadAllText("Assets/StellarFramework/Editor/StellarToolsHub/Modules/DevTools/DeveloperQuickToolsState.cs");

            Assert.That(source, Does.Contain("AddSceneMenu(menu, \"所有场景\""));
            Assert.That(source, Does.Not.Contain("AddSceneMenu(menu, \"样例场景\""));
            Assert.That(source, Does.Not.Contain("\"收藏/\" + group.Name"));
        }

        [Test]
        public void SceneViewFallbackUsesFavoriteToggle()
        {
            string source = System.IO.File.ReadAllText("Assets/StellarFramework/Editor/StellarToolsHub/Modules/DevTools/DeveloperQuickToolsSceneViewFallback.cs");

            Assert.That(source, Does.Contain("ToggleCurrentSceneFavorite"));
            Assert.That(source, Does.Contain("取消收藏"));
            Assert.That(source, Does.Not.Contain("AddCurrentSceneToFavorites"));
        }

        [Test]
        public void ToggleFavoriteAddsAndRemovesFromDefaultGroup()
        {
            var preferences = new DeveloperQuickToolsPreferences();
            preferences.EnsureDefaults();

            var scene = new DeveloperQuickSceneReference
            {
                Guid = "test-guid",
                Path = "Assets/TestScene.unity",
                Name = "TestScene"
            };

            Assert.That(DeveloperQuickToolsLogic.ToggleFavorite(preferences, scene), Is.True);
            Assert.That(preferences.FavoriteGroups.SelectMany(group => group.Scenes).Count(), Is.EqualTo(1));
            Assert.That(DeveloperQuickToolsLogic.IsFavorite(preferences, scene), Is.True);

            Assert.That(DeveloperQuickToolsLogic.ToggleFavorite(preferences, scene), Is.False);
            Assert.That(preferences.FavoriteGroups.SelectMany(group => group.Scenes), Is.Empty);
            Assert.That(DeveloperQuickToolsLogic.IsFavorite(preferences, scene), Is.False);
        }

        [Test]
        public void RemoveMissingScenesCleansRecentAndFavorites()
        {
            var preferences = new DeveloperQuickToolsPreferences();
            preferences.EnsureDefaults();

            var validScene = new DeveloperQuickSceneReference { Guid = "valid", Path = "Assets/Valid.unity" };
            var missingScene = new DeveloperQuickSceneReference { Guid = "missing", Path = "Assets/Missing.unity" };

            preferences.RecentScenes.Add(validScene);
            preferences.RecentScenes.Add(missingScene);
            preferences.FavoriteGroups[0].Scenes.Add(validScene);
            preferences.FavoriteGroups[0].Scenes.Add(missingScene);

            int removed = DeveloperQuickToolsLogic.RemoveMissingScenes(
                preferences,
                scene => scene != null && scene.Guid == "valid");

            Assert.That(removed, Is.EqualTo(2));
            Assert.That(preferences.RecentScenes, Has.Count.EqualTo(1));
            Assert.That(preferences.RecentScenes[0].Guid, Is.EqualTo("valid"));
            Assert.That(preferences.FavoriteGroups[0].Scenes, Has.Count.EqualTo(1));
            Assert.That(preferences.FavoriteGroups[0].Scenes[0].Guid, Is.EqualTo("valid"));
        }

        [Test]
        public void RemoveMissingScenesKeepsFrameworkScenesAndCleansExcludedQuickScenes()
        {
            var preferences = new DeveloperQuickToolsPreferences();
            preferences.EnsureDefaults();

            var projectScene = new DeveloperQuickSceneReference { Guid = "project", Path = "Assets/Scenes/Main.unity" };
            var packageScene = new DeveloperQuickSceneReference { Guid = "package", Path = "Packages/com.company.demo/Samples~/Demo.unity" };
            var frameworkScene = new DeveloperQuickSceneReference { Guid = "framework", Path = "Assets/StellarFramework/Samples/KitSamples/Scenes/UIKit_Playable.unity" };

            preferences.RecentScenes.Add(projectScene);
            preferences.RecentScenes.Add(packageScene);
            preferences.RecentScenes.Add(frameworkScene);
            preferences.FavoriteGroups[0].Scenes.Add(packageScene);
            preferences.FavoriteGroups[0].Scenes.Add(frameworkScene);

            int removed = DeveloperQuickToolsLogic.RemoveMissingScenes(
                preferences,
                scene => DeveloperQuickToolsLogic.ShouldIncludeSceneInQuickList(scene.Path));

            Assert.That(removed, Is.EqualTo(2));
            Assert.That(preferences.RecentScenes.Select(scene => scene.Guid), Is.EqualTo(new[] { "project", "framework" }));
            Assert.That(preferences.FavoriteGroups[0].Scenes.Single().Guid, Is.EqualTo("framework"));
        }

        [Test]
        public void QuickSceneListKeepsFrameworkScenesButFiltersPackageAndTildeScenes()
        {
            Assert.That(DeveloperQuickToolsLogic.ShouldIncludeSceneInQuickList("Assets/Scenes/Main.unity"), Is.True);
            Assert.That(DeveloperQuickToolsLogic.ShouldIncludeSceneInQuickList("Assets/StellarFramework/Samples/KitSamples/Scenes/UIKit_Playable.unity"), Is.True);
            Assert.That(DeveloperQuickToolsLogic.ShouldIncludeSceneInQuickList("Assets/StellarFramework/Samples/ArchitectureDemo/Scene/FrameworkArchitecture_Playable.unity"), Is.True);
            Assert.That(DeveloperQuickToolsLogic.IsFrameworkScene("Assets/StellarFramework/Samples/KitSamples/Scenes/UIKit_Playable.unity"), Is.True);
            Assert.That(DeveloperQuickToolsLogic.ShouldIncludeSceneInQuickList("Packages/com.company.demo/Samples~/Demo.unity"), Is.False);
            Assert.That(DeveloperQuickToolsLogic.ShouldIncludeSceneInQuickList("Assets/ThirdParty/Package/Samples~/Demo.unity"), Is.False);
            Assert.That(DeveloperQuickToolsLogic.ShouldIncludeSceneInQuickList("Assets/ThirdParty/Package/Scenes~/Demo.unity"), Is.False);
        }

        [Test]
        public void SceneMenuLabelKeepsAllScenesOneLevel()
        {
            var projectScene = new DeveloperQuickSceneReference
            {
                Path = "Assets/Scenes/Main.unity",
                Name = "Main"
            };

            var nestedProjectScene = new DeveloperQuickSceneReference
            {
                Path = "Assets/Game/Scenes/Battle/BattleMain.unity",
                Name = "BattleMain"
            };

            string projectLabel = DeveloperQuickToolsLogic.BuildSceneMenuLabel("所有场景", projectScene, true, false);
            string nestedProjectLabel = DeveloperQuickToolsLogic.BuildSceneMenuLabel("所有场景", nestedProjectScene, true, false);

            Assert.That(projectLabel, Is.EqualTo("所有场景/Main  (项目 > Scenes)"));
            Assert.That(nestedProjectLabel, Is.EqualTo("所有场景/BattleMain  (项目 > Game > Scenes > Battle)"));
            Assert.That(projectLabel.Count(character => character == '/'), Is.EqualTo(1));
            Assert.That(nestedProjectLabel.Count(character => character == '/'), Is.EqualTo(1));
        }

        [Test]
        public void FrameworkSceneMenuLabelUsesSingleFrameworkFolder()
        {
            var frameworkScene = new DeveloperQuickSceneReference
            {
                Path = "Assets/StellarFramework/Samples/KitSamples/Scenes/UIKit_Playable.unity",
                Name = "UIKit_Playable"
            };

            string label = DeveloperQuickToolsLogic.BuildSceneMenuLabel("StellarFramework", frameworkScene, true, false);

            Assert.That(label, Is.EqualTo("StellarFramework/UIKit_Playable  (框架 > Samples > KitSamples > Scenes)"));
            Assert.That(label.Count(character => character == '/'), Is.EqualTo(1));
        }

        [Test]
        public void DeveloperQuickToolsDoesNotHardcodeFrameworkSampleSceneButtons()
        {
            string source = System.IO.File.ReadAllText("Assets/StellarFramework/Editor/StellarToolsHub/Modules/DevTools/DeveloperQuickToolsHubModule.cs");

            Assert.That(source, Does.Not.Contain("FrameworkValidation_Playable"));
            Assert.That(source, Does.Not.Contain("UIKit_Playable"));
            Assert.That(source, Does.Not.Contain("ResKit_Playable"));
        }

        [Test]
        public void SceneToolbarButtonUsesDirectSceneSwitchMenu()
        {
            string stateSource = System.IO.File.ReadAllText("Assets/StellarFramework/Editor/StellarToolsHub/Modules/DevTools/DeveloperQuickToolsState.cs");
            string overlaySource = System.IO.File.ReadAllText("Assets/StellarFramework/Editor/StellarToolsHub/Modules/DevTools/DeveloperQuickToolsOverlay.cs");
            string fallbackSource = System.IO.File.ReadAllText("Assets/StellarFramework/Editor/StellarToolsHub/Modules/DevTools/DeveloperQuickToolsSceneViewFallback.cs");

            Assert.That(stateSource, Does.Contain("BuildDirectSceneSwitchMenu"));
            Assert.That(overlaySource, Does.Contain("BuildDirectSceneSwitchMenu"));
            Assert.That(fallbackSource, Does.Contain("BuildDirectSceneSwitchMenu"));
        }

        [Test]
        public void DirectSceneSwitchMenuPutsFrequentItemsBeforeAllScenes()
        {
            string source = System.IO.File.ReadAllText("Assets/StellarFramework/Editor/StellarToolsHub/Modules/DevTools/DeveloperQuickToolsState.cs");
            int methodStart = source.IndexOf("public static void BuildDirectSceneSwitchMenu", System.StringComparison.Ordinal);
            int recentIndex = source.IndexOf("\"最近打开\"", methodStart, System.StringComparison.Ordinal);
            int favoriteIndex = source.IndexOf("AddFavoriteMenus(menu);", methodStart, System.StringComparison.Ordinal);
            int buildSettingsIndex = source.IndexOf("\"Build Settings\"", methodStart, System.StringComparison.Ordinal);
            int allScenesIndex = source.IndexOf("AddSceneItems(menu, GetAllProjectScenes()", methodStart, System.StringComparison.Ordinal);
            int frameworkIndex = source.IndexOf("AddSceneMenu(menu, \"StellarFramework\", GetFrameworkScenes()", methodStart, System.StringComparison.Ordinal);

            Assert.That(methodStart, Is.GreaterThanOrEqualTo(0));
            Assert.That(recentIndex, Is.GreaterThan(methodStart));
            Assert.That(favoriteIndex, Is.GreaterThan(recentIndex));
            Assert.That(buildSettingsIndex, Is.GreaterThan(favoriteIndex));
            Assert.That(allScenesIndex, Is.GreaterThan(buildSettingsIndex));
            Assert.That(frameworkIndex, Is.GreaterThan(allScenesIndex));
        }
    }
}
