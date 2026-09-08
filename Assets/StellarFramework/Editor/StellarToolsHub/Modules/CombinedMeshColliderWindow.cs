using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace StellarFramework.Editor
{
    /// <summary>
    ///     ToolsHub 内嵌版的 Mesh 合并碰撞体生成工具
    ///     支持批量处理、撤销操作和防镂空逻辑
    /// </summary>
    public class CombinedMeshColliderWindow : ToolsHubEmbeddedPanel
    {
        // ================= 配置项 =================
        private string savePath = "Assets/CombinedMeshes";
        private string colliderName = "CombinedCollider";

        private bool includeMeshRenderer = true;
        private bool includeSkinnedMeshRenderer = true;
        private bool includeParticleSystemRenderer = false;
        private bool includeLineRenderer = false;
        private bool includeTrailRenderer = false;

        private bool bakePoseForSkinnedMesh = true;
        private bool saveMeshAsset = true;

        private TextField _colliderNameField;
        private TextField _savePathField;
        private Toggle _saveMeshToggle;
        private VisualElement _savePathRow;
        private Button _generateButton;
        private Button _clearButton;
        private HelpBox _selectionHelpBox;

        // ================= 绘制入口 =================

        protected override VisualElement BuildView()
        {
            ScrollView root = new ScrollView
            {
                style =
                {
                    flexGrow = 1f
                }
            };

            root.Add(new HelpBox("选中 Hierarchy 中的物体，点击生成按钮即可创建全包围 MeshCollider。支持 Mesh、Skinned、Particle、Line、Trail，并跳过碰撞体不需要的法线计算。", HelpBoxMessageType.Info));

            _colliderNameField = new TextField("碰撞体子物体名称")
            {
                value = colliderName
            };
            _colliderNameField.RegisterValueChangedCallback(evt => colliderName = evt.newValue);
            root.Add(_colliderNameField);

            _saveMeshToggle = new Toggle("保存 Mesh 到资产")
            {
                value = saveMeshAsset
            };
            _saveMeshToggle.RegisterValueChangedCallback(evt =>
            {
                saveMeshAsset = evt.newValue;
                _savePathRow?.SetEnabled(evt.newValue);
            });
            root.Add(_saveMeshToggle);

            _savePathRow = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    marginTop = 4
                }
            };

            _savePathField = new TextField("保存路径")
            {
                value = savePath
            };
            _savePathField.style.flexGrow = 1f;
            _savePathField.RegisterValueChangedCallback(evt => savePath = evt.newValue);
            _savePathRow.Add(_savePathField);

            Button browseButton = new Button(() =>
            {
                string path = EditorUtility.OpenFolderPanel("选择保存文件夹", "Assets", "");
                if (string.IsNullOrEmpty(path))
                {
                    return;
                }

                savePath = path.StartsWith(Application.dataPath)
                    ? "Assets" + path.Substring(Application.dataPath.Length)
                    : path;

                _savePathField.value = savePath;
            })
            {
                text = "..."
            };
            browseButton.style.width = 36;
            browseButton.style.marginLeft = 4;
            _savePathRow.Add(browseButton);
            _savePathRow.SetEnabled(saveMeshAsset);
            root.Add(_savePathRow);

            root.Add(new Label("包含的 Renderer 类型")
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    marginTop = 8
                }
            });

            root.Add(CreateToggle("Mesh Renderer", includeMeshRenderer, value => includeMeshRenderer = value));
            root.Add(CreateToggle("Skinned Mesh Renderer", includeSkinnedMeshRenderer, value => includeSkinnedMeshRenderer = value));
            root.Add(CreateToggle("烘焙当前姿态 (Bake Pose)", bakePoseForSkinnedMesh, value => bakePoseForSkinnedMesh = value));
            root.Add(CreateToggle("Particle System", includeParticleSystemRenderer, value => includeParticleSystemRenderer = value));
            root.Add(CreateToggle("Line Renderer", includeLineRenderer, value => includeLineRenderer = value));
            root.Add(CreateToggle("Trail Renderer", includeTrailRenderer, value => includeTrailRenderer = value));

            _selectionHelpBox = new HelpBox("请在 Hierarchy 中选择至少一个物体。", HelpBoxMessageType.Warning);
            root.Add(_selectionHelpBox);

            _generateButton = new Button(ProcessSelectedObjects)
            {
                text = "为选中物体生成"
            };
            _generateButton.style.height = 32;
            _generateButton.style.marginTop = 8;
            root.Add(_generateButton);

            _clearButton = new Button(ClearSelectedColliders)
            {
                text = "清除选中物体的旧碰撞体"
            };
            _clearButton.style.height = 28;
            root.Add(_clearButton);

            RefreshSelectionUi();
            return root;
        }

        protected override void DrawIMGUI()
        {
            DrawHeader();
            DrawSettings();
            DrawActionButtons();
        }

        protected override void OnSelectionChanged()
        {
            RefreshSelectionUi();
        }

        private void DrawHeader()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Mesh 碰撞体生成工具", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("选中 Hierarchy 中的物体，点击生成按钮即可创建全包围 MeshCollider。\n支持 Mesh、Skinned、Particle、Line、Trail，并跳过碰撞体不需要的法线计算。", MessageType.Info);
            EditorGUILayout.Space(10);
        }

        private void DrawSettings()
        {
            EditorGUILayout.LabelField("基础设置", EditorStyles.boldLabel);
            colliderName = EditorGUILayout.TextField("碰撞体子物体名称", colliderName);
            saveMeshAsset = EditorGUILayout.Toggle("保存 Mesh 到资产", saveMeshAsset);

            if (saveMeshAsset)
            {
                EditorGUILayout.BeginHorizontal();
                savePath = EditorGUILayout.TextField("保存路径", savePath);
                if (GUILayout.Button("...", GUILayout.Width(30)))
                {
                    string path = EditorUtility.OpenFolderPanel("选择保存文件夹", "Assets", "");
                    if (!string.IsNullOrEmpty(path))
                    {
                        // 转换为相对路径
                        if (path.StartsWith(Application.dataPath))
                            savePath = "Assets" + path.Substring(Application.dataPath.Length);
                        else
                            savePath = path; // 非项目路径可能导致保存失败，保持原样让后续逻辑报错即可
                    }
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("包含的 Renderer 类型", EditorStyles.boldLabel);
            includeMeshRenderer = EditorGUILayout.Toggle("Mesh Renderer", includeMeshRenderer);
            includeSkinnedMeshRenderer = EditorGUILayout.Toggle("Skinned Mesh Renderer", includeSkinnedMeshRenderer);
            if (includeSkinnedMeshRenderer)
            {
                EditorGUI.indentLevel++;
                bakePoseForSkinnedMesh = EditorGUILayout.Toggle("烘焙当前姿态 (Bake Pose)", bakePoseForSkinnedMesh);
                EditorGUI.indentLevel--;
            }

            includeParticleSystemRenderer = EditorGUILayout.Toggle("Particle System", includeParticleSystemRenderer);
            includeLineRenderer = EditorGUILayout.Toggle("Line Renderer", includeLineRenderer);
            includeTrailRenderer = EditorGUILayout.Toggle("Trail Renderer", includeTrailRenderer);
        }

        private void DrawActionButtons()
        {
            EditorGUILayout.Space(20);

            var selectedCount = Selection.gameObjects.Length;
            GUI.enabled = selectedCount > 0;

            if (GUILayout.Button($"为选中物体生成 ({selectedCount})", GUILayout.Height(40)))
            {
                ProcessSelectedObjects();
            }

            if (GUILayout.Button("清除选中物体的旧碰撞体"))
            {
                ClearSelectedColliders();
            }

            GUI.enabled = true;

            if (selectedCount == 0)
            {
                EditorGUILayout.HelpBox("请在 Hierarchy 中选择至少一个物体。", MessageType.Warning);
            }
        }

        // ================= 核心逻辑 =================

        private void ProcessSelectedObjects()
        {
            List<GameObject> selectedObjects = GetIndependentSelectedObjects();
            if (selectedObjects.Count == 0)
            {
                Debug.LogWarning("请先选择至少一个有效的根物体。");
                return;
            }

            if (saveMeshAsset && !PrepareAssetSavePath())
            {
                return;
            }

            int successCount = 0;

            try
            {
                for (int i = 0; i < selectedObjects.Count; i++)
                {
                    GameObject root = selectedObjects[i];
                    EditorUtility.DisplayProgressBar(
                        "正在生成碰撞体",
                        $"正在处理: {root.name} ({i + 1}/{selectedObjects.Count})",
                        (float)(i + 1) / selectedObjects.Count);

                    try
                    {
                        if (CreateCombinedMeshCollider(root))
                        {
                            successCount++;
                        }
                    }
                    catch (Exception exception)
                    {
                        Debug.LogError($"[{root.name}] 生成碰撞体时发生未处理异常: {exception}");
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                if (saveMeshAsset && successCount > 0)
                {
                    AssetDatabase.SaveAssets();
                }
            }

            Debug.Log($"<color=green>批量处理完成: 成功 {successCount} / 总计 {selectedObjects.Count}</color>");
        }

        private void ClearSelectedColliders()
        {
            foreach (var root in Selection.gameObjects)
            {
                var existing = root.transform.Find(colliderName);
                if (existing != null)
                {
                    Undo.DestroyObjectImmediate(existing.gameObject);
                }
            }
        }

        private bool CreateCombinedMeshCollider(GameObject root)
        {
            var buildContext = new CombineBuildContext();
            Mesh combinedMesh = null;
            bool meshSavedAsAsset = false;

            try
            {
                CollectMeshes(root, buildContext);

                if (buildContext.CombineInstances.Count == 0)
                {
                    Debug.LogWarning($"[{root.name}] 未找到有效的 Mesh Renderer，跳过。");
                    return false;
                }

                combinedMesh = new Mesh
                {
                    name = $"{root.name}_CombinedMesh",
                    indexFormat = buildContext.VertexCount > ushort.MaxValue
                        ? IndexFormat.UInt32
                        : IndexFormat.UInt16
                };

                // MeshCollider 只使用顶点和三角形索引。CombineMeshes 已经生成 bounds，
                // 不再额外 RecalculateNormals，避免对大型静态网格执行一次无意义的 O(n) 计算。
                combinedMesh.CombineMeshes(buildContext.CombineInstances.ToArray(), true, true);

                // 只有在新 Mesh 合并成功后才移除旧碰撞体，失败时保留原结果。
                Transform existing = root.transform.Find(colliderName);
                if (existing != null)
                {
                    Undo.DestroyObjectImmediate(existing.gameObject);
                }

                if (saveMeshAsset)
                {
                    meshSavedAsAsset = SaveMeshAsset(combinedMesh);
                }

                GameObject colliderObj = new GameObject(colliderName);
                colliderObj.transform.SetParent(root.transform);
                colliderObj.transform.localPosition = Vector3.zero;
                colliderObj.transform.localRotation = Quaternion.identity;
                colliderObj.transform.localScale = Vector3.one;

                MeshCollider meshCollider = colliderObj.AddComponent<MeshCollider>();
                meshCollider.sharedMesh = combinedMesh;
                meshCollider.convex = false;

                Undo.RegisterCreatedObjectUndo(colliderObj, "Create Combined Collider");
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError($"[{root.name}] Mesh 合并失败: {exception.Message}");
                if (combinedMesh != null && !meshSavedAsAsset)
                {
                    UnityEngine.Object.DestroyImmediate(combinedMesh);
                }

                return false;
            }
            finally
            {
                buildContext.Dispose();
            }
        }

        private bool SaveMeshAsset(Mesh mesh)
        {
            if (!TryNormalizeAssetFolderPath(savePath, out string normalizedPath))
            {
                Debug.LogError($"保存路径必须位于当前工程的 Assets 目录下: {savePath}");
                return false;
            }

            string fileName = SanitizeFileName(mesh.name);
            string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{normalizedPath}/{fileName}.asset");

            try
            {
                AssetDatabase.CreateAsset(mesh, assetPath);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError($"无法保存合并 Mesh 资产 ({assetPath}): {exception.Message}");
                return false;
            }
        }

        // ================= 收集逻辑 (复用并适配) =================

        private void CollectMeshes(GameObject root, CombineBuildContext buildContext)
        {
            Matrix4x4 matrixRoot = root.transform.worldToLocalMatrix;
            Transform[] transforms = root.GetComponentsInChildren<Transform>(false);

            // 一次遍历层级，同时检查所有 Renderer，避免为每种 Renderer 重复扫描同一棵树。
            foreach (Transform child in transforms)
            {
                Matrix4x4 transformMatrix = matrixRoot * child.localToWorldMatrix;

                if (includeMeshRenderer && child.TryGetComponent(out MeshRenderer meshRenderer))
                {
                    if (meshRenderer.enabled && child.TryGetComponent(out MeshFilter meshFilter))
                    {
                        AddMeshWithSubMeshes(buildContext, meshFilter.sharedMesh, transformMatrix);
                    }
                }

                if (includeSkinnedMeshRenderer && child.TryGetComponent(out SkinnedMeshRenderer skinnedRenderer))
                {
                    if (skinnedRenderer.enabled && skinnedRenderer.sharedMesh != null)
                    {
                        Mesh mesh = skinnedRenderer.sharedMesh;
                        if (bakePoseForSkinnedMesh)
                        {
                            mesh = new Mesh
                            {
                                name = $"{skinnedRenderer.name}_BakedMesh"
                            };
                            buildContext.TemporaryMeshes.Add(mesh);
                            skinnedRenderer.BakeMesh(mesh);
                        }

                        AddMeshWithSubMeshes(buildContext, mesh, transformMatrix);
                    }
                }

                if (includeParticleSystemRenderer && child.TryGetComponent(out ParticleSystemRenderer particleRenderer))
                {
                    if (particleRenderer.enabled)
                    {
                        AddMeshWithSubMeshes(buildContext, particleRenderer.mesh, transformMatrix);
                    }
                }

                if (includeLineRenderer && child.TryGetComponent(out LineRenderer lineRenderer))
                {
                    if (lineRenderer.enabled && lineRenderer.positionCount > 0)
                    {
                        Mesh mesh = new Mesh
                        {
                            name = $"{lineRenderer.name}_BakedMesh"
                        };
                        buildContext.TemporaryMeshes.Add(mesh);
                        lineRenderer.BakeMesh(mesh, false);
                        AddMeshWithSubMeshes(buildContext, mesh, transformMatrix);
                    }
                }

                if (includeTrailRenderer && child.TryGetComponent(out TrailRenderer trailRenderer))
                {
                    if (trailRenderer.enabled)
                    {
                        Mesh mesh = new Mesh
                        {
                            name = $"{trailRenderer.name}_BakedMesh"
                        };
                        buildContext.TemporaryMeshes.Add(mesh);
                        trailRenderer.BakeMesh(mesh, false);
                        AddMeshWithSubMeshes(buildContext, mesh, transformMatrix);
                    }
                }
            }
        }

        /// <summary>
        ///     核心修复：遍历所有 SubMesh 以防止镂空
        /// </summary>
        private static void AddMeshWithSubMeshes(CombineBuildContext buildContext, Mesh mesh, Matrix4x4 transformMatrix)
        {
            if (mesh == null || mesh.vertexCount == 0)
            {
                return;
            }

            for (int i = 0; i < mesh.subMeshCount; i++)
            {
                if (mesh.GetIndexCount(i) == 0)
                {
                    continue;
                }

                // CombineMeshes 会为每个 CombineInstance 追加顶点数据；多 SubMesh
                // 需要逐项计数，确保大网格正确切换到 UInt32 索引。
                buildContext.VertexCount += mesh.vertexCount;
                buildContext.CombineInstances.Add(new CombineInstance
                {
                    mesh = mesh,
                    subMeshIndex = i,
                    transform = transformMatrix
                });
            }
        }

        private static List<GameObject> GetIndependentSelectedObjects()
        {
            GameObject[] selected = Selection.gameObjects;
            var selectedTransforms = new HashSet<Transform>();
            for (int i = 0; i < selected.Length; i++)
            {
                selectedTransforms.Add(selected[i].transform);
            }

            var result = new List<GameObject>(selected.Length);
            for (int i = 0; i < selected.Length; i++)
            {
                Transform parent = selected[i].transform.parent;
                bool parentSelected = false;
                while (parent != null)
                {
                    if (selectedTransforms.Contains(parent))
                    {
                        parentSelected = true;
                        break;
                    }

                    parent = parent.parent;
                }

                if (!parentSelected)
                {
                    result.Add(selected[i]);
                }
            }

            return result;
        }

        private bool PrepareAssetSavePath()
        {
            if (!TryNormalizeAssetFolderPath(savePath, out string normalizedPath))
            {
                Debug.LogError($"保存路径必须位于当前工程的 Assets 目录下: {savePath}");
                return false;
            }

            savePath = normalizedPath;
            if (AssetDatabase.IsValidFolder(normalizedPath))
            {
                return true;
            }

            string[] parts = normalizedPath.Split('/');
            string currentFolder = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string nextFolder = $"{currentFolder}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(nextFolder) &&
                    string.IsNullOrEmpty(AssetDatabase.CreateFolder(currentFolder, parts[i])))
                {
                    Debug.LogError($"无法创建 Mesh 保存目录: {nextFolder}");
                    return false;
                }

                currentFolder = nextFolder;
            }

            return AssetDatabase.IsValidFolder(normalizedPath);
        }

        private static bool TryNormalizeAssetFolderPath(string path, out string normalizedPath)
        {
            normalizedPath = null;
            string candidate = path?.Trim().Replace('\\', '/').TrimEnd('/');
            if (string.IsNullOrEmpty(candidate) ||
                (!candidate.Equals("Assets", StringComparison.OrdinalIgnoreCase) &&
                 !candidate.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot))
            {
                return false;
            }

            string candidateFullPath = Path.GetFullPath(Path.Combine(
                projectRoot,
                candidate.Replace('/', Path.DirectorySeparatorChar)));
            string assetsFullPath = Path.GetFullPath(Application.dataPath);
            string assetsPrefix = assetsFullPath.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!candidateFullPath.Equals(assetsFullPath, StringComparison.OrdinalIgnoreCase) &&
                !candidateFullPath.StartsWith(assetsPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            normalizedPath = candidate;
            return true;
        }

        private static string SanitizeFileName(string fileName)
        {
            char[] chars = (string.IsNullOrEmpty(fileName) ? "CombinedMesh" : fileName).ToCharArray();
            char[] invalidChars = Path.GetInvalidFileNameChars();
            for (int i = 0; i < chars.Length; i++)
            {
                for (int j = 0; j < invalidChars.Length; j++)
                {
                    if (chars[i] == invalidChars[j])
                    {
                        chars[i] = '_';
                        break;
                    }
                }
            }

            return new string(chars);
        }

        private sealed class CombineBuildContext
        {
            internal readonly List<CombineInstance> CombineInstances = new List<CombineInstance>(64);
            internal readonly List<Mesh> TemporaryMeshes = new List<Mesh>(8);
            internal long VertexCount;

            internal void Dispose()
            {
                for (int i = 0; i < TemporaryMeshes.Count; i++)
                {
                    if (TemporaryMeshes[i] != null)
                    {
                        UnityEngine.Object.DestroyImmediate(TemporaryMeshes[i]);
                    }
                }

                TemporaryMeshes.Clear();
            }
        }

        private void RefreshSelectionUi()
        {
            int selectedCount = Selection.gameObjects.Length;

            if (_generateButton != null)
            {
                _generateButton.text = $"为选中物体生成 ({selectedCount})";
                _generateButton.SetEnabled(selectedCount > 0);
            }

            if (_clearButton != null)
            {
                _clearButton.SetEnabled(selectedCount > 0);
            }

            if (_selectionHelpBox != null)
            {
                _selectionHelpBox.style.display = selectedCount == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        private static Toggle CreateToggle(string label, bool value, System.Action<bool> onChanged)
        {
            Toggle toggle = new Toggle(label)
            {
                value = value
            };
            toggle.RegisterValueChangedCallback(evt => onChanged(evt.newValue));
            return toggle;
        }
    }
}
