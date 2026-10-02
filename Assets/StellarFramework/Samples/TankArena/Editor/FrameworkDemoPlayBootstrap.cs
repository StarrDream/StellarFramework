#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace StellarFramework.Samples.TankArena.Editor
{
    /// <summary>
    /// Lets the hot-update sample be previewed from its scene in the Editor.
    /// Android and Windows release gates still load the same entry through YooAsset and HybridCLR.
    /// </summary>
    [InitializeOnLoad]
    internal static class FrameworkDemoPlayBootstrap
    {
        private const string ScenePath = "Assets/StellarFramework/Samples/TankArena/Scene/FrameworkDemo.unity";
        private const string EntryAssemblyName = "HotUpdate";
        private const string EntryTypeName = "HotUpdate.HotUpdateMain";

        static FrameworkDemoPlayBootstrap()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode ||
                !string.Equals(SceneManager.GetActiveScene().path, ScenePath, StringComparison.Ordinal) ||
                GameObject.Find("StellarTankArena") != null)
            {
                return;
            }

            try
            {
                Assembly assembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(candidate => string.Equals(
                        candidate.GetName().Name, EntryAssemblyName, StringComparison.Ordinal));
                Type entryType = assembly?.GetType(EntryTypeName, false);
                MethodInfo entryPoint = entryType?.GetMethod(
                    "Main", BindingFlags.Public | BindingFlags.Static);
                if (entryPoint == null)
                {
                    Debug.LogError("[FrameworkDemo] HotUpdateMain.Main could not be found. Check the HotUpdate assembly compilation.");
                    return;
                }

                entryPoint.Invoke(null, null);
                Debug.Log("[FrameworkDemo] Local Editor preview started. Release builds use the YooAsset/HybridCLR delivery path.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }
    }
}
#endif